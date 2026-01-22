# Specification: Polymarket Bitcoin Price History Exporter

## Overview

Build a C# console application that discovers all completed Polymarket prediction markets matching the pattern "Bitcoin price on [Date]", retrieves minute-level historical price data (probabilities) for the "Yes" outcome tokens, and exports the data to CSV format. The service integrates with two Polymarket APIs: Gamma API for market discovery and filtering, and CLOB API for price history retrieval.

## Workflow Type

**Type**: feature

**Rationale**: This is a new standalone service being built from scratch. It implements a complete data extraction and export pipeline with API integration, pagination handling, rate limiting, and data persistence. The feature involves creating new functionality rather than modifying existing code, making it a feature implementation workflow.

## Task Scope

### Services Involved
- **PolymarketHistoryExporter** (primary) - New C# console application for Bitcoin price market data extraction

### This Task Will:
- [ ] Implement market discovery using Polymarket Gamma API with search filters ("Crypto" tag, "Bitcoin price on" pattern)
- [ ] Filter markets to only closed (closed=true) and archived markets
- [ ] Implement pagination logic to collect ALL historical markets (offset-based loop)
- [ ] Extract clobTokenIds from discovered markets and identify "Yes" outcome tokens
- [ ] Fetch price history for each "Yes" token using CLOB API `/prices-history` endpoint
- [ ] Implement rate limiting with Task.Delay to avoid HTTP 429 errors
- [ ] Convert Polymarket prices to probabilities ($0.65 → 65%)
- [ ] Track already-processed markets to avoid duplicate requests (deduplication)
- [ ] Export data to CSV format with schema: Timestamp | Market Name | Price (Probability)
- [ ] Handle API errors gracefully with retry logic and exponential backoff

### Out of Scope:
- Real-time market monitoring or live price updates
- Database storage (file-based persistence only)
- UI/Web interface (console application only)
- Processing "No" outcome tokens
- Historical data for non-Bitcoin markets
- Analysis or prediction functionality

## Service Context

### PolymarketHistoryExporter

**Tech Stack:**
- Language: C# (.NET 6+ or .NET 8)
- Framework: Console Application
- HTTP Client: System.Net.Http (built-in HttpClient)
- JSON Parsing: System.Text.Json (built-in)
- CSV Export: CsvHelper (NuGet package)
- Key directories: `/src` (source code), `/output` (CSV export files), `/cache` (processed market tracking)

**Entry Point:** `Program.cs`

**How to Run:**
```bash
# Build the project
dotnet build

# Run the service
dotnet run

# Or after build
dotnet run --project PolymarketHistoryExporter.csproj
```

**Dependencies:**
```bash
dotnet add package CsvHelper
```

## Files to Create

Since this is a greenfield project, the following files will be created:

| File | Purpose |
|------|---------|
| `PolymarketHistoryExporter.csproj` | Project file with dependencies (CsvHelper) |
| `Program.cs` | Entry point and main orchestration logic |
| `Services/GammaApiClient.cs` | Client for Gamma API market discovery |
| `Services/ClobApiClient.cs` | Client for CLOB API price history retrieval |
| `Services/MarketProcessor.cs` | Main service coordinating discovery, fetching, and export |
| `Services/ProcessedMarketTracker.cs` | Tracks already-processed markets (file-based) |
| `Models/GammaApiModels.cs` | DTOs for Gamma API responses (Event, Market, ClobTokenId) |
| `Models/ClobApiModels.cs` | DTOs for CLOB API responses (PriceHistory, PricePoint) |
| `Models/ExportModels.cs` | Export data structure for CSV (MarketPriceRecord) |
| `Utils/RateLimiter.cs` | Rate limiting utility with Task.Delay and backoff |
| `appsettings.json` | Configuration (API URLs, rate limits, file paths) |
| `.gitignore` | Ignore /output, /cache, bin, obj directories |

## API Integration Details

### Polymarket Gamma API

**Base URL:** `https://gamma-api.polymarket.com`

**Key Endpoints:**

1. **Market Discovery:**
   - Endpoint: `/events` or `/markets`
   - Method: GET
   - Query Parameters:
     - `tag=Crypto` OR `search=Bitcoin price on`
     - `closed=true` (only completed markets)
     - `archived=true` (include archived)
     - `limit=100` (results per page)
     - `offset=0` (pagination offset)

