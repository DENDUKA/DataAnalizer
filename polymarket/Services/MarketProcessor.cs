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
        Console.WriteLine("=" + new string('=', 49));
        Console.WriteLine("Polymarket Bitcoin Price History Exporter");
        Console.WriteLine("=" + new string('=', 49));

        // TODO: Implement in subtask-6-2
        // 1. Discover markets using _gammaClient.GetAllMarketsAsync()
        // 2. Filter markets matching "Bitcoin price on" pattern
        // 3. Extract "Yes" outcome clobTokenIds

        // TODO: Implement in subtask-6-3
        // 4. For each token:
        //    - Check if already processed via _tracker.IsProcessed()
        //    - Apply rate limiting via _rateLimiter.WaitAsync()
        //    - Fetch price history via _clobClient.GetPriceHistoryAsync()
        //    - Convert to MarketPriceRecord
        //    - Mark as processed via _tracker.MarkProcessed()

        // TODO: Implement in subtask-6-4
        // 5. Export all records to CSV using CsvHelper

        Console.WriteLine("\nMarketProcessor initialized with all dependencies.");
        Console.WriteLine($"Output directory: {_outputDirectory}");
        Console.WriteLine($"Export file pattern: {_exportFilePattern}");
        Console.WriteLine($"Rate limit delay: {_rateLimiter.DelayMs}ms");
        Console.WriteLine($"Previously processed tokens: {_tracker.Count}");

        await Task.CompletedTask;
    }
}
