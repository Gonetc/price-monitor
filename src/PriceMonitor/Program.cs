using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using PriceMonitor;
using PriceMonitor.Reporting;
using PriceMonitor.Scraping;

// Short command-line switches, e.g.: --pages 3 --interval 60 --out reports
var switches = new Dictionary<string, string>
{
    ["--pages"] = "Scraper:MaxPages",
    ["--delay"] = "Scraper:DelayBetweenRequestsMs",
    ["--interval"] = "Monitor:IntervalMinutes",
    ["--out"] = "Monitor:OutputFolder"
};

var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    // appsettings.json is read from the app folder, wherever the tool is started from
    ContentRootPath = AppContext.BaseDirectory
});
builder.Configuration.AddCommandLine(args, switches);

builder.Services.AddOptions<ScraperOptions>()
    .Bind(builder.Configuration.GetSection(ScraperOptions.SectionName))
    .ValidateDataAnnotations();
builder.Services.AddOptions<MonitorOptions>()
    .Bind(builder.Configuration.GetSection(MonitorOptions.SectionName))
    .ValidateDataAnnotations();

builder.Services.AddTransient<RetryHandler>();
builder.Services.AddHttpClient<CatalogScraper>((sp, client) =>
    {
        var settings = sp.GetRequiredService<IOptions<ScraperOptions>>().Value;
        client.DefaultRequestHeaders.UserAgent.ParseAdd(settings.UserAgent);
        client.Timeout = TimeSpan.FromSeconds(30);
    })
    .AddHttpMessageHandler<RetryHandler>();

builder.Services.AddSingleton<SnapshotStore>();
builder.Services.AddSingleton<ExcelReportWriter>();
builder.Services.AddSingleton<PriceMonitorApp>();

using var host = builder.Build();

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

try
{
    var app = host.Services.GetRequiredService<PriceMonitorApp>();
    return await app.RunAsync(cts.Token);
}
catch (OptionsValidationException ex)
{
    Console.Error.WriteLine("Invalid configuration: " + string.Join("; ", ex.Failures));
    return 2;
}