2. **Response Structure:**
```json
{
  "data": [
    {
      "id": "event-id",
      "title": "Bitcoin price on January 15, 2024",
      "markets": [
        {
          "id": "market-id",
          "question": "Will BTC close above $45,000?",
          "clobTokenIds": ["0xabc123...", "0xdef456..."],
          "outcomes": ["Yes", "No"]
        }
      ]
    }
  ],
  "count": 250,
  "next_offset": 100
}
```

### Polymarket CLOB API

**Base URL:** `https://clob.polymarket.com`

**Key Endpoint:**

1. **Price History:**
   - Endpoint: `/prices-history`
   - Method: GET
   - Query Parameters:
     - `market=<clobTokenId>` (Yes token ID only)
     - `interval=max` (all available data)
     - `fidelity=60` (60 seconds = minute-level granularity)

2. **Response Structure:**
```json
{
  "history": [
    {
      "t": 1705334400,
      "p": "0.6523"
    },
    {
      "t": 1705334460,
      "p": "0.6587"
    }
  ]
}
```

**Price Conversion:** Polymarket price $0.65 = 65% probability

## Implementation Architecture

### Core Data Flow

```
1. Market Discovery (Gamma API)
   ↓
2. Pagination Loop (collect all pages)
   ↓
3. Extract Markets & ClobTokenIds
   ↓
4. Filter "Yes" Tokens Only
   ↓
5. Check Processed Market Tracker
   ↓
6. For Each New Market:
   a. Rate Limit (Task.Delay)
   b. Fetch Price History (CLOB API)
   c. Parse & Convert Probabilities
   d. Append to CSV
   e. Mark as Processed
   ↓
7. Export Complete CSV File
```

### Class Structure

```csharp
// Program.cs - Entry point
public class Program
{
    static async Task Main(string[] args)
    {
        var processor = new MarketProcessor();
        await processor.RunAsync();
    }
}

// Services/MarketProcessor.cs - Main orchestrator
public class MarketProcessor
{
    private readonly GammaApiClient _gammaClient;
    private readonly ClobApiClient _clobClient;
    private readonly ProcessedMarketTracker _tracker;
    private readonly RateLimiter _rateLimiter;

    public async Task RunAsync()
    {
        // 1. Discover markets with pagination
        // 2. Process each market
        // 3. Export to CSV
    }
}

// Services/GammaApiClient.cs
public class GammaApiClient
{
    public async Task<List<Market>> DiscoverMarketsAsync(string searchPattern, int offset);
}

// Services/ClobApiClient.cs
public class ClobApiClient
{
    public async Task<List<PricePoint>> GetPriceHistoryAsync(string tokenId);
}

// Services/ProcessedMarketTracker.cs
public class ProcessedMarketTracker
{
    private HashSet<string> _processedTokenIds;

    public void LoadFromFile();
    public bool IsProcessed(string tokenId);
    public void MarkProcessed(string tokenId);
    public void SaveToFile();
}

// Utils/RateLimiter.cs
public class RateLimiter
{
    private readonly int _delayMs;

    public async Task WaitAsync();
    public async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> action);
}
```

## Patterns to Follow

### HTTP Client Pattern (Singleton)

**DO:**
```csharp
// Create HttpClient as singleton or use IHttpClientFactory
public class GammaApiClient
{
    private static readonly HttpClient _httpClient = new HttpClient
    {
        BaseAddress = new Uri("https://gamma-api.polymarket.com"),
        Timeout = TimeSpan.FromSeconds(30)
    };

    public async Task<string> GetAsync(string endpoint)
    {
        return await _httpClient.GetStringAsync(endpoint);
    }
}
```

**DON'T:**
```csharp
// NEVER create new HttpClient per request (socket exhaustion)
public async Task<string> GetDataAsync()
{
    using var client = new HttpClient(); // ❌ BAD
    return await client.GetStringAsync(url);
}
```

### JSON Deserialization Pattern

**DO:**
```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

public class Market
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("clobTokenIds")]
    public List<string> ClobTokenIds { get; set; }
}

var options = new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true
};

var markets = JsonSerializer.Deserialize<List<Market>>(json, options);
```

### Pagination Loop Pattern

**DO:**
```csharp
public async Task<List<Market>> GetAllMarketsAsync()
{
    var allMarkets = new List<Market>();
    int offset = 0;
    const int limit = 100;
    bool hasMore = true;

    while (hasMore)
    {
        var response = await _gammaClient.GetMarketsAsync(offset, limit);
        allMarkets.AddRange(response.Data);

        offset += limit;
        hasMore = response.Data.Count == limit; // Stop if less than page size
    }

    return allMarkets;
}
```

### Rate Limiting Pattern

