namespace NovaMac;

#if !WINDOWS_UWP
public sealed partial class MainWindow : Microsoft.UI.Xaml.Window
{
    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(RootMainPage.TitleBarElement);

        AppWindow.Resize(new Windows.Graphics.SizeInt32(1320, 860));
        SetAppIcon();
        UpdateTitleBarTheme(RootMainPage.ActualTheme);
    }

    private void SetAppIcon()
    {
        try
        {
            string baseDir = AppContext.BaseDirectory;
            string icoPath = System.IO.Path.Combine(baseDir, "app.ico");
            if (!System.IO.File.Exists(icoPath))
            {
                icoPath = System.IO.Path.Combine(baseDir, "Assets", "AppIcon.png");
            }
            if (System.IO.File.Exists(icoPath))
            {
                AppWindow.SetIcon(icoPath);
            }
        }
        catch
        {
            // Ignorar se a plataforma nao suportar SetIcon dinamico
        }
    }

    public void UpdateTitleBarTheme(ElementTheme theme)
    {
        if (!Microsoft.UI.Windowing.AppWindowTitleBar.IsCustomizationSupported())
            return;

        var titleBar = AppWindow.TitleBar;
        titleBar.ExtendsContentIntoTitleBar = true;
        titleBar.ButtonBackgroundColor = Microsoft.UI.Colors.Transparent;
        titleBar.ButtonInactiveBackgroundColor = Microsoft.UI.Colors.Transparent;

        bool isLight = theme == ElementTheme.Light ||
                       (theme == ElementTheme.Default && RootMainPage.ActualTheme == ElementTheme.Light);

        if (isLight)
        {
            titleBar.ButtonForegroundColor = Windows.UI.Color.FromArgb(255, 24, 24, 28);
            titleBar.ButtonHoverForegroundColor = Windows.UI.Color.FromArgb(255, 0, 0, 0);
            titleBar.ButtonHoverBackgroundColor = Windows.UI.Color.FromArgb(22, 0, 0, 0);
            titleBar.ButtonPressedBackgroundColor = Windows.UI.Color.FromArgb(40, 0, 0, 0);
            titleBar.ButtonInactiveForegroundColor = Windows.UI.Color.FromArgb(140, 24, 24, 28);
        }
        else
        {
            titleBar.ButtonForegroundColor = Windows.UI.Color.FromArgb(255, 240, 242, 248);
            titleBar.ButtonHoverForegroundColor = Windows.UI.Color.FromArgb(255, 255, 255, 255);
            titleBar.ButtonHoverBackgroundColor = Windows.UI.Color.FromArgb(28, 255, 255, 255);
            titleBar.ButtonPressedBackgroundColor = Windows.UI.Color.FromArgb(45, 255, 255, 255);
            titleBar.ButtonInactiveForegroundColor = Windows.UI.Color.FromArgb(140, 240, 242, 248);
        }
    }
}
#endif
