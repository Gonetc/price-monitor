using Microsoft.Extensions.Options;

namespace PriceMonitor.Scraping;

/// <summary>Walks the catalog page by page following the "next" link.</summary>
public class CatalogScraper(HttpClient http, IOptions<ScraperOptions> options, ILogger<CatalogScraper> logger)
{
    public async Task<IReadOnlyList<Product>> ScrapeAsync(CancellationToken ct)
    {
        var settings = options.Value;
        var products = new List<Product>();
        Uri? pageUrl = new(new Uri(settings.BaseUrl), settings.StartPath);
        var pageNumber = 0;

        while (pageUrl is not null && pageNumber < settings.MaxPages)
        {
            pageNumber++;
            var html = await http.GetStringAsync(pageUrl, ct);
            var page = CatalogParser.Parse(html, pageUrl);

            products.AddRange(page.Products);
            logger.LogInformation("Page {Page}: {Count} products (total {Total})",
                pageNumber, page.Products.Count, products.Count);

            pageUrl = page.NextPageUrl;
            if (pageUrl is not null && settings.DelayBetweenRequestsMs > 0)
            {
                await Task.Delay(settings.DelayBetweenRequestsMs, ct);
            }
        }

        if (pageUrl is not null)
        {
            logger.LogInformation("Stopped after {MaxPages} pages (Scraper:MaxPages limit)", settings.MaxPages);
        }

        return products.DistinctBy(p => p.Url).ToList();
    }
}
