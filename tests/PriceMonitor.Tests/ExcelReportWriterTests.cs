using ClosedXML.Excel;
using PriceMonitor.Reporting;

namespace PriceMonitor.Tests;

public class ExcelReportWriterTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"price-report-{Guid.NewGuid()}.xlsx");

    public void Dispose()
    {
        if (File.Exists(_path))
        {
            File.Delete(_path);
        }
    }

    [Fact]
    public void WritesAllSheets_WithProductsAndChanges()
    {
        Product[] products =
        [
            new("https://shop.test/a", "Book A", 12m, true, 4),
            new("https://shop.test/b", "Book B", 15m, false, 2)
        ];
        PriceChange[] changes = [new(ChangeKind.PriceUp, "Book A", "https://shop.test/a", 10m, 12m)];

        new ExcelReportWriter().Write(_path, products, changes, new DateTime(2030, 1, 7, 9, 0, 0), isFirstRun: false, "£#,##0.00");

        using var workbook = new XLWorkbook(_path);
        Assert.Equal(new[] { "Summary", "Products", "Changes" }, workbook.Worksheets.Select(w => w.Name));

        var productsSheet = workbook.Worksheet("Products");
        Assert.Equal("Book A", productsSheet.Cell(2, 1).GetString());
        Assert.Equal(12d, productsSheet.Cell(2, 2).GetDouble());
        Assert.Equal("★★★★☆", productsSheet.Cell(2, 5).GetString());
        Assert.Equal("+20.0%", productsSheet.Cell(2, 6).GetString());

        var changesSheet = workbook.Worksheet("Changes");
        Assert.Equal("Price up", changesSheet.Cell(2, 1).GetString());
    }

    [Fact]
    public void FirstRun_ExplainsThatThereIsNothingToCompare()
    {
        Product[] products = [new("https://shop.test/a", "Book A", 12m, true, 4)];

        new ExcelReportWriter().Write(_path, products, [], DateTime.Now, isFirstRun: true, "#,##0.00");

        using var workbook = new XLWorkbook(_path);
        Assert.StartsWith("First run", workbook.Worksheet("Changes").Cell(2, 1).GetString());
    }
}
