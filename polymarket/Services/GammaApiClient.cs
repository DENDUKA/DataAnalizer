using System.Text.Json;
using PolymarketHistoryExporter.Models;

namespace PolymarketHistoryExporter.Services;

/// <summary>
/// Client for interacting with the Polymarket Gamma API.
/// Used for market discovery and retrieval.
/// </summary>
public class GammaApiClient
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly string _searchPattern;
    private readonly string _tag;
    private readonly bool _onlyClosedMarkets;
    private readonly bool _onlyArchivedMarkets;
    private readonly int _pageSize;

    /// <summary>
    /// Initializes a new instance of the GammaApiClient.
    /// </summary>
    /// <param name="baseUrl">Base URL for the Gamma API (e.g., https://gamma-api.polymarket.com).</param>
    /// <param name="timeoutSeconds">HTTP request timeout in seconds.</param>
    /// <param name="searchPattern">Search pattern for market discovery (e.g., "Bitcoin price on").</param>
    /// <param name="tag">Tag filter for markets (e.g., "Crypto").</param>
    /// <param name="onlyClosedMarkets">If true, only retrieve closed markets.</param>
    /// <param name="onlyArchivedMarkets">If true, only retrieve archived markets.</param>
    /// <param name="pageSize">Number of results per page for pagination.</param>
    public GammaApiClient(
        string baseUrl,
        int timeoutSeconds,
        string searchPattern,
        string tag,
        bool onlyClosedMarkets,
        bool onlyArchivedMarkets,
        int pageSize)
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(baseUrl),
            Timeout = TimeSpan.FromSeconds(timeoutSeconds)
        };

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        _searchPattern = searchPattern;
        _tag = tag;
        _onlyClosedMarkets = onlyClosedMarkets;
        _onlyArchivedMarkets = onlyArchivedMarkets;
        _pageSize = pageSize;
    }

    /// <summary>
    /// Gets the configured page size for pagination.
    /// </summary>
    public int PageSize => _pageSize;

    /// <summary>
    /// Gets the configured search pattern.
    /// </summary>
    public string SearchPattern => _searchPattern;

    /// <summary>
    /// Gets the configured tag filter.
    /// </summary>
    public string Tag => _tag;

    /// <summary>
    /// Retrieves markets from the Gamma API with pagination.
    /// </summary>
    /// <param name="offset">The offset for pagination (number of items to skip).</param>
    /// <param name="limit">The maximum number of items to return.</param>
    /// <returns>A GammaApiResponse containing the markets and pagination info.</returns>
    public async Task<GammaApiResponse<GammaMarket>> GetMarketsAsync(int offset, int limit)
    {
        var queryParams = new List<string>
        {
            $"offset={offset}",
            $"limit={limit}"
        };

        if (!string.IsNullOrEmpty(_tag))
        {
            queryParams.Add($"tag={Uri.EscapeDataString(_tag)}");
        }

        if (_onlyClosedMarkets)
        {
            queryParams.Add("closed=true");
        }

        if (_onlyArchivedMarkets)
        {
            queryParams.Add("archived=true");
        }

        if (!string.IsNullOrEmpty(_searchPattern))
        {
            queryParams.Add($"_searchType=slug");
            queryParams.Add($"slug_starts_with={Uri.EscapeDataString(_searchPattern.ToLowerInvariant().Replace(" ", "-"))}");
        }

        var url = $"/markets?{string.Join("&", queryParams)}";

        try
        {
            var response = await _httpClient.GetStringAsync(url);
            var result = JsonSerializer.Deserialize<GammaApiResponse<GammaMarket>>(response, _jsonOptions);

            return result ?? new GammaApiResponse<GammaMarket>();
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"Error fetching markets: {ex.Message}");
            throw;
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"Error parsing markets response: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Retrieves an event by its slug from the Gamma API.
    /// </summary>
    /// <param name="slug">The event slug (e.g., "bitcoin-price-on-january-21").</param>
    /// <returns>The GammaEvent if found, or null if not found.</returns>
    public async Task<GammaEvent?> GetEventBySlugAsync(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            throw new ArgumentException("Slug cannot be null or empty.", nameof(slug));
        }

        // Normalize slug: lowercase and replace spaces with dashes
        var normalizedSlug = slug.ToLowerInvariant().Replace(" ", "-").Trim();
        var url = $"/events?slug={Uri.EscapeDataString(normalizedSlug)}";

        try
        {
            var response = await _httpClient.GetStringAsync(url);
            var events = JsonSerializer.Deserialize<List<GammaEvent>>(response, _jsonOptions);

            // The API returns an array; we expect exactly one match for a specific slug
            return events?.FirstOrDefault();
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"Error fetching event by slug '{slug}': {ex.Message}");
            throw;
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"Error parsing event response for slug '{slug}': {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Retrieves all markets from the Gamma API by iterating through all pages.
    /// Uses configured page size and filters.
    /// </summary>
    /// <returns>A list of all matching GammaMarket objects.</returns>
    public async Task<List<GammaMarket>> GetAllMarketsAsync()
    {
        var allMarkets = new List<GammaMarket>();
        var currentOffset = 0;

        while (true)
        {
            try
            {
                var response = await GetMarketsAsync(currentOffset, _pageSize);

                if (response.Data == null || response.Data.Count == 0)
                    break;

                allMarkets.AddRange(response.Data);

                Console.Write($"\rMarkets loaded: {allMarkets.Count}");

                // Check if there are more pages
                if (response.NextOffset == null || response.Data.Count < _pageSize)
                    break;

                currentOffset = response.NextOffset.Value;

                // Rate limiting delay
                await Task.Delay(100);
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"\nRequest error: {ex.Message}");
                Console.WriteLine("Retrying in 5 seconds...");
                await Task.Delay(5000);
            }
        }

        Console.WriteLine($"\nTotal markets loaded: {allMarkets.Count}");

        return allMarkets;
    }
}
