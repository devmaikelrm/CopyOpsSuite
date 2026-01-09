using System;
using System.Runtime.InteropServices;
using System.IO;
using CopyOpsSuite.App.WinUI.ViewModels;
using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace CopyOpsSuite.App.WinUI.Services
{
    internal sealed class TrayIconService : IDisposable
    {
        private const uint NIF_MESSAGE = 0x00000001;
        private const uint NIF_ICON = 0x00000002;
        private const uint NIF_TIP = 0x00000004;
        private const uint NIM_ADD = 0x00000000;
        private const uint NIM_DELETE = 0x00000002;
        private const int WM_APP = 0x8000;
        private const int WM_TRAYICON = WM_APP + 1;
        private const int WM_LBUTTONUP = 0x0202;
        private const int WM_RBUTTONUP = 0x0205;
        private const uint TPM_RETURNCMD = 0x0100;
        private const uint TPM_RIGHTBUTTON = 0x0002;
        private const int SW_HIDE = 0;
        private const int SW_SHOW = 5;

        private const uint MENU_OPEN_COPY = 1001;
        private const uint MENU_OPEN_CASH = 1002;
        private const uint MENU_OPEN_AUDIT = 1003;
        private const uint MENU_TOGGLE_PAUSE = 1004;
        private const uint MENU_DIAGNOSTICS = 1005;
        private const uint MENU_EXIT = 1006;

        private readonly AppServices _services;
        private readonly IntPtr _hwnd;
        private readonly WindowMessageHook _hook;
        private readonly uint _iconId = 1;
        private readonly IntPtr _trayIcon;
        private readonly bool _ownsTrayIcon;
        private bool _disposed;

        public bool IsExitRequested { get; private set; }

        public TrayIconService(AppServices services, Window window)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
            if (window == null) throw new ArgumentNullException(nameof(window));

            _hwnd = WindowNative.GetWindowHandle(window);
            _hook = new WindowMessageHook(_hwnd, WndProc);
            _trayIcon = LoadTrayIcon(out _ownsTrayIcon);
            AddIcon();
        }

        public void HideWindow()
        {
            ShowWindow(_hwnd, SW_HIDE);
        }

        public void ShowWindow(string? tag = null)
        {
            if (tag != null)
            {
                NavigateTo(tag);
            }

            ShowWindow(_hwnd, SW_SHOW);
            App.MainWindowInstance?.Activate();
        }

        public void ToggleWindow()
        {
            if (IsWindowVisible(_hwnd))
            {
                HideWindow();
            }
            else
            {
                ShowWindow();
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            RemoveIcon();
            _hook.Dispose();
            _disposed = true;
        }

        private void AddIcon()
        {
            var data = new NotifyIconData
            {
                cbSize = (uint)Marshal.SizeOf<NotifyIconData>(),
                hWnd = _hwnd,
                uID = _iconId,
                uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
                uCallbackMessage = WM_TRAYICON,
                hIcon = _trayIcon,
                szTip = "CopyOps Suite"
            };

            Shell_NotifyIcon(NIM_ADD, ref data);
        }

        private void RemoveIcon()
        {
            var data = new NotifyIconData
            {
                cbSize = (uint)Marshal.SizeOf<NotifyIconData>(),
                hWnd = _hwnd,
                uID = _iconId
            };

            Shell_NotifyIcon(NIM_DELETE, ref data);

            if (_ownsTrayIcon && _trayIcon != IntPtr.Zero)
            {
                DestroyIcon(_trayIcon);
            }
        }

        private IntPtr WndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == WM_TRAYICON)
            {
                var message = lParam.ToInt32();
                if (message == WM_LBUTTONUP)
                {
                    ToggleWindow();
                }
                else if (message == WM_RBUTTONUP)
                {
                    ShowContextMenu();
                }

                return IntPtr.Zero;
            }

            return _hook.CallOriginal(hwnd, msg, wParam, lParam);
        }

        private void ShowContextMenu()
        {
            var menu = CreatePopupMenu();
            if (menu == IntPtr.Zero)
            {
                return;
            }

            AppendMenu(menu, 0, MENU_OPEN_COPY, "Abrir Copy");
            AppendMenu(menu, 0, MENU_OPEN_CASH, "Caja");
            AppendMenu(menu, 0, MENU_OPEN_AUDIT, "Auditoria");
            AppendMenu(menu, 0, MENU_TOGGLE_PAUSE, _services.MultiCopyEngine.IsPaused ? "Reanudar" : "Pausar");
            AppendMenu(menu, 0, MENU_DIAGNOSTICS, "Ejecutar diagnosticos");
            AppendMenu(menu, 0, MENU_EXIT, "Salir");

            GetCursorPos(out var point);
            SetForegroundWindow(_hwnd);
            var selected = TrackPopupMenu(menu, TPM_RETURNCMD | TPM_RIGHTBUTTON, point.X, point.Y, 0, _hwnd, IntPtr.Zero);
            DestroyMenu(menu);

            if (selected == 0)
            {
                return;
            }

            HandleMenuCommand(selected);
        }

        private void HandleMenuCommand(uint command)
        {
            switch (command)
            {
                case MENU_OPEN_COPY:
                    ShowWindow("Copy");
                    break;
                case MENU_OPEN_CASH:
                    ShowWindow("Cash");
                    break;
                case MENU_OPEN_AUDIT:
                    ShowWindow("Audit");
                    break;
                case MENU_TOGGLE_PAUSE:
                    _services.MultiCopyEngine.SetPaused(!_services.MultiCopyEngine.IsPaused);
                    break;
                case MENU_DIAGNOSTICS:
                    ShowWindow("Settings");
                    _services.RequestDiagnostics();
                    break;
                case MENU_EXIT:
                    IsExitRequested = true;
                    Dispose();
                    Application.Current.Exit();
                    break;
            }
        }

        private void NavigateTo(string tag)
        {
            if (App.MainWindowInstance?.DataContext is ShellViewModel shell)
            {
                shell.SelectedTag = tag;
            }
        }

        private static IntPtr LoadTrayIcon(out bool ownsIcon)
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Assets", "CopyOpsSuite.ico");
            if (!File.Exists(path))
            {
                ownsIcon = false;
                return LoadIcon(IntPtr.Zero, (IntPtr)0x7F00);
            }

            const uint IMAGE_ICON = 1;
            const uint LR_LOADFROMFILE = 0x0010;
            const uint LR_DEFAULTSIZE = 0x0040;
            var icon = LoadImage(IntPtr.Zero, path, IMAGE_ICON, 0, 0, LR_LOADFROMFILE | LR_DEFAULTSIZE);
            if (icon == IntPtr.Zero)
            {
                ownsIcon = false;
                return LoadIcon(IntPtr.Zero, (IntPtr)0x7F00);
            }

            ownsIcon = true;
            return icon;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct NotifyIconData
        {
            public uint cbSize;
            public IntPtr hWnd;
            public uint uID;
            public uint uFlags;
            public uint uCallbackMessage;
            public IntPtr hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szTip;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Point
        {
            public int X;
            public int Y;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool Shell_NotifyIcon(uint dwMessage, ref NotifyIconData data);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr CreatePopupMenu();

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool AppendMenu(IntPtr hMenu, uint uFlags, uint uIDNewItem, string lpNewItem);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyMenu(IntPtr hMenu);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint TrackPopupMenu(IntPtr hMenu, uint uFlags, int x, int y, int nReserved, IntPtr hWnd, IntPtr prcRect);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetCursorPos(out Point lpPoint);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr lpIconName);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadImage(IntPtr hInst, string lpszName, uint uType, int cxDesired, int cyDesired, uint fuLoad);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);
    }
}
