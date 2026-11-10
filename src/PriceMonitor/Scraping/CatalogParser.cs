using System.Globalization;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace PriceMonitor.Scraping;

public record CatalogPage(IReadOnlyList<Product> Products, Uri? NextPageUrl);

/// <summary>
/// Extracts products from one catalog page. Pure function of the HTML, so it is unit-tested
/// against a saved page and never touches the network.
/// </summary>
public static class CatalogParser
{
    private static readonly HtmlParser Parser = new();

    private static readonly Dictionary<string, int> RatingWords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["One"] = 1, ["Two"] = 2, ["Three"] = 3, ["Four"] = 4, ["Five"] = 5
    };

    public static CatalogPage Parse(string html, Uri pageUrl)
    {
        using var document = Parser.ParseDocument(html);
        var products = new List<Product>();

        foreach (var card in document.QuerySelectorAll("article.product_pod"))
        {
            var product = ParseCard(card, pageUrl);
            if (product is not null)
            {
                products.Add(product);
            }
        }

        var nextHref = document.QuerySelector("li.next a")?.GetAttribute("href");
        var nextPage = string.IsNullOrWhiteSpace(nextHref) ? null : new Uri(pageUrl, nextHref);

        return new CatalogPage(products, nextPage);
    }

    /// <summary>Parses prices like "£51.77" (also tolerates broken encodings such as "Â£51.77").</summary>
    public static decimal? ParsePrice(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var digits = new string(text.Where(c => char.IsDigit(c) || c == '.').ToArray());
        return decimal.TryParse(digits, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var price)
            ? price
            : null;
    }

    private static Product? ParseCard(IElement card, Uri pageUrl)
    {
        var link = card.QuerySelector("h3 a");
        var href = link?.GetAttribute("href");
        var title = link?.GetAttribute("title") ?? link?.TextContent.Trim();
        var price = ParsePrice(card.QuerySelector(".price_color")?.TextContent);

        // A card without these essentials is skipped rather than saved half-empty
        if (string.IsNullOrWhiteSpace(href) || string.IsNullOrWhiteSpace(title) || price is null)
        {
            return null;
        }

        var availability = card.QuerySelector(".availability")?.TextContent ?? "";
        var inStock = availability.Contains("In stock", StringComparison.OrdinalIgnoreCase);

        var ratingWord = card.QuerySelector(".star-rating")?.ClassList.FirstOrDefault(c => c != "star-rating");
        var rating = ratingWord is not null && RatingWords.TryGetValue(ratingWord, out var stars) ? stars : 0;

        return new Product(new Uri(pageUrl, href).ToString(), title.Trim(), price.Value, inStock, rating);
    }
}
