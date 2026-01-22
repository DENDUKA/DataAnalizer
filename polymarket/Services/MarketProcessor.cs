using System.Diagnostics;
using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using PolymarketHistoryExporter.Models;
using PolymarketHistoryExporter.Utils;

namespace PolymarketHistoryExporter.Services;

/// <summary>
/// Main orchestrator service that coordinates market discovery, price history fetching,
/// and CSV export. This is the primary entry point for the data extraction pipeline.
/// </summary>
public class MarketProcessor
{
    private readonly GammaApiClient _gammaClient;
    private readonly ClobApiClient _clobClient;
    private readonly ProcessedMarketTracker _tracker;
    private readonly RateLimiter _rateLimiter;
    private readonly string _outputDirectory;
    private readonly string _exportFilePattern;

    /// <summary>
    /// Initializes a new instance of the MarketProcessor.
    /// </summary>
    /// <param name="gammaClient">Client for market discovery via Gamma API.</param>
    /// <param name="clobClient">Client for price history retrieval via CLOB API.</param>
    /// <param name="tracker">Tracker for processed markets to avoid duplicate requests.</param>
    /// <param name="rateLimiter">Rate limiter to prevent overwhelming the CLOB API.</param>
    /// <param name="outputDirectory">Directory for CSV export files. Default is ./output</param>
    /// <param name="exportFilePattern">Pattern for export file names. Default is bitcoin_price_history_{timestamp}.csv</param>
    public MarketProcessor(
        GammaApiClient gammaClient,
        ClobApiClient clobClient,
        ProcessedMarketTracker tracker,
        RateLimiter rateLimiter,
        string outputDirectory = "./output",
        string exportFilePattern = "bitcoin_price_history_{timestamp}.csv")
    {
        _gammaClient = gammaClient ?? throw new ArgumentNullException(nameof(gammaClient));
        _clobClient = clobClient ?? throw new ArgumentNullException(nameof(clobClient));
        _tracker = tracker ?? throw new ArgumentNullException(nameof(tracker));
        _rateLimiter = rateLimiter ?? throw new ArgumentNullException(nameof(rateLimiter));
        _outputDirectory = outputDirectory;
        _exportFilePattern = exportFilePattern;
    }

    /// <summary>
    /// Gets the configured output directory for CSV exports.
    /// </summary>
    public string OutputDirectory => _outputDirectory;

    /// <summary>
    /// Gets the configured export file pattern.
    /// </summary>
    public string ExportFilePattern => _exportFilePattern;

