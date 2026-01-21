using System.Diagnostics;
using System.Text.Json;
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
