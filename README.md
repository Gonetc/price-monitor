# Price Monitor

[![CI](https://github.com/Gonetc/price-monitor/actions/workflows/ci.yml/badge.svg)](https://github.com/Gonetc/price-monitor/actions/workflows/ci.yml) ![.NET 10](https://img.shields.io/badge/.NET-10-512BD4) ![License: MIT](https://img.shields.io/badge/license-MIT-green)

Command-line tool that collects product prices from an online catalog, compares them with the previous run
and produces a formatted **Excel report** of what changed. Built with **.NET 10**, AngleSharp and ClosedXML.

The demo target is [books.toscrape.com](https://books.toscrape.com) — a sandbox site made specifically
for practising web scraping. The parser is isolated in one class, so pointing the tool at another catalog
means changing a few CSS selectors.

## What it does

1. Walks the whole catalog page by page (following the “next” link) — 50 pages, 1,000 products
2. Extracts title, price, stock status and star rating
3. Compares with the previous run: **price up / down (with %), new, removed, back in stock, out of stock**
4. Writes `output/prices_YYYY-MM-DD_HHmm.xlsx` with three sheets:
   - **Summary** — run time, number of products, average / min / max price, number of changes by type
   - **Products** — all products with filters, frozen header, clickable links, star rating;
     rows with changed prices are highlighted (red — up, green — down, blue — new)
   - **Changes** — what changed since the previous run, sorted by type and size of the change
5. Saves a snapshot for the next comparison
6. Optionally repeats on a schedule until stopped

## Responsible scraping

- Fixed delay between requests (`Scraper:DelayBetweenRequestsMs`, default 500 ms)
- Honest `User-Agent` with a contact link
- Retries only transient errors (network, 5xx, 408, 429) with exponential back-off
- Hard limit on the number of pages
- Only scrape sites whose terms allow it

## Tech stack

| Area | Technology |
|---|---|
| Runtime | .NET 10 console app on the Generic Host (DI, configuration, logging) |
| HTTP | `IHttpClientFactory`, custom retry `DelegatingHandler` |
| HTML parsing | AngleSharp (CSS selectors) |
| Excel | ClosedXML |
| Tests | xUnit — parser on a saved HTML page, change detection, Excel output |

## Getting started

```bash
dotnet run --project src/PriceMonitor                 # full run, ~1 minute
dotnet run --project src/PriceMonitor -- --pages 3    # quick run: first 3 pages
```

Run it twice to see the **Changes** sheet in action.
The report appears in the `output` folder of the directory you started the tool from.

### Options

| Switch | Setting | Default | Description |
|---|---|---|---|
| `--pages N` | `Scraper:MaxPages` | `50` | Max catalog pages to visit |
| `--delay MS` | `Scraper:DelayBetweenRequestsMs` | `500` | Pause between requests |
| `--interval MIN` | `Monitor:IntervalMinutes` | `0` | Repeat every N minutes (0 = run once) |
| `--out PATH` | `Monitor:OutputFolder` | `output` | Where reports and snapshots go |

Example — check prices every 6 hours: `dotnet run --project src/PriceMonitor -- --interval 360`.
For production use, schedule a single run with Windows Task Scheduler or cron instead.

Exit codes: `0` success, `1` run failed (site unavailable, file locked), `2` invalid configuration.

### Run tests

```bash
dotnet test
```

## Project structure

```
src/PriceMonitor
├── Program.cs              host, DI, command-line switches
├── PriceMonitorApp.cs      one run: scrape → compare → report → snapshot; scheduling
├── Scraping/
│   ├── CatalogParser.cs    HTML → products (pure, unit-tested)
│   ├── CatalogScraper.cs   pagination, politeness delay
│   └── RetryHandler.cs     transient error retries
└── Reporting/
    ├── PriceComparer.cs    change detection
    ├── SnapshotStore.cs    previous run on disk (atomic write)
    └── ExcelReportWriter.cs
```

## Roadmap

- [ ] Product detail pages: exact stock count and category
- [ ] Price history across many runs with a chart in Excel
- [ ] Notifications (Telegram / e-mail) when a price drops below a threshold
- [ ] CSV / Google Sheets export

## License

MIT