    /// <summary>
    /// Runs the complete market data extraction pipeline:
    /// 1. Discovers markets via Gamma API with pagination
    /// 2. Filters for "Bitcoin price on" pattern and extracts "Yes" token IDs
    /// 3. Fetches price history for each token via CLOB API (with rate limiting)
    /// 4. Exports all data to CSV format
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task RunAsync()
    {
        var totalStopwatch = Stopwatch.StartNew();

        Console.WriteLine($"\n{new string('=', 50)}");
        Console.WriteLine("Polymarket Bitcoin Price History Exporter");
        Console.WriteLine($"{new string('=', 50)}");

        Console.WriteLine($"\nOutput directory: {_outputDirectory}");
        Console.WriteLine($"Export file pattern: {_exportFilePattern}");
        Console.WriteLine($"Rate limit delay: {_rateLimiter.DelayMs}ms");
        Console.WriteLine($"Previously processed tokens: {_tracker.Count}");

        // Step 1: Discover markets using Gamma API
        var stepStopwatch = Stopwatch.StartNew();
        Console.WriteLine("\n--- Step 1: Market Discovery ---");
        Console.WriteLine($"Search pattern: {_gammaClient.SearchPattern}");
        Console.WriteLine($"Tag filter: {_gammaClient.Tag}");
        Console.WriteLine("Fetching markets from Gamma API...");

        var allMarkets = await _gammaClient.GetAllMarketsAsync();
        stepStopwatch.Stop();
        Console.WriteLine($"Market discovery completed in {stepStopwatch.Elapsed.TotalSeconds:F2}s");

        if (allMarkets.Count == 0)
        {
            Console.WriteLine("No markets found. Exiting.");
            return;
        }

        // Step 2: Filter markets matching "Bitcoin price on" pattern
        // The GammaApiClient already filters by search pattern, but we apply additional validation
        var bitcoinPricePattern = "bitcoin price on";
        var filteredMarkets = allMarkets
            .Where(m => m.Question.Contains(bitcoinPricePattern, StringComparison.OrdinalIgnoreCase) ||
                       m.Slug.Contains("bitcoin-price-on", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Console.WriteLine($"\nMarkets matching 'Bitcoin price on' pattern: {filteredMarkets.Count}");

        // Step 3: Extract "Yes" outcome clobTokenIds
        var marketsWithYesTokens = new List<(GammaMarket Market, string YesTokenId)>();

        foreach (var market in filteredMarkets)
        {
            var yesTokenId = market.GetYesTokenId();

            if (!string.IsNullOrEmpty(yesTokenId))
            {
                marketsWithYesTokens.Add((market, yesTokenId));
            }
            else
            {
                Console.WriteLine($"Warning: No 'Yes' token found for market: {market.Question}");
            }
        }

        Console.WriteLine($"Markets with valid 'Yes' tokens: {marketsWithYesTokens.Count}");

        if (marketsWithYesTokens.Count == 0)
        {
            Console.WriteLine("No markets with valid 'Yes' tokens found. Exiting.");
            return;
        }

        // Step 4: Fetch price history for each token with rate limiting and deduplication
        Console.WriteLine("\n--- Step 2: Price History Fetching ---");

        var allPriceRecords = new List<MarketPriceRecord>();
        var processedCount = 0;
        var skippedCount = 0;
        var errorCount = 0;
        var totalTokens = marketsWithYesTokens.Count;

        foreach (var (market, yesTokenId) in marketsWithYesTokens)
        {
            // Check if already processed (deduplication)
            if (_tracker.IsProcessed(yesTokenId))
            {
                skippedCount++;
                Console.Write($"\rProgress: {processedCount + skippedCount}/{totalTokens} (processed: {processedCount}, skipped: {skippedCount}, errors: {errorCount})");
                continue;
            }

            // Apply rate limiting before making API request
            await _rateLimiter.WaitAsync();

            try
            {
                // Fetch price history from CLOB API
                var priceHistory = await _clobClient.GetPriceHistoryAsync(yesTokenId);

                if (priceHistory.Count > 0)
                {
                    // Convert PricePoints to MarketPriceRecords
                    foreach (var pricePoint in priceHistory)
                    {
                        var price = pricePoint.GetPriceAsDecimal();
                        if (price.HasValue)
                        {
                            var record = new MarketPriceRecord
                            {
                                Timestamp = pricePoint.GetTimestampAsUtc(),
                                MarketName = market.Question,
                                Price = price.Value
                            };
                            allPriceRecords.Add(record);
                        }
                    }
                }

                // Mark as processed (for persistence across runs)
                _tracker.MarkProcessed(yesTokenId);
                processedCount++;
            }
            catch (HttpRequestException ex)
            {
                errorCount++;
                Console.WriteLine($"\nError fetching price history for '{market.Question}': {ex.Message}");
            }

            Console.Write($"\rProgress: {processedCount + skippedCount}/{totalTokens} (processed: {processedCount}, skipped: {skippedCount}, errors: {errorCount})");
        }

        Console.WriteLine($"\n\nPrice history fetching complete:");
        Console.WriteLine($"  - Tokens processed: {processedCount}");
        Console.WriteLine($"  - Tokens skipped (already cached): {skippedCount}");
        Console.WriteLine($"  - Errors encountered: {errorCount}");
        Console.WriteLine($"  - Total price records collected: {allPriceRecords.Count}");

        // Step 5: Export all records to CSV using CsvHelper
        Console.WriteLine("\n--- Step 3: CSV Export ---");

        if (allPriceRecords.Count == 0)
        {
            Console.WriteLine("No price records to export. Skipping CSV export.");
        }
        else
        {
            // Sort records by timestamp for consistent output
            var sortedRecords = allPriceRecords
                .OrderBy(r => r.MarketName)
                .ThenBy(r => r.Timestamp)
                .ToList();

            // Ensure output directory exists
            if (!Directory.Exists(_outputDirectory))
            {
                Directory.CreateDirectory(_outputDirectory);
                Console.WriteLine($"Created output directory: {_outputDirectory}");
            }

            // Generate filename from pattern (replace {timestamp} with current UTC timestamp)
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            var fileName = _exportFilePattern.Replace("{timestamp}", timestamp);
            var outputFile = Path.Combine(_outputDirectory, fileName);

            // Configure CsvHelper
            var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true
            };

            // Write records to CSV
            await using var writer = new StreamWriter(outputFile);
            await using var csv = new CsvWriter(writer, csvConfig);

            await csv.WriteRecordsAsync(sortedRecords);

            var fileInfo = new FileInfo(outputFile);
            Console.WriteLine($"Exported {sortedRecords.Count} records to: {outputFile}");
            Console.WriteLine($"File size: {fileInfo.Length / 1024.0:F2} KB ({fileInfo.Length / 1024.0 / 1024.0:F2} MB)");
        }

        Console.WriteLine("\n--- Processing Complete ---");
        Console.WriteLine($"Total markets discovered: {allMarkets.Count}");
        Console.WriteLine($"Bitcoin price markets: {filteredMarkets.Count}");
        Console.WriteLine($"Markets processed: {processedCount}");
        Console.WriteLine($"Price records ready for export: {allPriceRecords.Count}");
    }
}