**DO:**
```csharp
public class RateLimiter
{
    private readonly int _delayMs;
    private DateTime _lastRequest = DateTime.MinValue;

    public RateLimiter(int delayMs = 500)
    {
        _delayMs = delayMs;
    }

    public async Task WaitAsync()
    {
        var elapsed = (DateTime.UtcNow - _lastRequest).TotalMilliseconds;
        if (elapsed < _delayMs)
        {
            await Task.Delay(_delayMs - (int)elapsed);
        }
        _lastRequest = DateTime.UtcNow;
    }

    // Exponential backoff for HTTP 429
    public async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> action, int maxRetries = 3)
    {
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                await WaitAsync();
                return await action();
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                if (i == maxRetries - 1) throw;

                var backoffMs = (int)Math.Pow(2, i) * 1000; // 1s, 2s, 4s
                Console.WriteLine($"Rate limited. Waiting {backoffMs}ms...");
                await Task.Delay(backoffMs);
            }
        }
        throw new Exception("Max retries exceeded");
    }
}
```

### CSV Export Pattern

**DO:**
```csharp
using CsvHelper;
using System.Globalization;

public class MarketPriceRecord
{
    public DateTime Timestamp { get; set; }
    public string MarketName { get; set; }
    public decimal Price { get; set; } // Probability as decimal (0.65)
    public decimal Probability => Price * 100; // Convert to percentage
}

public async Task ExportToCsvAsync(List<MarketPriceRecord> records, string filePath)
{
    using var writer = new StreamWriter(filePath);
    using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

    await csv.WriteRecordsAsync(records);
}
```

### Processed Market Tracking Pattern

**DO:**
```csharp
public class ProcessedMarketTracker
{
    private HashSet<string> _processedTokenIds;
    private readonly string _cacheFilePath;

    public ProcessedMarketTracker(string cacheFilePath = "./cache/processed_markets.txt")
    {
        _cacheFilePath = cacheFilePath;
        LoadFromFile();
    }

    public void LoadFromFile()
    {
        if (File.Exists(_cacheFilePath))
        {
            var lines = File.ReadAllLines(_cacheFilePath);
            _processedTokenIds = new HashSet<string>(lines);
        }
        else
        {
            _processedTokenIds = new HashSet<string>();
            Directory.CreateDirectory(Path.GetDirectoryName(_cacheFilePath));
        }
    }

    public bool IsProcessed(string tokenId) => _processedTokenIds.Contains(tokenId);

    public void MarkProcessed(string tokenId)
    {
        _processedTokenIds.Add(tokenId);
        File.AppendAllText(_cacheFilePath, tokenId + Environment.NewLine);
    }
}
```

## Requirements

### Functional Requirements

1. **Market Discovery**
   - Description: Query Gamma API with filters for "Bitcoin price on" pattern or "Crypto" tag
   - Acceptance: Successfully retrieve list of closed and archived Bitcoin price markets
   - Implementation: Use `/events` or `/markets` endpoint with `closed=true` and search filters

2. **Complete Pagination**
   - Description: Implement offset-based loop to collect ALL markets across multiple pages
   - Acceptance: Service continues fetching until no more results returned (count < limit)
   - Implementation: Increment offset by page size, loop until empty response

3. **Token Identification**
   - Description: Extract clobTokenIds from each market and identify "Yes" outcome tokens
   - Acceptance: Correctly map token IDs to "Yes" outcome only (not "No")
   - Implementation: Parse clobTokenIds array from market response, use outcomes array to identify position

4. **Price History Retrieval**
   - Description: Fetch minute-level price history for each "Yes" token using CLOB API
   - Acceptance: Retrieve complete historical price data with timestamps and prices
   - Implementation: Call `/prices-history` with parameters: market=<tokenId>, interval=max, fidelity=60

5. **Probability Conversion**
   - Description: Convert Polymarket prices ($0.65) to probabilities (65%)
   - Acceptance: Price values correctly multiplied by 100 for percentage representation
   - Implementation: Parse decimal price from API response, multiply by 100

6. **Rate Limiting**
   - Description: Implement Task.Delay between CLOB API requests to avoid HTTP 429 errors
   - Acceptance: No rate limit errors occur during normal operation, exponential backoff on 429
   - Implementation: Use RateLimiter class with 500ms default delay and retry logic

7. **Deduplication**
   - Description: Track processed markets and skip re-fetching data
   - Acceptance: Markets processed in previous runs are not re-queried
   - Implementation: File-based cache storing processed token IDs, checked before each fetch

