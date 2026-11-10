using System.ComponentModel.DataAnnotations;

namespace PriceMonitor;

public class ScraperOptions
{
    public const string SectionName = "Scraper";

    [Required, Url]
    public string BaseUrl { get; set; } = "https://books.toscrape.com/";

    [Required]
    public string StartPath { get; set; } = "catalogue/page-1.html";

    /// <summary>Safety limit on the number of catalog pages to visit.</summary>
    [Range(1, 1000)]
    public int MaxPages { get; set; } = 50;

    /// <summary>Politeness delay so the target site is not overloaded.</summary>
    [Range(0, 60_000)]
    public int DelayBetweenRequestsMs { get; set; } = 500;

    [Required]
    public string UserAgent { get; set; } = "PriceMonitor/1.0";
}

public class MonitorOptions
{
    public const string SectionName = "Monitor";

    [Required]
    public string OutputFolder { get; set; } = "output";

    /// <summary>Excel number format for prices.</summary>
    public string CurrencyFormat { get; set; } = "#,##0.00";

    /// <summary>0 = run once and exit; otherwise repeat every N minutes until Ctrl+C.</summary>
    [Range(0, 7 * 24 * 60)]
    public int IntervalMinutes { get; set; }
}
