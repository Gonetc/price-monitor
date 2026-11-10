using PriceMonitor.Scraping;

namespace PriceMonitor.Tests;

public class CatalogParserTests
{
    private static readonly Uri PageUrl = new("https://books.toscrape.com/catalogue/page-1.html");

    // Trimmed copy of a real catalog page: two valid cards, one broken card, pagination
    private const string Html = """
        <html><body>
        <ol class="row">
          <li><article class="product_pod">
            <p class="star-rating Three"></p>
            <h3><a href="a-light-in-the-attic_1000/index.html" title="A Light in the Attic">A Light in the ...</a></h3>
            <div class="product_price">
              <p class="price_color">£51.77</p>
              <p class="instock availability"><i class="icon-ok"></i> In stock</p>
            </div>
          </article></li>
          <li><article class="product_pod">
            <p class="star-rating One"></p>
            <h3><a href="tipping-the-velvet_999/index.html" title="Tipping the Velvet">Tipping the Velvet</a></h3>
            <div class="product_price">
              <p class="price_color">Â£53.74</p>
              <p class="availability">Out of stock</p>
            </div>
          </article></li>
          <li><article class="product_pod">
            <h3><a href="broken_1/index.html" title="Broken card without price">Broken</a></h3>
          </article></li>
        </ol>
        <ul class="pager"><li class="current">Page 1 of 50</li><li class="next"><a href="page-2.html">next</a></li></ul>
        </body></html>
        """;

    [Fact]
    public void ParsesProductCards()
    {
        var page = CatalogParser.Parse(Html, PageUrl);

        Assert.Equal(2, page.Products.Count);

        var first = page.Products[0];
        Assert.Equal("A Light in the Attic", first.Title); // full title from the attribute, not the shortened text
        Assert.Equal(51.77m, first.Price);
        Assert.True(first.InStock);
        Assert.Equal(3, first.Rating);
        Assert.Equal("https://books.toscrape.com/catalogue/a-light-in-the-attic_1000/index.html", first.Url);

        var second = page.Products[1];
        Assert.Equal(53.74m, second.Price);   // survives the "Â£" encoding glitch
        Assert.False(second.InStock);
        Assert.Equal(1, second.Rating);
    }

    [Fact]
    public void FindsNextPage_AsAbsoluteUrl()
    {
        var page = CatalogParser.Parse(Html, PageUrl);

        Assert.Equal(new Uri("https://books.toscrape.com/catalogue/page-2.html"), page.NextPageUrl);
    }

    [Fact]
    public void LastPage_HasNoNextPage()
    {
        var page = CatalogParser.Parse("<html><body><ul class='pager'></ul></body></html>", PageUrl);

        Assert.Empty(page.Products);
        Assert.Null(page.NextPageUrl);
    }

    [Theory]
    [InlineData("£51.77", 51.77)]
    [InlineData("Â£10.00", 10.00)]
    [InlineData(" 7.5 ", 7.5)]
    public void ParsePrice_HandlesCurrencySymbols(string text, double expected)
    {
        Assert.Equal((decimal)expected, CatalogParser.ParsePrice(text));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("free")]
    public void ParsePrice_ReturnsNullForGarbage(string? text)
    {
        Assert.Null(CatalogParser.ParsePrice(text));
    }
}
