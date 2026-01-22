namespace BinanceDataLoader.Models;

/// <summary>
/// Represents a real-time price update from Binance WebSocket stream.
/// </summary>
public class PriceUpdate
{
    /// <summary>
    /// Trading pair symbol (e.g., "BTCUSDT").
    /// </summary>
    public string Symbol { get; set; } = string.Empty;

    /// <summary>
    /// Current price of the asset.
    /// </summary>
    public decimal Price { get; set; }

    /// <summary>
    /// Timestamp when the price update was received.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// 24-hour price change percentage.
    /// </summary>
    public decimal PriceChangePercent { get; set; }

    /// <summary>
    /// 24-hour trading volume.
    /// </summary>
    public decimal Volume { get; set; }
}
