using PriceMonitor.Reporting;

namespace PriceMonitor.Tests;

public class PriceComparerTests
{
    private static Product P(string id, decimal price, bool inStock = true) =>
        new($"https://shop.test/{id}", $"Book {id}", price, inStock, 3);

    [Fact]
    public void FirstRun_HasNoChanges()
    {
        var changes = PriceComparer.Compare(null, [P("a", 10)]);

        Assert.Empty(changes);
    }

    [Fact]
    public void DetectsPriceUpAndDown_WithPercent()
    {
        var changes = PriceComparer.Compare(
            [P("a", 10), P("b", 20)],
            [P("a", 12), P("b", 15)]);

        var up = Assert.Single(changes, c => c.Kind == ChangeKind.PriceUp);
        Assert.Equal(2m, up.Difference);
        Assert.Equal(20.0m, up.DifferencePercent);

        var down = Assert.Single(changes, c => c.Kind == ChangeKind.PriceDown);
        Assert.Equal(-5m, down.Difference);
        Assert.Equal(-25.0m, down.DifferencePercent);
    }

    [Fact]
    public void DetectsNewAndRemovedProducts()
    {
        var changes = PriceComparer.Compare([P("old", 10)], [P("new", 10)]);

        Assert.Contains(changes, c => c.Kind == ChangeKind.New && c.Url.EndsWith("/new"));
        Assert.Contains(changes, c => c.Kind == ChangeKind.Removed && c.Url.EndsWith("/old"));
    }

    [Fact]
    public void DetectsStockChanges()
    {
        var changes = PriceComparer.Compare(
            [P("a", 10, inStock: true), P("b", 10, inStock: false)],
            [P("a", 10, inStock: false), P("b", 10, inStock: true)]);

        Assert.Contains(changes, c => c.Kind == ChangeKind.OutOfStock && c.Url.EndsWith("/a"));
        Assert.Contains(changes, c => c.Kind == ChangeKind.BackInStock && c.Url.EndsWith("/b"));
    }

    [Fact]
    public void UnchangedProducts_AreNotReported()
    {
        var changes = PriceComparer.Compare([P("a", 10)], [P("a", 10)]);

        Assert.Empty(changes);
    }
}
