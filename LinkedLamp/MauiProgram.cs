using Microsoft.Extensions.Logging;

namespace LinkedLamp;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        builder.Services.AddSingleton<LinkedLamp.Services.EspBleProvisioningService>();

        builder.Services.AddTransient<LinkedLamp.Pages.HomePage>();
        builder.Services.AddTransient<LinkedLamp.Pages.WifiSsidPage>();
        builder.Services.AddTransient<LinkedLamp.Pages.WifiPassPage>();
        builder.Services.AddTransient<LinkedLamp.Pages.GroupPage>();
        builder.Services.AddTransient<LinkedLamp.Pages.BluetoothMode>();
        builder.Services.AddTransient<LinkedLamp.Pages.ScanPage>();
        builder.Services.AddTransient<LinkedLamp.Pages.SendConfigPage>();

        return builder.Build();
    }
}
