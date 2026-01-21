using System.Text.Json;
using PolymarketHistoryExporter.Models;

namespace PolymarketHistoryExporter.Services;

/// <summary>
/// Client for interacting with the Polymarket CLOB API.
/// Used for fetching price history data for market tokens.
/// </summary>
public class ClobApiClient
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// Initializes a new instance of the ClobApiClient.
    /// </summary>
    /// <param name="baseUrl">Base URL for the CLOB API (e.g., https://clob.polymarket.com).</param>
    /// <param name="timeoutSeconds">HTTP request timeout in seconds.</param>
    public ClobApiClient(string baseUrl, int timeoutSeconds)
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
    }
}
