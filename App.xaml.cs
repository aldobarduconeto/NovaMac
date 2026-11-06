using NovaMac.Services;

namespace NovaMac;

public partial class App : UIApplication
{
    public static MainWindow? CurrentWindow { get; private set; }
    public static AppSettingsService Settings { get; } = new();

    public App()
    {
        InitializeComponent();
        this.UnhandledException += (s, e) =>
        {
            LogApplicationFailure(e.Exception);
        };
    }

#if WINDOWS_UWP
    protected override void OnLaunched(Windows.ApplicationModel.Activation.LaunchActivatedEventArgs args)
    {
        if (Windows.UI.Xaml.Window.Current.Content is not MainPage)
        {
            var page = new MainPage();
            Windows.UI.Xaml.Window.Current.Content = page;
        }
        ApplyTheme(Settings.Current.Theme);
        Windows.UI.Xaml.Window.Current.Activate();
    }
#else
    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        try
        {
            NovaMac.Models.VirtualMachineConfig.UiDispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
            CurrentWindow = new MainWindow();
            ApplyBackdrop(Settings.Current.BackdropType);
            ApplyTheme(Settings.Current.Theme);
            CurrentWindow.Activate();
        }
        catch (Exception exception)
        {
            LogApplicationFailure(exception);
            throw;
        }
    }
#endif

    private static void LogApplicationFailure(Exception exception)
    {
        System.Diagnostics.Debug.WriteLine(exception);
        try
        {
            string directory = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NovaMac");
            System.IO.Directory.CreateDirectory(directory);
            System.IO.File.AppendAllText(System.IO.Path.Combine(directory, "app-error.log"),
                $"{DateTimeOffset.Now:O} {exception}{Environment.NewLine}");
        }
        catch (System.IO.IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    public static void ApplyTheme(string themeName)
    {
        var theme = themeName switch
        {
            "Light" => ElementTheme.Light,
            "Dark" => ElementTheme.Dark,
            _ => ElementTheme.Default
        };

#if WINDOWS_UWP
        if (Windows.UI.Xaml.Window.Current.Content is FrameworkElement root)
        {
            root.RequestedTheme = theme;
        }
#else
        if (CurrentWindow?.Content is FrameworkElement root)
        {
            root.RequestedTheme = theme;
            CurrentWindow.UpdateTitleBarTheme(theme);
        }
#endif
    }

    public static void ApplyBackdrop(string backdropName)
    {
#if !WINDOWS_UWP
        if (CurrentWindow == null)
            return;

        CurrentWindow.SystemBackdrop = backdropName switch
        {
            "Mica" => new Microsoft.UI.Xaml.Media.MicaBackdrop
            {
                Kind = Microsoft.UI.Composition.SystemBackdrops.MicaKind.Base
            },
            "Acrylic" => new Microsoft.UI.Xaml.Media.DesktopAcrylicBackdrop(),
            _ => new Microsoft.UI.Xaml.Media.MicaBackdrop
            {
                Kind = Microsoft.UI.Composition.SystemBackdrops.MicaKind.BaseAlt
            }
        };
#endif
    }
}
