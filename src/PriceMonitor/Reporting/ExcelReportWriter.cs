using ClosedXML.Excel;

namespace PriceMonitor.Reporting;

/// <summary>
/// Builds a formatted Excel report: Summary, Products (with changed rows highlighted) and Changes.
/// </summary>
public class ExcelReportWriter
{
    private static readonly XLColor PriceUpFill = XLColor.FromHtml("#FDECEC");
    private static readonly XLColor PriceDownFill = XLColor.FromHtml("#E3F4E8");
    private static readonly XLColor NewFill = XLColor.FromHtml("#E3ECF8");
    private static readonly XLColor HeaderFill = XLColor.FromHtml("#1F1C1A");

    public void Write(
        string path,
        IReadOnlyList<Product> products,
        IReadOnlyList<PriceChange> changes,
        DateTime capturedAtLocal,
        bool isFirstRun,
        string currencyFormat)
    {
        using var workbook = new XLWorkbook();

        WriteSummary(workbook.Worksheets.Add("Summary"), products, changes, capturedAtLocal, isFirstRun, currencyFormat);
        WriteProducts(workbook.Worksheets.Add("Products"), products, changes, currencyFormat);
        WriteChanges(workbook.Worksheets.Add("Changes"), changes, isFirstRun, currencyFormat);

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        workbook.SaveAs(path);
    }

    private static void WriteSummary(
        IXLWorksheet sheet,
        IReadOnlyList<Product> products,
        IReadOnlyList<PriceChange> changes,
        DateTime capturedAtLocal,
        bool isFirstRun,
        string currencyFormat)
    {
        sheet.Cell(1, 1).Value = "Price monitor report";
        sheet.Cell(1, 1).Style.Font.SetBold().Font.SetFontSize(16);

        var rows = new List<(string Label, XLCellValue Value, string? Format)>
        {
            ("Captured at", capturedAtLocal, "yyyy-mm-dd hh:mm"),
            ("Products", products.Count, null),
            ("In stock", products.Count(p => p.InStock), null),
            ("Average price", products.Count == 0 ? 0 : (double)products.Average(p => p.Price), currencyFormat),
            ("Cheapest", products.Count == 0 ? 0 : (double)products.Min(p => p.Price), currencyFormat),
            ("Most expensive", products.Count == 0 ? 0 : (double)products.Max(p => p.Price), currencyFormat)
        };

        if (isFirstRun)
        {
            rows.Add(("Changes", "First run: changes will appear after the next run", null));
        }
        else
        {
            foreach (var kind in Enum.GetValues<ChangeKind>())
            {
                rows.Add((Describe(kind), changes.Count(c => c.Kind == kind), null));
            }
        }

        var row = 3;
        foreach (var (label, value, format) in rows)
        {
            sheet.Cell(row, 1).Value = label;
            sheet.Cell(row, 1).Style.Font.SetFontColor(XLColor.FromHtml("#6B6B70"));
            sheet.Cell(row, 2).Value = value;
            sheet.Cell(row, 2).Style.Font.SetBold();
            sheet.Cell(row, 2).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Left);
            if (format is not null)
            {
                sheet.Cell(row, 2).Style.NumberFormat.Format = format;
            }

            row++;
        }

