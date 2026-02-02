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

        builder.Services.AddSingleton<LinkedLamp.Services.LinkedLampBLEService>();

        builder.Services.AddTransient<LinkedLamp.Pages.HomePage>();
        builder.Services.AddTransient<LinkedLamp.Pages.ScanPage>();

        return builder.Build();
    }
}
