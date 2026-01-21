namespace PolymarketHistoryExporter.Utils;

/// <summary>
/// Provides rate limiting functionality to prevent overwhelming APIs with requests.
/// Ensures a minimum delay between consecutive requests.
/// </summary>
public class RateLimiter
{
    private readonly int _delayMs;
    private DateTime _lastRequest;
    private readonly object _lock = new();

    /// <summary>
    /// Gets the configured delay in milliseconds between requests.
    /// </summary>
    public int DelayMs => _delayMs;

    /// <summary>
    /// Initializes a new instance of the RateLimiter.
    /// </summary>
    /// <param name="delayMs">Minimum delay in milliseconds between requests. Default is 500ms.</param>
    public RateLimiter(int delayMs = 500)
    {
        _delayMs = delayMs;
        _lastRequest = DateTime.MinValue;
    }

    /// <summary>
    /// Waits asynchronously if needed to ensure the minimum delay between requests.
    /// Call this method before each API request.
    /// </summary>
    /// <returns>A task that completes when the rate limit delay has passed.</returns>
    public async Task WaitAsync()
    {
        int waitMs;

        lock (_lock)
        {
            var elapsed = DateTime.UtcNow - _lastRequest;
            var elapsedMs = (int)elapsed.TotalMilliseconds;

            if (elapsedMs < _delayMs)
            {
                waitMs = _delayMs - elapsedMs;
            }
            else
            {
                waitMs = 0;
            }

            // Update last request time after calculating wait
            // This ensures the next request will be properly delayed
            _lastRequest = DateTime.UtcNow.AddMilliseconds(waitMs);
        }

        if (waitMs > 0)
        {
            await Task.Delay(waitMs);
        }
    }

    /// <summary>
    /// Resets the rate limiter, allowing the next request to proceed immediately.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _lastRequest = DateTime.MinValue;
        }
    }
}
