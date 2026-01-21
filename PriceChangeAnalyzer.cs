using System.Globalization;

var filePath = "btcusdt_30m_4years.csv";
var lines = File.ReadAllLines(filePath).Skip(1).ToArray(); // Пропускаем заголовок

var candles = lines.Select(line =>
{
    var parts = line.Split(',');
    return new
    {
        OpenTime = DateTime.Parse(parts[0], CultureInfo.InvariantCulture),
        Close = decimal.Parse(parts[4], CultureInfo.InvariantCulture)
    };
}).ToList();

Console.WriteLine($"Загружено {candles.Count} свечей");
Console.WriteLine($"Период: {candles.First().OpenTime:yyyy-MM-dd} - {candles.Last().OpenTime:yyyy-MM-dd}");
Console.WriteLine();

// Анализ для периодов от 1 до 24 часов
// 30-минутные свечи: 1 час = 2 свечи
for (int hours = 1; hours <= 24; hours++)
{
    int candleOffset = hours * 2; // количество 30-мин свечей в периоде

    var priceChanges = new List<decimal>();

    for (int i = 0; i < candles.Count - candleOffset; i++)
    {
        var startPrice = candles[i].Close;
        var endPrice = candles[i + candleOffset].Close;
        var change = endPrice - startPrice;
        priceChanges.Add(change);
    }

    // Группируем по интервалам $100
    var grouped = priceChanges
        .GroupBy(change => (int)Math.Floor(change / 100) * 100)
        .OrderBy(g => g.Key)
        .Select(g => new
        {
            RangeStart = g.Key,
            RangeEnd = g.Key + 100,
            Count = g.Count(),
            Percentage = (double)g.Count() / priceChanges.Count * 100
        })
        .ToList();

    Console.WriteLine($"══════════════════════════════════════════════════════════════");
    Console.WriteLine($"  ПЕРИОД: {hours} ч. | Измерений: {priceChanges.Count:N0}");
    Console.WriteLine($"══════════════════════════════════════════════════════════════");
    Console.WriteLine($"{"Изменение цены ($)",-25} {"Количество",-15} {"Вероятность",-10}");
    Console.WriteLine($"{"─────────────────────────",-25} {"───────────────",-15} {"──────────",-10}");

    foreach (var item in grouped)
    {
        var range = item.RangeStart >= 0
            ? $"+{item.RangeStart,6} .. +{item.RangeEnd,6}"
            : item.RangeEnd <= 0
                ? $"{item.RangeStart,7} .. {item.RangeEnd,7}"
                : $"{item.RangeStart,7} .. +{item.RangeEnd,6}";

        Console.WriteLine($"{range,-25} {item.Count,-15:N0} {item.Percentage,8:F2}%");
    }

    // Статистика
    var avgChange = priceChanges.Average();
    var minChange = priceChanges.Min();
    var maxChange = priceChanges.Max();

    Console.WriteLine();
    Console.WriteLine($"  Среднее изменение: {avgChange:+#,##0.00;-#,##0.00;0} $");
    Console.WriteLine($"  Мин: {minChange:+#,##0.00;-#,##0.00;0} $ | Макс: {maxChange:+#,##0.00;-#,##0.00;0} $");
    Console.WriteLine();
}
