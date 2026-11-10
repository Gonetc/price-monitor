using Microsoft.Extensions.Options;
using PriceMonitor.Reporting;
using PriceMonitor.Scraping;

namespace PriceMonitor;

/// <summary>One run = scrape → compare with the previous run → Excel report → save snapshot.</summary>
public class PriceMonitorApp(
    CatalogScraper scraper,
    SnapshotStore snapshots,
    ExcelReportWriter reportWriter,
    IOptions<MonitorOptions> options,
    ILogger<PriceMonitorApp> logger)
{
    public async Task<int> RunAsync(CancellationToken ct)
    {
        var interval = options.Value.IntervalMinutes;
        try
        {
            if (interval <= 0)
            {
                return await RunOnceAsync(ct) ? 0 : 1;
            }

            logger.LogInformation("Monitoring every {Interval} min. Press Ctrl+C to stop.", interval);
            await RunOnceAsync(ct);
            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(interval));
            while (await timer.WaitForNextTickAsync(ct))
            {
                await RunOnceAsync(ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            logger.LogInformation("Stopped.");
        }

        return 0;
    }

    public async Task<bool> RunOnceAsync(CancellationToken ct)
    {
        var started = DateTime.Now;
        try
        {
            var products = await scraper.ScrapeAsync(ct);
            if (products.Count == 0)
            {
                logger.LogWarning("No products found. The site layout may have changed; the report was not created.");
                return false;
            }

            var previous = await snapshots.LoadLatestAsync(ct);
            var changes = PriceComparer.Compare(previous?.Products, products);

            var folder = Path.GetFullPath(options.Value.OutputFolder);
            var reportPath = Path.Combine(folder, $"prices_{started:yyyy-MM-dd_HHmm}.xlsx");
            reportWriter.Write(reportPath, products, changes, started, isFirstRun: previous is null, options.Value.CurrencyFormat);

            await snapshots.SaveAsync(new Snapshot(DateTime.UtcNow, products), ct);

            logger.LogInformation(
                "Done in {Seconds:0.0}s: {Products} products, {Changes} changes. Report: {Path}",
                (DateTime.Now - started).TotalSeconds, products.Count, changes.Count, reportPath);
            return true;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            logger.LogError("The site is unavailable: {Error}", ex.Message);
            return false;
        }
        catch (IOException ex)
        {
            // Typical case: the previous report is open in Excel
            logger.LogError("Could not write the report: {Error}. Is the file open in Excel?", ex.Message);
            return false;
        }
    }
}
