using Microsoft.Extensions.Logging;
using System.Net.Http;

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
                fonts.AddFont("MaterialSymbolsRounded.ttf", "MaterialSymbols");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        builder.Services.AddSingleton<LinkedLamp.Services.LinkedLampBLEService>();
        builder.Services.AddSingleton<LinkedLamp.Services.AppState>();
        builder.Services.AddSingleton<HttpClient>();
        builder.Services.AddSingleton<LinkedLamp.Services.BackendClient>();

        builder.Services.AddTransient<LinkedLamp.Pages.RegisterPage>();
        builder.Services.AddTransient<LinkedLamp.Pages.LoginPage>();
        builder.Services.AddTransient<LinkedLamp.Pages.ManageGroupPage>();
        builder.Services.AddTransient<LinkedLamp.Pages.ManageGroupsPage>();
        builder.Services.AddTransient<LinkedLamp.Pages.HomePage>();
        builder.Services.AddTransient<LinkedLamp.Pages.ScanPage>();

        return builder.Build();
    }
}
