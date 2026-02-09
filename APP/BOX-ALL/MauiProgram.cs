using Microsoft.Extensions.Logging;
using BOX_ALL.Services;
using BOX_ALL.ViewModels;
using BOX_ALL.Views;

namespace BOX_ALL
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("Ubuntu_Regular.ttf", "UbuntuRegular");
                    fonts.AddFont("UbuntuCondensed_Regular.ttf", "UbuntuCondensed");
                    fonts.AddFont("UbuntuMono_Regular.ttf", "UbuntuMono");
                    fonts.AddFont("STENCIL.TTF", "StencilFont");
                });

            // Register Services
            builder.Services.AddSingleton<FileService>();
            builder.Services.AddSingleton<BoxRegistryService>();
            builder.Services.AddSingleton<BoxDataService>();
            builder.Services.AddSingleton<ExportImportService>();
            builder.Services.AddSingleton<ImportLogService>();
            builder.Services.AddTransient<BulkCsvParserService>();
            builder.Services.AddTransient<SyncExportService>();

            // Register platform-specific folder picker service
#if ANDROID
            builder.Services.AddSingleton<IFolderPickerService, Platforms.Android.FolderPickerService>();
#endif

            // Register ViewModels
            builder.Services.AddTransient<DashboardViewModel>();
            builder.Services.AddTransient<BoxViewViewModel>();
            builder.Services.AddTransient<AddComponentViewModel>();
            builder.Services.AddTransient<AddEditBoxViewModel>();
            builder.Services.AddTransient<BulkImportViewModel>();
            builder.Services.AddTransient<ComponentDetailsViewModel>();
            builder.Services.AddTransient<ExportSelectViewModel>();
            builder.Services.AddTransient<ImportSelectViewModel>();

            // Register Pages
            builder.Services.AddTransient<DashboardPage>();
            builder.Services.AddTransient<BoxViewPage>();
            builder.Services.AddTransient<AddComponentPage>();
            builder.Services.AddTransient<AddEditBoxPage>();
            builder.Services.AddTransient<BulkImportPage>();
            builder.Services.AddTransient<ComponentDetailsPage>();
            builder.Services.AddTransient<ExportSelectPage>();
            builder.Services.AddTransient<ImportSelectPage>();

#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
