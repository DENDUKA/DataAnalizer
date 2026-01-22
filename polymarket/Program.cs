using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using PolymarketHistoryExporter.Models;
using PolymarketHistoryExporter.Services;
using PolymarketHistoryExporter.Utils;

// Track execution time
var stopwatch = Stopwatch.StartNew();

Console.WriteLine($"\n{new string('=', 50)}");
Console.WriteLine("Polymarket History Exporter - Startup");
Console.WriteLine($"{new string('=', 50)}");
Console.WriteLine($"Started at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

// Load configuration from appsettings.json
var configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
if (!File.Exists(configPath))
{
    // Try current directory as fallback
    configPath = "appsettings.json";
}

if (!File.Exists(configPath))
{
    Console.WriteLine("Error: appsettings.json not found.");
    Console.WriteLine($"Searched paths:");
    Console.WriteLine($"  - {Path.Combine(AppContext.BaseDirectory, "appsettings.json")}");
    Console.WriteLine($"  - {Path.GetFullPath("appsettings.json")}");
    return 1;
}

Console.WriteLine($"Loading configuration from: {Path.GetFullPath(configPath)}");

AppSettings? settings;
try
{
    var configJson = await File.ReadAllTextAsync(configPath);
    settings = JsonSerializer.Deserialize<AppSettings>(configJson, new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    });

    if (settings == null)
    {
        Console.WriteLine("Error: Failed to parse appsettings.json.");
        return 1;
    }
}
catch (JsonException ex)
{
    Console.WriteLine($"Error parsing appsettings.json: {ex.Message}");
    return 1;
}
catch (IOException ex)
{
    Console.WriteLine($"Error reading appsettings.json: {ex.Message}");
    return 1;
}

// Extract configuration values with defaults
var apiSettings = settings.ApiSettings ?? new ApiSettings();
var searchSettings = settings.SearchSettings ?? new SearchSettings();
var exportSettings = settings.ExportSettings ?? new ExportSettings();