8. **CSV Export**
   - Description: Export data to CSV format with schema: Timestamp | Market Name | Price (Probability)
   - Acceptance: Valid CSV file generated with all required columns and data
   - Implementation: Use CsvHelper library with proper culture settings

### Edge Cases

1. **Empty Market Results** - If no markets match filters, log message and exit gracefully (no error)
2. **API Unavailable** - If API returns 500/503, log error, wait 5 seconds, retry up to 3 times
3. **Invalid Token ID** - If CLOB API returns 404 for token, log warning and skip to next market
4. **Malformed JSON Response** - Catch JsonException, log error with response body, skip record
5. **Large Price History** - For markets with extensive history (>10,000 points), consider streaming/batching CSV writes
6. **Duplicate Token IDs Across Markets** - Use token ID as unique key (not market name) for deduplication
7. **Network Timeouts** - Set HttpClient timeout to 30 seconds, retry on timeout exceptions
8. **File Write Permissions** - Check directory exists and is writable before export, create if missing

## Implementation Notes

### DO
- Use HttpClient as singleton (static field or IHttpClientFactory) to prevent socket exhaustion
- Set `PropertyNameCaseInsensitive = true` in JsonSerializerOptions for robust deserialization
- Use `CultureInfo.InvariantCulture` for CSV exports to ensure consistent number formatting
- Implement logging to console for progress tracking (e.g., "Processing market 15/250...")
- Start with conservative rate limit (500ms delay) and adjust based on API responses
- Create output and cache directories if they don't exist (Directory.CreateDirectory)
- Use async/await throughout for efficient I/O operations
- Parse Unix timestamps from CLOB API (t: 1705334400) to DateTime using DateTimeOffset.FromUnixTimeSeconds()
- Handle both `tag=Crypto` AND `search=Bitcoin price on` approaches (try both, merge results)

### DON'T
- Don't create new HttpClient instances per request (memory/socket leak)
- Don't proceed to next page if API returns error - retry with exponential backoff first
- Don't assume first token in clobTokenIds array is "Yes" - verify using outcomes array
- Don't skip rate limiting even for first request (establish pattern immediately)
- Don't write entire dataset to memory before CSV export if very large (stream if needed)
- Don't use `closed=false` filter - only historical completed markets are in scope
- Don't hardcode API URLs - use configuration file (appsettings.json)
- Don't suppress exceptions silently - log all errors with context (market ID, token ID, etc.)

## Development Environment

### Start Services

```bash
# Initialize project
dotnet new console -n PolymarketHistoryExporter
cd PolymarketHistoryExporter

# Add dependencies
dotnet add package CsvHelper

# Build
dotnet build

# Run
dotnet run

# Run with specific configuration
dotnet run --configuration Release
```

### Configuration (appsettings.json)

```json
{
  "ApiSettings": {
    "GammaApiBaseUrl": "https://gamma-api.polymarket.com",
    "ClobApiBaseUrl": "https://clob.polymarket.com",
    "RateLimitDelayMs": 500,
    "HttpTimeoutSeconds": 30,
    "MaxRetries": 3
  },
  "SearchSettings": {
    "SearchPattern": "Bitcoin price on",
    "Tag": "Crypto",
    "OnlyClosedMarkets": true,
    "OnlyArchivedMarkets": true,
    "PageSize": 100
  },
  "ExportSettings": {
    "OutputDirectory": "./output",
    "CacheDirectory": "./cache",
    "ProcessedMarketsFile": "./cache/processed_markets.txt",
    "ExportFileNamePattern": "bitcoin_price_history_{timestamp}.csv"
  }
}
```

### Required Environment Variables
None - all configuration in appsettings.json

### Directory Structure
```
PolymarketHistoryExporter/
├── PolymarketHistoryExporter.csproj
├── Program.cs
├── appsettings.json
├── Services/
│   ├── GammaApiClient.cs
│   ├── ClobApiClient.cs
│   ├── MarketProcessor.cs
│   └── ProcessedMarketTracker.cs
├── Models/
│   ├── GammaApiModels.cs
│   ├── ClobApiModels.cs
│   └── ExportModels.cs
├── Utils/
│   └── RateLimiter.cs
├── output/          (generated)
│   └── bitcoin_price_history_*.csv
├── cache/           (generated)
│   └── processed_markets.txt
├── bin/
└── obj/
```

## Success Criteria

The task is complete when:

