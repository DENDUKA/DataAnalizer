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

    private const int MaxRetries = 5;
    private const int InitialDelayMs = 1000;

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

    /// <summary>
    /// Retrieves price history for a single market token from the CLOB API.
    /// </summary>
    /// <param name="tokenId">The clobTokenId of the market token (typically the "Yes" outcome token).</param>
    /// <returns>
    /// A list of PricePoint objects representing the historical price data.
    /// Returns an empty list if the token is not found (HTTP 404) or if no data is available.
    /// </returns>
    public async Task<List<PricePoint>> GetPriceHistoryAsync(string tokenId)
    {
        if (string.IsNullOrEmpty(tokenId))
        {
            return new List<PricePoint>();
        }

        var url = $"/prices-history?market={Uri.EscapeDataString(tokenId)}&interval=max&fidelity=60";
        var retryCount = 0;
        var delayMs = InitialDelayMs;

        while (true)
        {
            try
            {
                var response = await _httpClient.GetStringAsync(url);
                var result = JsonSerializer.Deserialize<PriceHistoryResponse>(response, _jsonOptions);

                return result?.History ?? new List<PricePoint>();
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // Token not found - return empty list (not an error, just no data)
                Console.WriteLine($"Token not found: {tokenId}");
                return new List<PricePoint>();
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                retryCount++;
                if (retryCount > MaxRetries)
                {
                    Console.WriteLine($"Max retries exceeded for token {tokenId} after {MaxRetries} attempts");
                    throw;
                }

                Console.WriteLine($"Rate limited (HTTP 429). Retry {retryCount}/{MaxRetries} after {delayMs}ms...");
                await Task.Delay(delayMs);
                delayMs *= 2; // Exponential backoff
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"Error fetching price history for token {tokenId}: {ex.Message}");
                throw;
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"Error parsing price history response for token {tokenId}: {ex.Message}");
                throw;
            }
        }
    }

    /// <summary>
    /// Retrieves orderbook data (bids/asks) for a single market token from the CLOB API.
    /// </summary>
    /// <param name="tokenId">The clobTokenId of the market token.</param>
    /// <returns>
    /// An OrderbookResponse object containing bid and ask orders.
    /// Returns null if the token is not found (HTTP 404) or if no orderbook data is available.
    /// </returns>
    public async Task<OrderbookResponse?> GetOrderbookAsync(string tokenId)
    {
        if (string.IsNullOrEmpty(tokenId))
        {
            return null;
        }

        var url = $"/book?token_id={Uri.EscapeDataString(tokenId)}";
        var retryCount = 0;
        var delayMs = InitialDelayMs;

        while (true)
        {
            try
            {
                var response = await _httpClient.GetStringAsync(url);
                var result = JsonSerializer.Deserialize<OrderbookResponse>(response, _jsonOptions);

                return result;
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // Token not found - return null (not an error, just no data)
                Console.WriteLine($"Orderbook not found for token: {tokenId}");
                return null;
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                retryCount++;
                if (retryCount > MaxRetries)
                {
                    Console.WriteLine($"Max retries exceeded for orderbook token {tokenId} after {MaxRetries} attempts");
                    throw;
                }

                Console.WriteLine($"Rate limited (HTTP 429). Retry {retryCount}/{MaxRetries} after {delayMs}ms...");
                await Task.Delay(delayMs);
                delayMs *= 2; // Exponential backoff
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"Error fetching orderbook for token {tokenId}: {ex.Message}");
                throw;
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"Error parsing orderbook response for token {tokenId}: {ex.Message}");
                throw;
            }
        }
    }
}