Console.WriteLine("Configuration loaded successfully.");
Console.WriteLine($"\n--- API Settings ---");
Console.WriteLine($"Gamma API: {apiSettings.GammaApiBaseUrl ?? "https://gamma-api.polymarket.com"}");
Console.WriteLine($"CLOB API: {apiSettings.ClobApiBaseUrl ?? "https://clob.polymarket.com"}");
Console.WriteLine($"Rate limit delay: {apiSettings.RateLimitDelayMs}ms");
Console.WriteLine($"HTTP timeout: {apiSettings.HttpTimeoutSeconds}s");

Console.WriteLine($"\n--- Search Settings ---");
Console.WriteLine($"Search pattern: {searchSettings.SearchPattern ?? "Bitcoin price on"}");
Console.WriteLine($"Tag filter: {searchSettings.Tag ?? "Crypto"}");
Console.WriteLine($"Only closed markets: {searchSettings.OnlyClosedMarkets}");
Console.WriteLine($"Only archived markets: {searchSettings.OnlyArchivedMarkets}");
Console.WriteLine($"Page size: {searchSettings.PageSize}");

Console.WriteLine($"\n--- Export Settings ---");
Console.WriteLine($"Output directory: {exportSettings.OutputDirectory ?? "./output"}");
Console.WriteLine($"Cache file: {exportSettings.ProcessedMarketsFile ?? "./cache/processed_markets.txt"}");
Console.WriteLine($"File pattern: {exportSettings.ExportFileNamePattern ?? "bitcoin_price_history_{timestamp}.csv"}");

// Create Gamma API client for market discovery
var gammaClient = new GammaApiClient(
    baseUrl: apiSettings.GammaApiBaseUrl ?? "https://gamma-api.polymarket.com",
    timeoutSeconds: apiSettings.HttpTimeoutSeconds,
    searchPattern: searchSettings.SearchPattern ?? "Bitcoin price on",
    tag: searchSettings.Tag ?? "Crypto",
    onlyClosedMarkets: searchSettings.OnlyClosedMarkets,
    onlyArchivedMarkets: searchSettings.OnlyArchivedMarkets,
    pageSize: searchSettings.PageSize
);

// Create CLOB API client for price history
var clobClient = new ClobApiClient(
    baseUrl: apiSettings.ClobApiBaseUrl ?? "https://clob.polymarket.com",
    timeoutSeconds: apiSettings.HttpTimeoutSeconds
);

// Create rate limiter to prevent HTTP 429 errors
var rateLimiter = new RateLimiter(
    delayMs: apiSettings.RateLimitDelayMs
);

// Create processed market tracker for deduplication
var tracker = new ProcessedMarketTracker(
    cacheFilePath: exportSettings.ProcessedMarketsFile ?? "./cache/processed_markets.txt"
);

// Create market processor - main orchestrator
var processor = new MarketProcessor(
    gammaClient: gammaClient,
    clobClient: clobClient,
    tracker: tracker,
    rateLimiter: rateLimiter,
    outputDirectory: exportSettings.OutputDirectory ?? "./output",
    exportFilePattern: exportSettings.ExportFileNamePattern ?? "bitcoin_price_history_{timestamp}.csv"
);

// Run the export pipeline
try
{
    await processor.RunAsync();

    stopwatch.Stop();
    Console.WriteLine($"\n{new string('=', 50)}");
    Console.WriteLine("Export completed successfully!");
    Console.WriteLine($"Total elapsed time: {stopwatch.Elapsed:hh\\:mm\\:ss\\.fff}");
    Console.WriteLine($"Finished at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
    Console.WriteLine($"{new string('=', 50)}");

    return 0;
}
catch (Exception ex)
{
    stopwatch.Stop();
    Console.WriteLine($"\n{new string('=', 50)}");
    Console.WriteLine($"Fatal error: {ex.Message}");
    Console.WriteLine($"Stack trace: {ex.StackTrace}");
    Console.WriteLine($"Total elapsed time: {stopwatch.Elapsed:hh\\:mm\\:ss\\.fff}");
    Console.WriteLine($"Failed at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
    Console.WriteLine($"{new string('=', 50)}");
    return 1;
}

// Configuration classes for appsettings.json deserialization
public class AppSettings
{
    public ApiSettings? ApiSettings { get; set; }
    public SearchSettings? SearchSettings { get; set; }
    public ExportSettings? ExportSettings { get; set; }
}

public class ApiSettings
{
    public string? GammaApiBaseUrl { get; set; }
    public string? ClobApiBaseUrl { get; set; }
    public int RateLimitDelayMs { get; set; } = 500;
    public int HttpTimeoutSeconds { get; set; } = 30;
    public int MaxRetries { get; set; } = 3;
}

public class SearchSettings
{
    public string? SearchPattern { get; set; }
    public string? Tag { get; set; }
    public bool OnlyClosedMarkets { get; set; } = true;
    public bool OnlyArchivedMarkets { get; set; } = true;
    public int PageSize { get; set; } = 100;
}

public class ExportSettings
{
    public string? OutputDirectory { get; set; }
    public string? CacheDirectory { get; set; }
    public string? ProcessedMarketsFile { get; set; }
    public string? ExportFileNamePattern { get; set; }
}

/// <summary>
/// Orchestrator for fetching orderbooks for all outcomes of a Polymarket event.
/// </summary>
public static class OrderbookOrchestrator
{
    /// <summary>
    /// Regex pattern to extract event slug from Polymarket URLs.
    /// Matches: https://polymarket.com/event/some-event-slug
    /// </summary>
    private static readonly Regex EventUrlPattern = new Regex(
        @"polymarket\.com/event/([a-zA-Z0-9\-]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Fetches orderbook data for all outcomes of a Polymarket event.
    /// </summary>
    /// <param name="eventUrlOrSlug">Either a full Polymarket event URL or just the slug (e.g., "bitcoin-price-on-january-21").</param>
    /// <param name="gammaClient">The Gamma API client for fetching event metadata.</param>
    /// <param name="clobClient">The CLOB API client for fetching orderbooks.</param>
    /// <param name="rateLimiter">The rate limiter to prevent HTTP 429 errors.</param>
    /// <returns>
    /// A dictionary mapping outcome names to their orderbook data.
    /// Keys are in the format "MarketQuestion: OutcomeName" (e.g., "Will Bitcoin reach $100k?: Yes").
    /// Values are the OrderbookResponse for that outcome, or null if no orderbook data is available.
    /// </returns>
    /// <exception cref="ArgumentException">Thrown when eventUrlOrSlug is null, empty, or cannot be parsed.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the event is not found or has no markets.</exception>
    public static async Task<Dictionary<string, OrderbookResponse?>> GetEventOrderbooksAsync(
        string eventUrlOrSlug,
        GammaApiClient gammaClient,
        ClobApiClient clobClient,
        RateLimiter rateLimiter)
    {
        if (string.IsNullOrWhiteSpace(eventUrlOrSlug))
        {
            throw new ArgumentException("Event URL or slug cannot be null or empty.", nameof(eventUrlOrSlug));
        }

        // Extract slug from URL if a full URL is provided
        var slug = ExtractSlugFromUrl(eventUrlOrSlug);

        Console.WriteLine($"\n--- Fetching Orderbooks for Event ---");
        Console.WriteLine($"Slug: {slug}");

        // Fetch event metadata from Gamma API
        var gammaEvent = await gammaClient.GetEventBySlugAsync(slug);

        if (gammaEvent == null)
        {
            throw new InvalidOperationException($"Event not found: {slug}");
        }

        Console.WriteLine($"Event found: {gammaEvent.Title}");
        Console.WriteLine($"  Closed: {gammaEvent.Closed}");
        Console.WriteLine($"  Markets: {gammaEvent.Markets.Count}");

        if (gammaEvent.Markets.Count == 0)
        {
            throw new InvalidOperationException($"Event has no markets: {slug}");
        }

        // Check if event is closed - warn but continue (CLOB API may still have data)
        if (gammaEvent.Closed)
        {
            Console.WriteLine("Warning: Event is closed. Orderbook data may be limited or unavailable.");
        }

        var results = new Dictionary<string, OrderbookResponse?>();
        var totalTokens = gammaEvent.Markets.Sum(m => m.ClobTokenIds.Count);
        var processedTokens = 0;
        var successCount = 0;
        var errorCount = 0;

        Console.WriteLine($"\nFetching orderbooks for {totalTokens} outcome tokens...");

        // Iterate through each market in the event
        foreach (var market in gammaEvent.Markets)
        {
            // Validate array lengths match
            if (market.Outcomes.Count != market.ClobTokenIds.Count)
            {
                Console.WriteLine($"Warning: Outcome/TokenId mismatch for market '{market.Question}' " +
                    $"(Outcomes: {market.Outcomes.Count}, TokenIds: {market.ClobTokenIds.Count})");
                continue;
            }

            // Check if market is closed
            if (market.Closed && !market.Active)
            {
                Console.WriteLine($"Skipping closed/inactive market: {market.Question}");
                continue;
            }

            // Iterate through all outcomes (not just Yes/No)
            for (var i = 0; i < market.Outcomes.Count; i++)
            {
                var outcomeName = market.Outcomes[i];
                var tokenId = market.ClobTokenIds[i];

                // Create a unique key for this outcome
                var key = $"{market.Question}: {outcomeName}";

                // Apply rate limiting before each CLOB API call
                await rateLimiter.WaitAsync();

                try
                {
                    var orderbook = await clobClient.GetOrderbookAsync(tokenId);
                    results[key] = orderbook;

                    if (orderbook != null)
                    {
                        successCount++;
                        var bestBid = orderbook.GetBestBid();
                        var bestAsk = orderbook.GetBestAsk();
                        Console.Write($"\r  [{++processedTokens}/{totalTokens}] {outcomeName}: " +
                            $"Bid={bestBid?.ToString("P2") ?? "N/A"}, Ask={bestAsk?.ToString("P2") ?? "N/A"}    ");
                    }
                    else
                    {
                        Console.Write($"\r  [{++processedTokens}/{totalTokens}] {outcomeName}: No orderbook data    ");
                    }
                }
                catch (HttpRequestException ex)
                {
                    errorCount++;
                    results[key] = null;
                    Console.WriteLine($"\r  [{++processedTokens}/{totalTokens}] Error fetching orderbook for '{outcomeName}': {ex.Message}");
                }
            }
        }

        Console.WriteLine($"\n\nOrderbook fetch complete:");
        Console.WriteLine($"  - Total outcomes: {totalTokens}");
        Console.WriteLine($"  - Successful: {successCount}");
        Console.WriteLine($"  - Failed/Empty: {errorCount + (processedTokens - successCount - errorCount)}");

        return results;
    }

    /// <summary>
    /// Extracts the event slug from a Polymarket URL or returns the input if it's already a slug.
    /// </summary>
    /// <param name="eventUrlOrSlug">Either a full Polymarket event URL or just the slug.</param>
    /// <returns>The event slug.</returns>
    private static string ExtractSlugFromUrl(string eventUrlOrSlug)
    {
        // Try to match a URL pattern
        var match = EventUrlPattern.Match(eventUrlOrSlug);
        if (match.Success)
        {
            return match.Groups[1].Value.ToLowerInvariant();
        }

        // Assume it's already a slug - normalize it
        return eventUrlOrSlug.Trim().ToLowerInvariant().Replace(" ", "-");
    }
}
