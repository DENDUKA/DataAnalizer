using System.Text.Json;
using PolymarketHistoryExporter.Services;
using PolymarketHistoryExporter.Utils;

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
    return 0;
}
catch (Exception ex)
{
    Console.WriteLine($"\nFatal error: {ex.Message}");
    Console.WriteLine($"Stack trace: {ex.StackTrace}");
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
