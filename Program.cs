using System.Globalization;
using System.Text.Json;
using CsvHelper;
using CsvHelper.Configuration;

var symbols = new[] { "BTCUSDT", "ETHUSDT", "SOLUSDT", "XRPUSDT" };
const string Interval = "30m";
const int LimitPerRequest = 1000;
var utcOffset = TimeSpan.FromHours(4);

var httpClient = new HttpClient();
httpClient.BaseAddress = new Uri("https://api.binance.com");

var endTime = DateTimeOffset.UtcNow;
var startTime = endTime.AddYears(-4);

foreach (var symbol in symbols)
{
    Console.WriteLine($"\n{'=',-50}");
    Console.WriteLine($"Загрузка данных {symbol} с {startTime:yyyy-MM-dd} по {endTime:yyyy-MM-dd}");
    Console.WriteLine($"Таймфрейм: {Interval}");

    var allCandles = new List<Candle>();
    var currentStart = startTime;

    while (currentStart < endTime)
    {
        var startMs = currentStart.ToUnixTimeMilliseconds();
        var endMs = endTime.ToUnixTimeMilliseconds();

        var url = $"/api/v3/klines?symbol={symbol}&interval={Interval}&startTime={startMs}&endTime={endMs}&limit={LimitPerRequest}";

        try
        {
            var response = await httpClient.GetStringAsync(url);
            var data = JsonSerializer.Deserialize<JsonElement[]>(response);

            if (data == null || data.Length == 0)
                break;

            foreach (var item in data)
            {
                var candle = new Candle
                {
                    OpenTime = DateTimeOffset.FromUnixTimeMilliseconds(item[0].GetInt64()).UtcDateTime + utcOffset,
                    Open = decimal.Parse(item[1].GetString()!, CultureInfo.InvariantCulture),
                    High = decimal.Parse(item[2].GetString()!, CultureInfo.InvariantCulture),
                    Low = decimal.Parse(item[3].GetString()!, CultureInfo.InvariantCulture),
                    Close = decimal.Parse(item[4].GetString()!, CultureInfo.InvariantCulture),
                    Volume = decimal.Parse(item[5].GetString()!, CultureInfo.InvariantCulture),
                    CloseTime = DateTimeOffset.FromUnixTimeMilliseconds(item[6].GetInt64()).UtcDateTime + utcOffset,
                    QuoteVolume = decimal.Parse(item[7].GetString()!, CultureInfo.InvariantCulture),
                    TradesCount = item[8].GetInt32()
                };
                allCandles.Add(candle);
            }

            var lastCandleTime = data[^1][6].GetInt64();
            currentStart = DateTimeOffset.FromUnixTimeMilliseconds(lastCandleTime + 1);

            Console.Write($"\r{symbol}: загружено свечей: {allCandles.Count}");

            await Task.Delay(100);
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"\nОшибка запроса: {ex.Message}");
            Console.WriteLine("Повторная попытка через 5 секунд...");
            await Task.Delay(5000);
        }
    }

    Console.WriteLine($"\n{symbol}: всего загружено {allCandles.Count} свечей");

    var outputFile = $"{symbol.ToLower()}_30m_4years.csv";
    var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
    {
        HasHeaderRecord = true
    };

    await using var writer = new StreamWriter(outputFile);
    await using var csv = new CsvWriter(writer, csvConfig);

    await csv.WriteRecordsAsync(allCandles);

    var fileInfo = new FileInfo(outputFile);
    Console.WriteLine($"Сохранено в: {outputFile} ({fileInfo.Length / 1024.0 / 1024.0:F2} МБ)");
}

public class Candle
{
    public DateTime OpenTime { get; set; }
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public decimal Volume { get; set; }
    public DateTime CloseTime { get; set; }
    public decimal QuoteVolume { get; set; }
    public int TradesCount { get; set; }
}
