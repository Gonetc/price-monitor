namespace PriceMonitor.Reporting;

/// <summary>Finds what changed between two runs.</summary>
public static class PriceComparer
{
    public static IReadOnlyList<PriceChange> Compare(IEnumerable<Product>? previous, IEnumerable<Product> current)
    {
        // First run: nothing to compare with
        if (previous is null)
        {
            return [];
        }

        var before = previous.GroupBy(p => p.Url).ToDictionary(g => g.Key, g => g.First());
        var now = current.GroupBy(p => p.Url).ToDictionary(g => g.Key, g => g.First());
        var changes = new List<PriceChange>();

        foreach (var (url, product) in now)
        {
            if (!before.TryGetValue(url, out var old))
            {
                changes.Add(new PriceChange(ChangeKind.New, product.Title, url, null, product.Price));
                continue;
            }

            if (product.Price > old.Price)
            {
                changes.Add(new PriceChange(ChangeKind.PriceUp, product.Title, url, old.Price, product.Price));
            }
            else if (product.Price < old.Price)
            {
                changes.Add(new PriceChange(ChangeKind.PriceDown, product.Title, url, old.Price, product.Price));
            }

            if (product.InStock != old.InStock)
            {
                var kind = product.InStock ? ChangeKind.BackInStock : ChangeKind.OutOfStock;
                changes.Add(new PriceChange(kind, product.Title, url, old.Price, product.Price));
            }
        }

        foreach (var (url, old) in before)
        {
            if (!now.ContainsKey(url))
            {
                changes.Add(new PriceChange(ChangeKind.Removed, old.Title, url, old.Price, null));
            }
        }

        return changes
            .OrderBy(c => c.Kind)
            .ThenByDescending(c => Math.Abs(c.DifferencePercent ?? 0))
            .ThenBy(c => c.Title)
            .ToList();
    }
}
