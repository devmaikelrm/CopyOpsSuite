using System;
using System.Runtime.InteropServices;

namespace CopyOpsSuite.App.WinUI.Services
{
    internal sealed class WindowMessageHook : IDisposable
    {
        public delegate IntPtr WindowProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

        private const int GWLP_WNDPROC = -4;
        private readonly IntPtr _hwnd;
        private readonly WindowProc _callback;
        private readonly IntPtr _oldWndProc;
        private bool _disposed;

        public WindowMessageHook(IntPtr hwnd, WindowProc callback)
        {
            _hwnd = hwnd;
            _callback = callback ?? throw new ArgumentNullException(nameof(callback));
            _oldWndProc = SetWindowLongPtr(hwnd, GWLP_WNDPROC, Marshal.GetFunctionPointerForDelegate(_callback));
        }

        public IntPtr CallOriginal(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            return CallWindowProc(_oldWndProc, hwnd, msg, wParam, lParam);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            SetWindowLongPtr(_hwnd, GWLP_WNDPROC, _oldWndProc);
            _disposed = true;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
    }
}