1. [ ] Service successfully discovers all closed Bitcoin price markets from Gamma API with complete pagination
2. [ ] Service correctly extracts "Yes" outcome clobTokenIds from each market
3. [ ] Service fetches price history for each token from CLOB API with minute-level granularity
4. [ ] Rate limiting prevents HTTP 429 errors (no more than 2 requests per second)
5. [ ] Processed markets are tracked and not re-fetched on subsequent runs
6. [ ] Prices are correctly converted from decimal ($0.65) to probability percentage (65%)
7. [ ] CSV file is generated with valid structure: Timestamp | Market Name | Price (Probability)
8. [ ] No console errors or unhandled exceptions during execution
9. [ ] Service handles API errors gracefully with retry logic and exponential backoff
10. [ ] Documentation includes instructions for building, running, and configuring the service

## QA Acceptance Criteria

**CRITICAL**: These criteria must be verified by the QA Agent before sign-off.

### Unit Tests
| Test | File | What to Verify |
|------|------|----------------|
| PriceConversion_Test | `Tests/ModelTests.cs` | Verify $0.65 converts to 65.0% probability |
| TokenIdExtraction_Test | `Tests/GammaApiClientTests.cs` | Verify correct extraction of "Yes" tokens from clobTokenIds array |
| PaginationLogic_Test | `Tests/MarketProcessorTests.cs` | Verify pagination continues until empty result set |
| RateLimiter_Test | `Tests/RateLimiterTests.cs` | Verify minimum delay enforced between requests |
| ProcessedMarketTracking_Test | `Tests/ProcessedMarketTrackerTests.cs` | Verify markets marked as processed are skipped |

### Integration Tests
| Test | Services | What to Verify |
|------|----------|----------------|
| GammaApi_DiscoverMarkets_Integration | GammaApiClient ↔ Gamma API | Verify real API returns valid market data with expected schema |
| ClobApi_GetPriceHistory_Integration | ClobApiClient ↔ CLOB API | Verify real API returns price history with timestamps and prices |
| EndToEnd_SingleMarket_Test | All Services | Verify complete flow from discovery → fetch → export for 1 market |

### End-to-End Tests
| Flow | Steps | Expected Outcome |
|------|-------|------------------|
| Complete Data Export Flow | 1. Run `dotnet run` 2. Wait for completion 3. Check output directory | CSV file exists with data, no errors in console |
| Idempotency Test | 1. Run service 2. Run again immediately | Second run skips processed markets, no duplicate API calls |
| Rate Limit Compliance | 1. Monitor API calls with debugger 2. Verify delays | Minimum 500ms between CLOB API requests, exponential backoff on 429 |

### Manual Verification
| Check | Command/Action | Expected Result |
|-------|---------------|-----------------|
| CSV Schema Validation | Open generated CSV file | Columns: Timestamp, Market Name, Price (or Probability) |
| Data Accuracy | Compare sample row to CLOB API response | Timestamp and price match API data |
| Processed Market Cache | Open `./cache/processed_markets.txt` | Contains token IDs of processed markets |
| Console Output | Review console logs | Progress messages, no error stack traces |

### API Verification (Manual Testing)
| Endpoint | Test Command | Expected |
|----------|-------------|----------|
| Gamma API Markets | `curl "https://gamma-api.polymarket.com/markets?tag=Crypto&closed=true&limit=10"` | JSON response with market data |
| CLOB API Price History | `curl "https://clob.polymarket.com/prices-history?market=<tokenId>&interval=max&fidelity=60"` | JSON response with price history array |

### Error Handling Verification
| Scenario | How to Test | Expected Behavior |
|----------|-------------|-------------------|
| Invalid Token ID | Modify code to use fake token ID | Log warning, skip market, continue processing |
| Network Timeout | Disconnect network briefly during run | Retry 3 times with backoff, then skip market |
| HTTP 429 Rate Limit | Remove rate limiter temporarily | Exponential backoff triggers, request succeeds on retry |

### QA Sign-off Requirements
- [ ] All unit tests pass with >80% code coverage
- [ ] Integration tests successfully call real APIs and parse responses
- [ ] End-to-end test completes without errors and generates valid CSV
- [ ] Manual CSV verification shows correct schema and data accuracy
- [ ] Idempotency test confirms deduplication works (no duplicate processing)
- [ ] Rate limiting verified via logs (minimum delays observed)
- [ ] Processed market cache file created and populated correctly
- [ ] Code follows C# conventions (PascalCase classes, camelCase fields, async/await)
- [ ] No security vulnerabilities (no hardcoded secrets, safe file operations)
- [ ] Error handling tested for common failure scenarios
- [ ] Documentation includes build/run instructions and configuration guide
