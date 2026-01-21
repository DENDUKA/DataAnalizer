using System.Globalization;
using System.Text.Json.Serialization;

namespace PolymarketHistoryExporter.Models;

/// <summary>
/// Response wrapper for CLOB API /prices-history endpoint.
/// Contains historical price data for a specific market token.
/// </summary>
public class PriceHistoryResponse
{
    /// <summary>
    /// Array of price points representing historical price data.
    /// </summary>
    [JsonPropertyName("history")]
    public List<PricePoint> History { get; set; } = new();
}

/// <summary>
/// Represents a single price point in the price history.
/// Contains Unix timestamp and price as returned by the CLOB API.
/// </summary>
public class PricePoint
{
    /// <summary>
    /// Unix timestamp in seconds when this price was recorded.
    /// </summary>
    [JsonPropertyName("t")]
    public long Timestamp { get; set; }

    /// <summary>
    /// Price as a decimal string (e.g., "0.6523" representing $0.6523 or 65.23% probability).
    /// Polymarket prices range from 0 to 1, where the value represents the probability.
    /// </summary>
    [JsonPropertyName("p")]
    public string Price { get; set; } = string.Empty;

    /// <summary>
    /// Converts the Unix timestamp to a UTC DateTime.
    /// </summary>
    /// <returns>DateTime in UTC representing when this price was recorded.</returns>
    public DateTime GetTimestampAsUtc()
    {
        return DateTimeOffset.FromUnixTimeSeconds(Timestamp).UtcDateTime;
    }

    /// <summary>
    /// Converts the Unix timestamp to a DateTime with the specified UTC offset.
    /// </summary>
    /// <param name="utcOffset">The UTC offset to apply.</param>
    /// <returns>DateTime with the specified offset.</returns>
    public DateTime GetTimestampWithOffset(TimeSpan utcOffset)
    {
        return DateTimeOffset.FromUnixTimeSeconds(Timestamp).UtcDateTime + utcOffset;
    }

    /// <summary>
    /// Parses the price string to a decimal value.
    /// Returns null if parsing fails.
    /// </summary>
    /// <returns>Decimal value of the price (0-1 range), or null if parsing fails.</returns>
    public decimal? GetPriceAsDecimal()
    {
        if (decimal.TryParse(Price, NumberStyles.Any, CultureInfo.InvariantCulture, out var price))
        {
            return price;
        }

        return null;
    }

    /// <summary>
    /// Converts the price to a probability percentage (0-100 range).
    /// Returns null if price parsing fails.
    /// </summary>
    /// <returns>Probability as a percentage (e.g., 65.23 for 65.23%), or null if parsing fails.</returns>
    public decimal? GetProbabilityPercent()
    {
        var price = GetPriceAsDecimal();
        return price.HasValue ? price.Value * 100 : null;
    }
}
