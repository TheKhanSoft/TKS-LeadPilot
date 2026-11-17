using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TKS.LeadPilot.Core.Interfaces;
using TKS.LeadPilot.Core.Services;
using TKS.LeadPilot.Data;
using TKS.LeadPilot.Data.Repositories;
using TKS.LeadPilot.Data.Services;
using TKS.LeadPilot.UI.Services;
using TKS.LeadPilot.UI.ViewModels;
using TKS.LeadPilot.UI.Views;

namespace TKS.LeadPilot.UI;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Configure Dependency Injection Services
        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();

        // Run database initialization and seeding before showing UI
        using (var scope = Services.CreateScope())
        {
            var initializer = scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>();
            initializer.InitializeAsync().GetAwaiter().GetResult();
        }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainVm = Services.GetRequiredService<MainViewModel>();
            desktop.MainWindow = new MainWindow
            {
                DataContext = mainVm
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Database
        var appDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TKS.LeadPilot");

        if (!Directory.Exists(appDataDir))
        {
            Directory.CreateDirectory(appDataDir);
        }

        var dbPath = Path.Combine(appDataDir, "tks_leadpilot.db");
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite($"Data Source={dbPath}"));

        // Repositories & Initializer
        services.AddScoped<IBusinessRepository, BusinessRepository>();
        services.AddScoped<IPipelineStageRepository, PipelineStageRepository>();
        services.AddScoped<IAuditRepository, AuditRepository>();
        services.AddScoped<IDatabaseInitializer, DatabaseInitializer>();

        // Core Business Services (Stage 4, 5, 6, 7)
        services.AddSingleton(new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(60) });
        services.AddScoped<IDeduplicationService, DeduplicationService>();
        services.AddScoped<ICsvImporterService, CsvImporterService>();
        services.AddScoped<IDiscoveryService, OverpassDiscoveryService>();
        services.AddScoped<ISsrfValidator, SsrfValidator>();
        services.AddScoped<IWebsiteAuditEngine, WebsiteAuditEngine>();
        services.AddScoped<IGooglePlacesEnrichmentService, GooglePlacesEnrichmentService>();
        services.AddScoped<IAiProposalSynthesizer, AiProposalSynthesizer>();
        services.AddScoped<IAuditComparisonService, AuditComparisonService>();
        services.AddScoped<ILeadScoringEngine, LeadScoringEngine>();
        services.AddScoped<LeadScoringEngine.ILeadScoreStore, EfLeadScoreStore>();
        services.AddScoped<ITksServiceMatcher, TksServiceMatcher>();
        services.AddScoped<ICrmService, CrmService>();

        // Secret Vault
        services.AddSingleton<ISecretVault, DpapiSecretVault>();

        // ViewModels
        services.AddTransient<MainViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<BusinessesViewModel>();
        services.AddTransient<AuditsViewModel>();
        services.AddTransient<PipelineViewModel>();
        services.AddTransient<SettingsViewModel>();
    }
}
