using Android.App;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.View;

namespace InvestProMax.Maui;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode
        | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        if (Window is not null)
        {
            var corBarraSistema = global::Android.Graphics.Color.ParseColor("#050E09");
            Window.DecorView.SetBackgroundColor(corBarraSistema);

#pragma warning disable CA1422
            Window.SetStatusBarColor(corBarraSistema);
            Window.SetNavigationBarColor(corBarraSistema);
#pragma warning restore CA1422

            var insetsController = WindowCompat.GetInsetsController(Window, Window.DecorView);
            if (insetsController is not null)
            {
                insetsController.AppearanceLightStatusBars = false;
                insetsController.AppearanceLightNavigationBars = false;
            }

            var contentView = FindViewById(global::Android.Resource.Id.Content);
            if (contentView is not null)
            {
                contentView.SetBackgroundColor(corBarraSistema);
                ViewCompat.SetOnApplyWindowInsetsListener(contentView, new SystemBarsInsetsListener());
                ViewCompat.RequestApplyInsets(contentView);
            }
        }
    }

    private sealed class SystemBarsInsetsListener : Java.Lang.Object, IOnApplyWindowInsetsListener
    {
        public WindowInsetsCompat OnApplyWindowInsets(global::Android.Views.View? v, WindowInsetsCompat? insets)
        {
            if (v is null || insets is null)
            {
                return insets ?? WindowInsetsCompat.Consumed!;
            }

            var barras = insets.GetInsets(
                WindowInsetsCompat.Type.SystemBars()
                | WindowInsetsCompat.Type.DisplayCutout()
                | WindowInsetsCompat.Type.Ime());

            if (barras is not null)
            {
                v.SetPadding(barras.Left, barras.Top, barras.Right, barras.Bottom);
            }

            return WindowInsetsCompat.Consumed!;
        }
    }
}
