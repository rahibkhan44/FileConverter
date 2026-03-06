using CommunityToolkit.Maui;
using FileConverter.Maui.Converters;
using FileConverter.Maui.Pages;
using FileConverter.Maui.ViewModels;
using FileConverter.Shared;
using Microsoft.Extensions.Logging;

namespace FileConverter.Maui;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // API client
        var apiBaseUrl = Preferences.Get("ApiBaseUrl", "https://localhost:7001");
        builder.Services.AddHttpClient<FileConverterApiClient>(client =>
        {
            client.BaseAddress = new Uri(apiBaseUrl);
            client.Timeout = TimeSpan.FromMinutes(5);
        });

        // ViewModels
        builder.Services.AddTransient<ConvertViewModel>();

        // Pages
        builder.Services.AddTransient<ConvertPage>();
        builder.Services.AddTransient<HistoryPage>();
        builder.Services.AddTransient<SettingsPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