        sheet.Column(1).Width = 22;
        sheet.Column(2).Width = 50;
    }

    private static void WriteProducts(
        IXLWorksheet sheet, IReadOnlyList<Product> products, IReadOnlyList<PriceChange> changes, string currencyFormat)
    {
        string[] headers = ["Title", "Price", "In stock", "Rating", "Stars", "Change", "URL"];
        WriteHeader(sheet, headers);

        var changeByUrl = changes
            .Where(c => c.Kind is ChangeKind.PriceUp or ChangeKind.PriceDown or ChangeKind.New)
            .GroupBy(c => c.Url)
            .ToDictionary(g => g.Key, g => g.First());

        var row = 2;
        foreach (var product in products.OrderBy(p => p.Title))
        {
            sheet.Cell(row, 1).Value = product.Title;
            sheet.Cell(row, 2).Value = (double)product.Price;
            sheet.Cell(row, 2).Style.NumberFormat.Format = currencyFormat;
            sheet.Cell(row, 3).Value = product.InStock ? "Yes" : "No";
            sheet.Cell(row, 4).Value = product.Rating;
            sheet.Cell(row, 5).Value = new string('★', product.Rating) + new string('☆', 5 - product.Rating);
            sheet.Cell(row, 7).Value = product.Url;
            sheet.Cell(row, 7).SetHyperlink(new XLHyperlink(product.Url));

            if (changeByUrl.TryGetValue(product.Url, out var change))
            {
                sheet.Cell(row, 6).Value = change.Kind == ChangeKind.New ? "New" : $"{change.DifferencePercent:+0.0;-0.0}%";
                sheet.Range(row, 1, row, headers.Length).Style.Fill.BackgroundColor = change.Kind switch
                {
                    ChangeKind.PriceUp => PriceUpFill,
                    ChangeKind.PriceDown => PriceDownFill,
                    _ => NewFill
                };
            }

            row++;
        }

        FinishTable(sheet, headers.Length, row - 1, maxWidth: 70);
    }

    private static void WriteChanges(IXLWorksheet sheet, IReadOnlyList<PriceChange> changes, bool isFirstRun, string currencyFormat)
    {
        string[] headers = ["Change", "Title", "Old price", "New price", "Difference", "Difference, %", "URL"];
        WriteHeader(sheet, headers);

        if (changes.Count == 0)
        {
            sheet.Cell(2, 1).Value = isFirstRun
                ? "First run: there is nothing to compare with yet."
                : "No changes since the previous run.";
            sheet.Columns().AdjustToContents();
            return;
        }

        var row = 2;
        foreach (var change in changes)
        {
            sheet.Cell(row, 1).Value = Describe(change.Kind);
            sheet.Cell(row, 2).Value = change.Title;
            SetMoney(sheet.Cell(row, 3), change.OldPrice, currencyFormat);
            SetMoney(sheet.Cell(row, 4), change.NewPrice, currencyFormat);
            SetMoney(sheet.Cell(row, 5), change.Difference, currencyFormat);
            if (change.DifferencePercent is { } percent)
            {
                sheet.Cell(row, 6).Value = (double)percent / 100;
                sheet.Cell(row, 6).Style.NumberFormat.Format = "+0.0%;-0.0%";
            }

            sheet.Cell(row, 7).Value = change.Url;
            sheet.Cell(row, 7).SetHyperlink(new XLHyperlink(change.Url));

            var fill = change.Kind switch
            {
                ChangeKind.PriceUp or ChangeKind.OutOfStock or ChangeKind.Removed => PriceUpFill,
                ChangeKind.PriceDown or ChangeKind.BackInStock => PriceDownFill,
                _ => NewFill
            };
            sheet.Cell(row, 1).Style.Fill.BackgroundColor = fill;
            row++;
        }

        FinishTable(sheet, headers.Length, row - 1, maxWidth: 70);
    }

    private static void WriteHeader(IXLWorksheet sheet, string[] headers)
    {
        for (var i = 0; i < headers.Length; i++)
        {
            sheet.Cell(1, i + 1).Value = headers[i];
        }

        var header = sheet.Range(1, 1, 1, headers.Length);
        header.Style.Font.SetBold().Font.SetFontColor(XLColor.White);
        header.Style.Fill.BackgroundColor = HeaderFill;
        sheet.SheetView.FreezeRows(1);
    }

    private static void FinishTable(IXLWorksheet sheet, int columns, int lastRow, double maxWidth)
    {
        sheet.Range(1, 1, lastRow, columns).SetAutoFilter();
        sheet.Columns(1, columns).AdjustToContents();
        foreach (var column in sheet.Columns(1, columns))
        {
            if (column.Width > maxWidth)
            {
                column.Width = maxWidth;
            }
        }
    }

    private static void SetMoney(IXLCell cell, decimal? value, string currencyFormat)
    {
        if (value is null)
        {
            return;
        }

        cell.Value = (double)value.Value;
        cell.Style.NumberFormat.Format = currencyFormat;
    }

    public static string Describe(ChangeKind kind) => kind switch
    {
        ChangeKind.New => "New product",
        ChangeKind.Removed => "Removed",
        ChangeKind.PriceUp => "Price up",
        ChangeKind.PriceDown => "Price down",
        ChangeKind.BackInStock => "Back in stock",
        ChangeKind.OutOfStock => "Out of stock",
        _ => kind.ToString()
    };
}
