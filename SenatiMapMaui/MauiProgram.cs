using SenatiMapMaui.Services;
#if ANDROID
using SenatiMapMaui.Platforms.Android;
#endif

namespace SenatiMapMaui;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>().ConfigureFonts(_ => { });
        builder.Services.AddSingleton<ILocationTrackingService, MauiLocationTrackingService>();
#if ANDROID
        builder.Services.AddSingleton<IBleScannerService, AndroidBleScannerService>();
#endif
        builder.Services.AddSingleton<MainPage>();
        return builder.Build();
    }
}
