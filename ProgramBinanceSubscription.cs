using BinanceDataLoader.Models;
using BinanceDataLoader.Services;

namespace BinanceDataLoader;

/// <summary>
/// Entry point for real-time BTC price subscription from Binance via WebSocket.
/// </summary>
public class ProgramBinanceSubscription
{
    private static readonly CancellationTokenSource _cts = new();
    private static BinancePriceService? _priceService;

    public static async Task Main(string[] args)
    {
        Console.WriteLine("=".PadRight(50, '='));
        Console.WriteLine("Binance BTC Price Subscription");
        Console.WriteLine("=".PadRight(50, '='));
        Console.WriteLine();

        // Setup Ctrl+C handler for graceful shutdown
        Console.CancelKeyPress += OnCancelKeyPress;
        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;

        try
        {
            _priceService = new BinancePriceService();

            // Subscribe to status changes
            _priceService.OnStatusChanged += message =>
            {
                Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] {message}");
            };

            // Subscribe to price updates
            _priceService.OnPriceUpdate += OnPriceUpdate;

            // Start the subscription
            var success = await _priceService.SubscribeToTickerAsync("BTCUSDT");

            if (!success)
            {
                Console.WriteLine("Failed to establish connection. Exiting...");
                return;
            }

            Console.WriteLine();
            Console.WriteLine("Press Ctrl+C to exit...");
            Console.WriteLine();

            // Keep the application running until cancellation
            try
            {
                await Task.Delay(Timeout.Infinite, _cts.Token);
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
        finally
        {
            await ShutdownAsync();
        }
    }

    /// <summary>
    /// Handles incoming price updates and displays them in the console.
    /// </summary>
    private static void OnPriceUpdate(PriceUpdate update)
    {
        var changeSymbol = update.PriceChangePercent >= 0 ? "+" : "";
        Console.WriteLine($"[{update.Timestamp:yyyy-MM-dd HH:mm:ss}] BTC: ${update.Price:N2} ({changeSymbol}{update.PriceChangePercent:N2}%)");
    }

    /// <summary>
    /// Handles Ctrl+C for graceful shutdown.
    /// </summary>
    private static void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
    {
        e.Cancel = true; // Prevent immediate termination
        Console.WriteLine();
        Console.WriteLine("Shutdown requested...");
        _cts.Cancel();
    }

    /// <summary>
    /// Handles process exit event.
    /// </summary>
    private static void OnProcessExit(object? sender, EventArgs e)
    {
        ShutdownAsync().GetAwaiter().GetResult();
    }

    /// <summary>
    /// Performs graceful shutdown of the price service.
    /// </summary>
    private static async Task ShutdownAsync()
    {
        if (_priceService != null)
        {
            await _priceService.CloseAsync();
            _priceService.Dispose();
            _priceService = null;
        }

        Console.WriteLine("Application terminated.");
    }
}
