namespace PolymarketHistoryExporter.Models;

/// <summary>
/// Represents a single price record for CSV export.
/// Contains timestamp, market name, and price/probability data.
/// </summary>
public class MarketPriceRecord
{
    /// <summary>
    /// The timestamp when this price was recorded.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// The name or title of the market (e.g., "Bitcoin price on January 15, 2024").
    /// </summary>
    public string MarketName { get; set; } = string.Empty;

    /// <summary>
    /// The raw price as a decimal value (0-1 range).
    /// Represents the cost of a "Yes" share on Polymarket.
    /// For example, 0.65 means $0.65 per share.
    /// </summary>
    public decimal Price { get; set; }

    /// <summary>
    /// The probability as a percentage (0-100 range).
    /// Calculated as Price * 100.
    /// For example, a price of 0.65 becomes 65.0%.
    /// </summary>
    public decimal Probability => Price * 100;
}
