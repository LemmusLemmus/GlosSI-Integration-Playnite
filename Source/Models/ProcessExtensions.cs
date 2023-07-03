using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace GlosSIIntegration.Models
{
    static class ProcessExtensions
    {
        private const uint WM_CLOSE = 0x0010;
        /// <summary>
        /// Exit code if a process has not terminated.
        /// </summary>
        private const int STILL_ACTIVE = 259;
        [Flags]
        public enum ProcessAccessFlags : uint
        {
            PROCESS_QUERY_LIMITED_INFORMATION = 0x00001000,
        }

        [DllImport("user32.dll")]
        private static extern IntPtr FindWindow(IntPtr lpClassName, string lpWindowName);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(ProcessAccessFlags dwDesiredAccess, bool bInheritHandle, int dwProcessId);
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool GetExitCodeProcess(IntPtr hProcess, out uint lpExitCode);
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool CloseHandle(IntPtr handle);

        /// <summary>
        /// Closes a process by its name. 
        /// Note that the process might not have closed yet when the method returns.
        /// </summary>
        /// <param name="windowName">The name of the process window.</param>
        /// <exception cref="InvalidOperationException">If the process window was not found. 
        /// This could be because the process has already closed.</exception>
        /// <exception cref="Win32Exception">If Win32 PostMessage failed.</exception>
        public static void CloseProcessByName(string windowName)
        {
            IntPtr hWnd = FindWindow(windowName);

            if (hWnd == IntPtr.Zero)
            {
                throw new InvalidOperationException($"No \"{ windowName }\" window to close was found.");
            }

            if (!PostMessage(hWnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }

        /// <summary>
        /// Finds a window by its name. 
        /// See <a href="https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-findwindowa">
        /// Win32 FindWindow</a>.
        /// </summary>
        /// <param name="windowName">The name of the window.</param>
        /// <returns>The window handle, or <see cref="IntPtr.Zero"/> if it fails.</returns>
        public static IntPtr FindWindow(string windowName)
        {
            return FindWindow(IntPtr.Zero, windowName);
        }

        /// <summary>
        /// Checks if a process has exited. Replaces <see cref="Process.HasExited"/>. 
        /// Unlike <see cref="Process.HasExited"/>, this method will not throw access denied 
        /// when the process is run with elevated privileges. 
        /// See <a href="https://www.giorgi.dev/net/access-denied-process-bugs/">this webpage</a> 
        /// for details regarding the bug.
        /// Note that this method is currently simpler and not as robust as <see cref="Process.HasExited"/>, 
        /// since it only checks if the exit code is not <c>259</c>.
        /// </summary>
        /// <param name="process">The process to check if it has exited.</param>
        /// <returns>True if the process has exited; false otherwise.</returns>
        /// <exception cref="Win32Exception">If something went wrong...</exception>
        public static bool HasExitedSafe(this Process process)
        {
            IntPtr hProcess = OpenProcess(ProcessAccessFlags.PROCESS_QUERY_LIMITED_INFORMATION, false, process.Id);

            if (hProcess == null)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            if (!GetExitCodeProcess(hProcess, out uint exitCode))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            if (!CloseHandle(hProcess))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            return exitCode != STILL_ACTIVE;
        }

        // Method stolen from
        // https://github.com/microsoft/vs-threading/blob/main/src/Microsoft.VisualStudio.Threading/AwaitExtensions.cs
        // Changed to use HasExitedSafe() instead.
        /// <summary>
        /// Returns a task that completes when the process exits and provides the exit code of that process.
        /// </summary>
        /// <param name="process">The process to wait for exit.</param>
        /// <param name="cancellationToken">
        /// A token whose cancellation will cause the returned Task to complete
        /// before the process exits in a faulted state with an <see cref="OperationCanceledException"/>.
        /// This token has no effect on the <paramref name="process"/> itself.
        /// </param>
        /// <returns>A task whose result is the <see cref="Process.ExitCode"/> of the <paramref name="process"/>.</returns>
        public static async Task<int> WaitForExitAsyncSafe(this Process process, CancellationToken cancellationToken = default)
        {
            TaskCompletionSource<int> tcs = new TaskCompletionSource<int>();

            void exitHandler(object s, EventArgs e)
            {
                tcs.TrySetResult(process.ExitCode);
            }

            try
            {
                process.EnableRaisingEvents = true;
                process.Exited += exitHandler;
                if (process.HasExitedSafe()) // TODO: Could directly get the exit code from HasExitedSafe() here...
                {
                    // Allow for the race condition that the process has already exited.
                    tcs.TrySetResult(process.ExitCode);
                }

                using (cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken)))
                {
                    return await tcs.Task.ConfigureAwait(false);
                }
            }
            finally
            {
                process.Exited -= exitHandler;
            }
        }
    }
}
