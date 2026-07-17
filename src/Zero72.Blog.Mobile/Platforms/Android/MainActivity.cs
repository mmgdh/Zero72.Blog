using Android.App;
using Android.Content.PM;
using Android.OS;

namespace Zero72.Blog.Mobile;

/// <summary>
/// Android 启动 Activity，承载 MAUI 应用窗口。
/// </summary>
[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
}
