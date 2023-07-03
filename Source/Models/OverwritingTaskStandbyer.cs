using System;
using System.Threading.Tasks;

namespace GlosSIIntegration.Models
{
    /// <summary>
    /// Schedules tasks to be run one at a time asynchronously. 
    /// Any task not currently being run is overwritten if a new task is scheduled.
    /// </summary>
    class OverwritingTaskStandbyer // TODO: Could use a better name.
    {
        private Task currentTask;
        private Task readyNextTaskTask;
        private Func<Task> taskOnStandbyFunc;
        private readonly Action<Exception> userExceptionHandler;
        private readonly bool continueOnCapturedContext;

        /// <summary>
        /// Locks access to <see cref="currentTask"/> and 
        /// <see cref="taskOnStandbyFunc"/>.
        /// </summary>
        private readonly object taskLock;

        /// <summary>
        /// Instantiates a new <see cref="OverwritingTaskStandbyer"/> object. 
        /// </summary>
        /// <param name="exceptionHandler">Handles any exception thrown by 
        /// the scheduled task returned by a supplied Func&lt;Task&gt; or 
        /// by the function itself. 
        /// The <paramref name="exceptionHandler"/> should not throw 
        /// an exception itself. </param>
        /// <param name="continueOnCapturedContext">
        /// <paramref name="continueOnCapturedContext"/> is passed to all 
        /// awaits via <see cref="Task.ConfigureAwait(bool)"/></param>
        public OverwritingTaskStandbyer(Action<Exception> exceptionHandler, 
            bool continueOnCapturedContext)
        {
            currentTask = null;
            readyNextTaskTask = null;
            taskOnStandbyFunc = null;
            taskLock = new object();
            userExceptionHandler = exceptionHandler;
            this.continueOnCapturedContext = continueOnCapturedContext;
        }

        /// <summary>
        /// Schedules a new task to be run and overwrites any old task on standby. 
        /// Send <c>null</c> to simply clear any task on standby.
        /// <para>
        /// Note: The method must be called synchronously.
        /// </para>
        /// </summary>
        /// <param name="newTask">The function returning a task to be run. 
        /// The function is permitted to return <c>null</c> and run its 
        /// logic partly or entirely inside the <see cref="Func{TResult}]"/>. 
        /// Alternatively, send <c>null</c> to simply clear any task on standby.
        /// </param>
        public void StartNewTask(Func<Task> newTask)
        {
            if (newTask == null)
            {
                lock (taskLock) taskOnStandbyFunc = null;
                return;
            }

            lock (taskLock)
            {
                if (currentTask != null)
                {
                    // Update/put task on standby.
                    taskOnStandbyFunc = newTask;
                }
                else
                {
                    // Start task now.
                    try
                    {
                        currentTask = newTask.Invoke();
                        if (currentTask != null)
                        {
                            readyNextTaskTask = ReadyNextTask();
                        }
                    }
                    catch (Exception ex)
                    {
                        userExceptionHandler(ex);
                    }
                }
            }
        }

        /// <summary>
        /// Readies the next task(s). Starts the task and waits for it to finish.
        /// Starts the next task and waits for it to finish for as long as 
        /// there are new tasks on standby.
        /// <para>
        /// Note: <see cref="currentTask"/> may not be <c>null</c> when 
        /// this method is called, and it may not be changed anywhere outside 
        /// this method after this method has been called until 
        /// <see cref="currentTask"/> is set to <c>null</c> 
        /// (i.e. until this Task completes / until 
        /// <see cref="readyNextTaskTask"/> is set to <c>null</c>).
        /// </para>
        /// </summary>
        private async Task ReadyNextTask()
        {
            bool waitOnAnotherTask;

            do
            {
                try
                {
                    // currentTask should not be able to be null,
                    // since it is only ever set to null in this method and 
                    // this method is never called with currentTask set to null.
                    await currentTask.ConfigureAwait(continueOnCapturedContext);
                }
                catch (Exception ex)
                {
                    userExceptionHandler(ex);
                }

                lock (taskLock)
                {
                    try
                    {
                        currentTask = taskOnStandbyFunc?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        currentTask = null;
                        userExceptionHandler(ex);
                    }

                    taskOnStandbyFunc = null;
                    waitOnAnotherTask = currentTask != null;
                    if (!waitOnAnotherTask) readyNextTaskTask = null;
                }

            } while (waitOnAnotherTask);
        }

        /// <summary>
        /// Synchronously waits for all tasks to finish.
        /// </summary>
        public void WaitOnTasks()
        {
            Task taskToWaitOn;

            lock (taskLock)
            {
                taskToWaitOn = readyNextTaskTask;
            }

            try
            {
                taskToWaitOn?.Wait();
            }
            catch
            {
                // Exceptions are handled elsewhere (by ReadyNextTask()).
            }
        }
    }
}
