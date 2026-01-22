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

/// <summary>
/// Response wrapper for CLOB API /book endpoint.
/// Contains orderbook data (bids and asks) for a specific market token.
/// </summary>
public class OrderbookResponse
{
    /// <summary>
    /// List of bid orders representing buy interest at various price levels.
    /// Bids are typically sorted by price descending (highest bid first).
    /// </summary>
    [JsonPropertyName("bids")]
    public List<OrderbookEntry> Bids { get; set; } = new();

    /// <summary>
    /// List of ask orders representing sell interest at various price levels.
    /// Asks are typically sorted by price ascending (lowest ask first).
    /// </summary>
    [JsonPropertyName("asks")]
    public List<OrderbookEntry> Asks { get; set; } = new();

    /// <summary>
    /// Gets the best (highest) bid price, or null if no bids exist.
    /// </summary>
    /// <returns>The highest bid price as a decimal, or null if no bids.</returns>
    public decimal? GetBestBid()
    {
        return Bids.Count > 0 ? Bids[0].GetPriceAsDecimal() : null;
    }

    /// <summary>
    /// Gets the best (lowest) ask price, or null if no asks exist.
    /// </summary>
    /// <returns>The lowest ask price as a decimal, or null if no asks.</returns>
    public decimal? GetBestAsk()
    {
        return Asks.Count > 0 ? Asks[0].GetPriceAsDecimal() : null;
    }

    /// <summary>
    /// Calculates the bid-ask spread as a decimal.
    /// Returns null if either best bid or best ask is unavailable.
    /// </summary>
    /// <returns>The spread between best ask and best bid, or null if unavailable.</returns>
    public decimal? GetSpread()
    {
        var bestBid = GetBestBid();
        var bestAsk = GetBestAsk();
        if (bestBid.HasValue && bestAsk.HasValue)
        {
            return bestAsk.Value - bestBid.Value;
        }
        return null;
    }

    /// <summary>
    /// Calculates the mid-price between best bid and best ask.
    /// Returns null if either best bid or best ask is unavailable.
    /// </summary>
    /// <returns>The mid-price as a decimal, or null if unavailable.</returns>
    public decimal? GetMidPrice()
    {
        var bestBid = GetBestBid();
        var bestAsk = GetBestAsk();
        if (bestBid.HasValue && bestAsk.HasValue)
        {
            return (bestBid.Value + bestAsk.Value) / 2;
        }
        return null;
    }
}

/// <summary>
/// Represents a single order entry in the orderbook.
/// Contains price and size as returned by the CLOB API /book endpoint.
/// </summary>
public class OrderbookEntry
{
    /// <summary>
    /// Price as a decimal string (e.g., "0.6523" representing $0.6523 or 65.23% probability).
    /// Polymarket prices range from 0 to 1, where the value represents the probability.
    /// </summary>
    [JsonPropertyName("price")]
    public string Price { get; set; } = string.Empty;

    /// <summary>
    /// Size (quantity) as a decimal string representing the amount available at this price level.
    /// </summary>
    [JsonPropertyName("size")]
    public string Size { get; set; } = string.Empty;

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
    /// Parses the size string to a decimal value.
    /// Returns null if parsing fails.
    /// </summary>
    /// <returns>Decimal value of the size, or null if parsing fails.</returns>
    public decimal? GetSizeAsDecimal()
    {
        if (decimal.TryParse(Size, NumberStyles.Any, CultureInfo.InvariantCulture, out var size))
        {
            return size;
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
