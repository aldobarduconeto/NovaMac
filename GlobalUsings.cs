#if WINDOWS_UWP
global using UIWindow = Windows.UI.Xaml.Window;
global using UIApplication = Windows.UI.Xaml.Application;
global using UIElement = Windows.UI.Xaml.UIElement;
global using FrameworkElement = Windows.UI.Xaml.FrameworkElement;
global using RoutedEventArgs = Windows.UI.Xaml.RoutedEventArgs;
global using TappedRoutedEventArgs = Windows.UI.Xaml.Input.TappedRoutedEventArgs;
global using Visibility = Windows.UI.Xaml.Visibility;
global using ElementTheme = Windows.UI.Xaml.ElementTheme;
global using Windows.UI.Xaml;
global using Windows.UI.Xaml.Controls;
global using Windows.UI.Xaml.Media;
#else
global using UIWindow = Microsoft.UI.Xaml.Window;
global using UIApplication = Microsoft.UI.Xaml.Application;
global using UIElement = Microsoft.UI.Xaml.UIElement;
global using FrameworkElement = Microsoft.UI.Xaml.FrameworkElement;
global using RoutedEventArgs = Microsoft.UI.Xaml.RoutedEventArgs;
global using TappedRoutedEventArgs = Microsoft.UI.Xaml.Input.TappedRoutedEventArgs;
global using Visibility = Microsoft.UI.Xaml.Visibility;
global using ElementTheme = Microsoft.UI.Xaml.ElementTheme;
global using Microsoft.UI.Xaml;
global using Microsoft.UI.Xaml.Controls;
global using Microsoft.UI.Xaml.Media;
#endif
