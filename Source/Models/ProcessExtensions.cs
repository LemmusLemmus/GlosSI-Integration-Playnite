using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace GlosSIIntegration.Models
{
    internal static class ProcessExtensions
    {
        #region Win32
        /// <summary>
        /// Exit code if a process has not terminated 
        /// (unless the process exited with this exit code).
        /// </summary>
        private const int StillActiveExitCode = 259;
        [Flags]
        public enum ProcessAccessFlags : uint
        {
            ProcessQueryLimitedInformation = 0x00001000,
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(ProcessAccessFlags dwDesiredAccess, bool bInheritHandle, int dwProcessId);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetExitCodeProcess(IntPtr hProcess, out uint lpExitCode);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr handle);
        #endregion

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
            IntPtr hProcess = OpenProcess(ProcessAccessFlags.ProcessQueryLimitedInformation, false, process.Id);

            if (hProcess == null)
            {
                throw new Win32Exception();
            }

            if (!GetExitCodeProcess(hProcess, out uint exitCode))
            {
                CloseHandle(hProcess);
                throw new Win32Exception();
            }

            if (!CloseHandle(hProcess))
            {
                throw new Win32Exception();
            }

            return exitCode != StillActiveExitCode;
        }

        // Method stolen from
        // https://github.com/microsoft/vs-threading/blob/main/src/Microsoft.VisualStudio.Threading/AwaitExtensions.cs
        // Changed to use HasExitedSafe() instead.
        /* Original license:
        Microsoft.VisualStudio.Threading
        Copyright (c) Microsoft Corporation
        All rights reserved. 

        MIT License

        Permission is hereby granted, free of charge, to any person obtaining a copy
        of this software and associated documentation files (the "Software"), to deal
        in the Software without restriction, including without limitation the rights
        to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
        copies of the Software, and to permit persons to whom the Software is
        furnished to do so, subject to the following conditions:

        The above copyright notice and this permission notice shall be included in all
        copies or substantial portions of the Software.

        THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
        IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
        FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
        AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
        LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
        OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
        SOFTWARE.
        */
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
