using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace CopyOpsSuite.App.WinUI
{
    public partial class App : Application
    {
        public static AppServices Services { get; } = new();
        public static Window? MainWindowInstance { get; private set; }
        public static ElementTheme CurrentTheme { get; private set; } = ElementTheme.Default;

        private Window? m_window;

        public App()
        {
            InitializeComponent();
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            MainWindowInstance ??= new MainWindow();
            m_window = MainWindowInstance;
            ApplyThemeFromSettings();
            Services.Start();
            MainWindowInstance.Activate();
        }

        public static void ApplyTheme(ElementTheme theme)
        {
            CurrentTheme = theme;

            if (Current is App app)
            {
                if (app.m_window?.Content is FrameworkElement root)
                {
                    var queue = root.DispatcherQueue ?? DispatcherQueue.GetForCurrentThread();
                    if (queue != null)
                    {
                        queue.TryEnqueue(() => root.RequestedTheme = theme);
                    }
                    else
                    {
                        root.RequestedTheme = theme;
                    }
                }
            }
        }

        private void ApplyThemeFromSettings()
        {
            var themeSetting = Services.SettingsService.GetAppTheme();

            var theme = themeSetting switch
            {
                "Light" => ElementTheme.Light,
                "Dark" => ElementTheme.Dark,
                _ => ElementTheme.Default
            };

            ApplyTheme(theme);
        }
    }
}
