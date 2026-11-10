namespace PriceMonitor;

/// <summary>One product as seen in the catalog. <see cref="Url"/> is the unique key.</summary>
public record Product(string Url, string Title, decimal Price, bool InStock, int Rating);

/// <summary>All products captured in one run; stored to compare with the next run.</summary>
public record Snapshot(DateTime CapturedAtUtc, IReadOnlyList<Product> Products);

public enum ChangeKind
{
    New,
    Removed,
    PriceUp,
    PriceDown,
    BackInStock,
    OutOfStock
}

public record PriceChange(ChangeKind Kind, string Title, string Url, decimal? OldPrice, decimal? NewPrice)
{
    public decimal? Difference => OldPrice is not null && NewPrice is not null ? NewPrice - OldPrice : null;

    public decimal? DifferencePercent =>
        OldPrice is > 0 && NewPrice is not null ? Math.Round((NewPrice.Value - OldPrice.Value) / OldPrice.Value * 100, 1) : null;
}
