using Binance.Net.Clients;
using Binance.Net.Objects.Models.Spot.Socket;
using BinanceDataLoader.Models;
using CryptoExchange.Net.Objects.Sockets;

namespace BinanceDataLoader.Services;

/// <summary>
/// Service for subscribing to real-time BTC price updates from Binance via WebSocket.
/// </summary>
public class BinancePriceService : IDisposable
{
    private readonly BinanceSocketClient _socketClient;
    private UpdateSubscription? _subscription;
    private bool _disposed;

    /// <summary>
    /// Event raised when a new price update is received.
    /// </summary>
    public event Action<PriceUpdate>? OnPriceUpdate;

    /// <summary>
    /// Event raised when connection status changes.
    /// </summary>
    public event Action<string>? OnStatusChanged;

    /// <summary>
    /// Initializes a new instance of the BinancePriceService.
    /// </summary>
    public BinancePriceService()
    {
        _socketClient = new BinanceSocketClient(options =>
        {
            options.AutoReconnect = true;
            options.ReconnectInterval = TimeSpan.FromSeconds(5);
        });
    }

    /// <summary>
    /// Subscribes to ticker updates for the specified symbol.
    /// </summary>
    /// <param name="symbol">Trading pair symbol (e.g., "BTCUSDT").</param>
    /// <returns>True if subscription was successful, false otherwise.</returns>
    public async Task<bool> SubscribeToTickerAsync(string symbol = "BTCUSDT")
    {
        try
        {
            OnStatusChanged?.Invoke($"Connecting to Binance WebSocket for {symbol}...");

            var result = await _socketClient.SpotApi.ExchangeData.SubscribeToTickerUpdatesAsync(
                symbol,
                OnTickerUpdate
            );

            if (!result.Success)
            {
                OnStatusChanged?.Invoke($"Failed to subscribe: {result.Error?.Message ?? "Unknown error"}");
                return false;
            }

            _subscription = result.Data;

            // Wire up connection events
            _subscription.ConnectionLost += () =>
            {
                OnStatusChanged?.Invoke("WebSocket connection lost. Attempting to reconnect...");
            };

            _subscription.ConnectionRestored += (time) =>
            {
                OnStatusChanged?.Invoke($"WebSocket reconnected at {time:yyyy-MM-dd HH:mm:ss}");
            };

            OnStatusChanged?.Invoke($"Successfully subscribed to {symbol} ticker updates");
            return true;
        }
        catch (Exception ex)
        {
            OnStatusChanged?.Invoke($"Error subscribing to ticker: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Handles incoming ticker update data from Binance.
    /// </summary>
    private void OnTickerUpdate(CryptoExchange.Net.Objects.DataEvent<IBinanceTick> data)
    {
        try
        {
            var tick = data.Data;
            var priceUpdate = new PriceUpdate
            {
                Symbol = tick.Symbol,
                Price = tick.LastPrice,
                Timestamp = DateTime.UtcNow,
                PriceChangePercent = tick.PriceChangePercent,
                Volume = tick.Volume
            };

            OnPriceUpdate?.Invoke(priceUpdate);
        }
        catch (Exception ex)
        {
            OnStatusChanged?.Invoke($"Error processing ticker update: {ex.Message}");
        }
    }

    /// <summary>
    /// Closes the WebSocket subscription gracefully.
    /// </summary>
    public async Task CloseAsync()
    {
        try
        {
            OnStatusChanged?.Invoke("Closing WebSocket connection...");

            if (_subscription != null)
            {
                await _subscription.CloseAsync();
                _subscription = null;
            }

            OnStatusChanged?.Invoke("WebSocket connection closed");
        }
        catch (Exception ex)
        {
            OnStatusChanged?.Invoke($"Error closing connection: {ex.Message}");
        }
    }

    /// <summary>
    /// Disposes resources used by the service.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        // Synchronously close if still subscribed
        if (_subscription != null)
        {
            _subscription.CloseAsync().GetAwaiter().GetResult();
            _subscription = null;
        }

        _socketClient.Dispose();
        GC.SuppressFinalize(this);
    }
}
