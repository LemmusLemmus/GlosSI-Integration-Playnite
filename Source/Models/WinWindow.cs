using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace GlosSIIntegration.Models
{
    /// <summary>
    /// Represents a Windows window with some useful operations. Note that the window can be closed at any time.
    /// </summary>
    internal class WinWindow
    {
        #region Win32
        [DllImport("User32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("User32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindow(IntPtr intPtrZero, string lpWindowName);

        [DllImport("User32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("User32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("User32.dll", SetLastError = true)]
        private static extern bool LockSetForegroundWindow(SetForegroundWindowLock lockState);

        private enum SetForegroundWindowLock : int
        {
            Lock = 1,
            Unlock = 2
        }

        [DllImport("User32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("User32.dll", SetLastError = true)]
        private static extern bool PostMessage(IntPtr hWnd, WindowMessage msg, IntPtr wParam, IntPtr lParam);

        private enum WindowMessage : uint
        {
            Close = 0x0010
        }

        [DllImport("User32.dll")]
        private static extern bool EnableWindow(IntPtr hWnd, bool enable);

        [DllImport("User32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, ShowWindowOption nCmdShow);

        /// <summary>
        /// Controls how a window is to be shown when passed to <see cref="ShowWindow(IntPtr, ShowWindowOption)"/>
        /// </summary>
        private enum ShowWindowOption : int
        {
            /// <summary>
            /// Hides the window and activates another window.
            /// </summary>
            Hide,
            ShowNormal,
            Normal = 1,
            ShowMinimized,
            ShowMaximized,
            Maximize = 3,
            ShowNoActivate,
            Show,
            Minimize,
            ShowMinNoActive,
            ShowNA,
            Restore,
            ShowDefault,
            ForceMinimize
        }

        [DllImport("Dwmapi.dll", PreserveSig = false)]
        private static extern void DwmSetWindowAttribute(IntPtr hWnd, DwmWindowAttribute dwAttribute, ref int pvAttribute, int cbAttribute);

        private enum DwmWindowAttribute : uint
        {
            NcRendering_Enabled = 1,
            NcRendering_Policy,
            Transitions_ForceDisabled,
            Allow_NcPaint,
            Caption_Button_Bounds,
            Nonclient_Rtl_Layout,
            Force_Iconic_Representation,
            Flip3D_Policy,
            Extended_Frame_Bounds,
            Has_Iconic_Bitmap,
            Disallow_Peek,
            Excluded_From_Peek,
            Cloak,
            Cloaked,
            Freeze_Representation,
            Passive_Update_Mode,
            Use_HostBackdropBrush,
            Use_Immersive_Dark_Mode = 20,
            Window_Corner_Preference = 33,
            Border_Color,
            Caption_Color,
            Text_Color,
            Visible_Frame_Border_Thickness,
            SystemBackdrop_Type,
            Last
        };
        #endregion Win32

        protected readonly IntPtr handle;

        protected WinWindow(IntPtr hWnd)
        {
            handle = hWnd;
        }

        /// <summary>
        /// Finds a window by its name and window class.
        /// See <a href="https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-findwindowa">
        /// Win32 FindWindow</a>.
        /// </summary>
        /// <param name="windowClassName">The name of the window's window class.</param>
        /// <param name="windowName">The name of the window.</param>
        /// <returns>The window, or <c>null</c> if it fails.</returns>
        public static WinWindow Find(string windowClassName, string windowName)
        {
            return TryInstantiate(FindWindow(windowClassName, windowName));
        }

        public static WinWindow GetFocusedWindow()
        {
            return TryInstantiate(GetForegroundWindow());
        }

        /// <summary>
        /// Finds a window by its name. 
        /// See <a href="https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-findwindowa">
        /// Win32 FindWindow</a>.
        /// </summary>
        /// <param name="windowName">The name of the window.</param>
        /// <returns>The window, or <c>null</c> if it fails.</returns>
        public static WinWindow Find(string windowName)
        {
            return TryInstantiate(FindWindow(IntPtr.Zero, windowName));
        }

        /// <summary>
        /// Instantiates a <see cref="WinWindow"/> from a window handle, 
        /// provided that the handle is not <see cref="IntPtr.Zero"/>.
        /// </summary>
        /// <param name="hWnd">The window handle of the WinWindow to be instantiated.</param>
        /// <returns>The instantiated <see cref="WinWindow"/>, 
        /// or <c>null</c> if <c><paramref name="hWnd"/> == <see cref="IntPtr.Zero"/></c>.</returns>
        private static WinWindow TryInstantiate(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero)
            {
                return null;
            }

            return new WinWindow(hWnd);
        }

        public static bool LockSetForegroundWindow()
        {
            return LockSetForegroundWindow(SetForegroundWindowLock.Lock);
        }

        public static void UnlockSetForegroundWindow()
        {
            LockSetForegroundWindow(SetForegroundWindowLock.Unlock);
        }

        /// <summary>
        /// Enables user input to the window.
        /// </summary>
        public void EnableInput()
        {
            EnableWindow(handle, true);
        }

        /// <summary>
        /// Disables user input to the window.
        /// </summary>
        public void DisableInput()
        {
            EnableWindow(handle, false);
        }

        /// <summary>
        /// Enables window transitions animations.
        /// </summary>
        public void EnableTransitionsAnimations()
        {
            SetTransitionsAnimations(0);
        }

        /// <summary>
        /// Disables window transitions animations.
        /// </summary>
        public void DisableTransitionsAnimations()
        {
            SetTransitionsAnimations(1);
        }

        /// <summary>
        /// Sets window transitions animations on or off.
        /// </summary>
        /// <param name="disable">true to disable animations; false otherwise.</param>
        private void SetTransitionsAnimations(int disable)
        {
            DwmSetWindowAttribute(handle, DwmWindowAttribute.Transitions_ForceDisabled, ref disable, sizeof(int));
        }

        public override bool Equals(object obj)
        {
            return obj is WinWindow otherWindow && handle == otherWindow.handle;
        }

        public override int GetHashCode()
        {
            return handle.ToInt32();
        }

        public uint GetProcessId()
        {
            return GetWindowThreadProcessId(handle, out uint pid) == 0 ? throw new Win32Exception() : pid;
        }

        /// <summary>
        /// Checks if the window is the foreground window.
        /// </summary>
        /// <returns>true if this window is the foreground window; false otherwise.</returns>
        public bool IsFocused()
        {
            return handle == GetForegroundWindow();
        }

        /// <summary>
        /// Tells the window to please close.
        /// </summary>
        /// <exception cref="InvalidOperationException">If closing the window failed, 
        /// for example if the window has already been closed.</exception>
        public void Close()
        {
            if (!PostMessage(handle, WindowMessage.Close, IntPtr.Zero, IntPtr.Zero))
            {
                throw new InvalidOperationException("Failed to close the window", new Win32Exception());
            }
        }

        /// <summary>
        /// Tries to focus the window.
        /// </summary>
        /// <returns>true if the window was focused; false otherwise.</returns>
        public bool Focus()
        {
            return SetForegroundWindow(handle);
        }

        /// <summary>
        /// Minimizes the window without activating it.
        /// </summary>
        public void Minimize()
        {
            ShowWindow(handle, ShowWindowOption.ShowMinNoActive);
        }

        /// <summary>
        /// Shows the window.
        /// </summary>
        public void Show()
        {
            ShowWindow(handle, ShowWindowOption.Show);
        }
    }
}
