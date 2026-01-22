using System.Text.Json.Serialization;

namespace PolymarketHistoryExporter.Models;

/// <summary>
/// Wrapper for Gamma API paginated responses.
/// </summary>
/// <typeparam name="T">The type of items in the data array.</typeparam>
public class GammaApiResponse<T>
{
    [JsonPropertyName("data")]
    public List<T> Data { get; set; } = new();

    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("next_offset")]
    public int? NextOffset { get; set; }
}

/// <summary>
/// Represents an event from the Gamma API.
/// Events contain one or more related markets.
/// </summary>
public class GammaEvent
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("slug")]
    public string Slug { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("markets")]
    public List<GammaMarket> Markets { get; set; } = new();

    [JsonPropertyName("closed")]
    public bool Closed { get; set; }

    [JsonPropertyName("archived")]
    public bool Archived { get; set; }

    [JsonPropertyName("startDate")]
    public string? StartDate { get; set; }

    [JsonPropertyName("endDate")]
    public string? EndDate { get; set; }
}

/// <summary>
/// Represents a market within an event from the Gamma API.
/// Each market has outcomes (typically Yes/No) with corresponding clobTokenIds.
/// </summary>
public class GammaMarket
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("question")]
    public string Question { get; set; } = string.Empty;

    [JsonPropertyName("conditionId")]
    public string ConditionId { get; set; } = string.Empty;

    [JsonPropertyName("slug")]
    public string Slug { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Token IDs for the CLOB API. Order corresponds to the outcomes array.
    /// Typically: [0] = Yes token, [1] = No token.
    /// </summary>
    [JsonPropertyName("clobTokenIds")]
    public List<string> ClobTokenIds { get; set; } = new();

    /// <summary>
    /// Outcome labels corresponding to clobTokenIds.
    /// Typically: ["Yes", "No"].
    /// </summary>
    [JsonPropertyName("outcomes")]
    public List<string> Outcomes { get; set; } = new();

    [JsonPropertyName("outcomePrices")]
    public List<string> OutcomePrices { get; set; } = new();

    [JsonPropertyName("closed")]
    public bool Closed { get; set; }

    [JsonPropertyName("archived")]
    public bool Archived { get; set; }

    [JsonPropertyName("active")]
    public bool Active { get; set; }

    [JsonPropertyName("volume")]
    public string Volume { get; set; } = string.Empty;

    [JsonPropertyName("startDate")]
    public string? StartDate { get; set; }

    [JsonPropertyName("endDate")]
    public string? EndDate { get; set; }

    /// <summary>
    /// Gets the clobTokenId for the "Yes" outcome.
    /// Returns null if not found.
    /// </summary>
    public string? GetYesTokenId()
    {
        var yesIndex = Outcomes.FindIndex(o =>
            o.Equals("Yes", StringComparison.OrdinalIgnoreCase));

        if (yesIndex >= 0 && yesIndex < ClobTokenIds.Count)
        {
            return ClobTokenIds[yesIndex];
        }

        return null;
    }

    /// <summary>
    /// Gets the clobTokenId for the "No" outcome.
    /// Returns null if not found.
    /// </summary>
    public string? GetNoTokenId()
    {
        var noIndex = Outcomes.FindIndex(o =>
            o.Equals("No", StringComparison.OrdinalIgnoreCase));

        if (noIndex >= 0 && noIndex < ClobTokenIds.Count)
        {
            return ClobTokenIds[noIndex];
        }

        return null;
    }
}
