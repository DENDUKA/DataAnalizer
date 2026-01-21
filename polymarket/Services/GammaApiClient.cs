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
}
