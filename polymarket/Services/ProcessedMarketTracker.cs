namespace PolymarketHistoryExporter.Services;

/// <summary>
/// Tracks already-processed markets to avoid duplicate API requests.
/// Uses file-based persistence to maintain state across application runs.
/// </summary>
public class ProcessedMarketTracker
{
    private readonly HashSet<string> _processedTokenIds;
    private readonly string _cacheFilePath;
    private readonly object _lock = new();

    /// <summary>
    /// Gets the path to the cache file.
    /// </summary>
    public string CacheFilePath => _cacheFilePath;

    /// <summary>
    /// Gets the number of processed token IDs currently tracked.
    /// </summary>
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _processedTokenIds.Count;
            }
        }
    }

    /// <summary>
    /// Initializes a new instance of the ProcessedMarketTracker.
    /// Loads existing processed token IDs from the cache file if it exists.
    /// </summary>
    /// <param name="cacheFilePath">Path to the cache file. Default is ./cache/processed_markets.txt</param>
    public ProcessedMarketTracker(string cacheFilePath = "./cache/processed_markets.txt")
    {
        _cacheFilePath = cacheFilePath;
        _processedTokenIds = new HashSet<string>();
        LoadFromFile();
    }

    /// <summary>
    /// Loads processed token IDs from the cache file.
    /// Creates the cache directory if it doesn't exist.
    /// </summary>
    private void LoadFromFile()
    {
        try
        {
            if (File.Exists(_cacheFilePath))
            {
                var lines = File.ReadAllLines(_cacheFilePath);

                lock (_lock)
                {
                    foreach (var line in lines)
                    {
                        var trimmedLine = line.Trim();
                        if (!string.IsNullOrEmpty(trimmedLine))
                        {
                            _processedTokenIds.Add(trimmedLine);
                        }
                    }
                }

                Console.WriteLine($"Loaded {_processedTokenIds.Count} processed token IDs from cache.");
            }
            else
            {
                // Create the directory if it doesn't exist
                var directory = Path.GetDirectoryName(_cacheFilePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                    Console.WriteLine($"Created cache directory: {directory}");
                }

                Console.WriteLine("No existing cache file found. Starting fresh.");
            }
        }
        catch (IOException ex)
        {
            Console.WriteLine($"Warning: Error loading cache file: {ex.Message}");
            Console.WriteLine("Starting with empty cache.");
        }
        catch (UnauthorizedAccessException ex)
        {
            Console.WriteLine($"Warning: Access denied to cache file: {ex.Message}");
            Console.WriteLine("Starting with empty cache.");
        }
    }

    /// <summary>
    /// Checks if a token ID has already been processed.
    /// </summary>
    /// <param name="tokenId">The clobTokenId to check.</param>
    /// <returns>True if the token has been processed, false otherwise.</returns>
    public bool IsProcessed(string tokenId)
    {
        if (string.IsNullOrEmpty(tokenId))
            return false;

        lock (_lock)
        {
            return _processedTokenIds.Contains(tokenId);
        }
    }

    /// <summary>
    /// Marks a token ID as processed and immediately persists to the cache file.
    /// </summary>
    /// <param name="tokenId">The clobTokenId to mark as processed.</param>
    /// <returns>True if the token was newly added, false if it was already processed.</returns>
    public bool MarkProcessed(string tokenId)
    {
        if (string.IsNullOrEmpty(tokenId))
            return false;

        bool added;

        lock (_lock)
        {
            added = _processedTokenIds.Add(tokenId);
        }

        if (added)
        {
            try
            {
                // Ensure directory exists
                var directory = Path.GetDirectoryName(_cacheFilePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Append to file immediately for persistence
                File.AppendAllText(_cacheFilePath, tokenId + Environment.NewLine);
            }
            catch (IOException ex)
            {
                Console.WriteLine($"Warning: Failed to persist processed token ID: {ex.Message}");
            }
            catch (UnauthorizedAccessException ex)
            {
                Console.WriteLine($"Warning: Access denied when persisting token ID: {ex.Message}");
            }
        }

        return added;
    }

    /// <summary>
    /// Gets all processed token IDs.
    /// </summary>
    /// <returns>A copy of all processed token IDs.</returns>
    public IReadOnlyCollection<string> GetAllProcessedIds()
    {
        lock (_lock)
        {
            return _processedTokenIds.ToList().AsReadOnly();
        }
    }

    /// <summary>
    /// Clears all processed token IDs from memory and deletes the cache file.
    /// Use with caution - this will cause all markets to be reprocessed.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _processedTokenIds.Clear();
        }

        try
        {
            if (File.Exists(_cacheFilePath))
            {
                File.Delete(_cacheFilePath);
                Console.WriteLine("Cache file cleared.");
            }
        }
        catch (IOException ex)
        {
            Console.WriteLine($"Warning: Failed to delete cache file: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            Console.WriteLine($"Warning: Access denied when deleting cache file: {ex.Message}");
        }
    }
}
