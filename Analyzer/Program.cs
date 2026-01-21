using System.Globalization;
using OfficeOpenXml;
using OfficeOpenXml.Drawing.Chart;

ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

var filePath = "../btcusdt_30m_4years.csv";
var lines = File.ReadAllLines(filePath).Skip(1).ToArray();

var oneYearAgo = DateTime.UtcNow.AddYears(-1);

var candles = lines.Select(line =>
{
    var parts = line.Split(',');
    return new
    {
        OpenTime = DateTime.Parse(parts[0], CultureInfo.InvariantCulture),
        Close = decimal.Parse(parts[4], CultureInfo.InvariantCulture)
    };
})
.Where(c => c.OpenTime >= oneYearAgo)
.ToList();

Console.WriteLine($"Загружено {candles.Count} свечей");
Console.WriteLine($"Период: {candles.First().OpenTime:yyyy-MM-dd} - {candles.Last().OpenTime:yyyy-MM-dd}");
Console.WriteLine();

using var package = new ExcelPackage();

// Лист со сводкой
var summarySheet = package.Workbook.Worksheets.Add("Сводка");
summarySheet.Cells[1, 1].Value = "Период (ч)";
summarySheet.Cells[1, 2].Value = "Измерений";
summarySheet.Cells[1, 3].Value = "Среднее ($)";
summarySheet.Cells[1, 4].Value = "Мин ($)";
summarySheet.Cells[1, 5].Value = "Макс ($)";

int summaryRow = 2;

for (int hours = 1; hours <= 24; hours++)
{
    int candleOffset = hours * 2;

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

    // Создаём лист для этого часа
    var sheetName = $"{hours}ч";
    var sheet = package.Workbook.Worksheets.Add(sheetName);

    // Заголовки
    sheet.Cells[1, 1].Value = "Изменение от ($)";
    sheet.Cells[1, 2].Value = "Изменение до ($)";
    sheet.Cells[1, 3].Value = "Диапазон";
    sheet.Cells[1, 4].Value = "Количество";
    sheet.Cells[1, 5].Value = "Вероятность (%)";

    int row = 2;
    foreach (var item in grouped)
    {
        sheet.Cells[row, 1].Value = item.RangeStart;
        sheet.Cells[row, 2].Value = item.RangeEnd;
        sheet.Cells[row, 3].Value = $"{item.RangeStart}..{item.RangeEnd}";
        sheet.Cells[row, 4].Value = item.Count;
        sheet.Cells[row, 5].Value = Math.Round(item.Percentage, 2);
        row++;
    }

    // Автоширина колонок
    sheet.Cells[1, 1, row - 1, 5].AutoFitColumns();

    // Создаём столбчатую диаграмму
    var chart = sheet.Drawings.AddChart($"Chart{hours}", eChartType.ColumnClustered);
    chart.Title.Text = $"Распределение изменений цены за {hours} ч";
    chart.SetPosition(1, 0, 6, 0);
    chart.SetSize(800, 400);

    var dataRange = sheet.Cells[2, 5, row - 1, 5];
    var labelRange = sheet.Cells[2, 3, row - 1, 3];

    var series = chart.Series.Add(dataRange, labelRange);
    series.Header = "Вероятность (%)";

    chart.XAxis.Title.Text = "Изменение цены ($)";
    chart.YAxis.Title.Text = "Вероятность (%)";

    // Статистика
    var avgChange = priceChanges.Average();
    var minChange = priceChanges.Min();
    var maxChange = priceChanges.Max();

    // Добавляем в сводку
    summarySheet.Cells[summaryRow, 1].Value = hours;
    summarySheet.Cells[summaryRow, 2].Value = priceChanges.Count;
    summarySheet.Cells[summaryRow, 3].Value = Math.Round(avgChange, 2);
    summarySheet.Cells[summaryRow, 4].Value = Math.Round(minChange, 2);
    summarySheet.Cells[summaryRow, 5].Value = Math.Round(maxChange, 2);
    summaryRow++;

    Console.WriteLine($"{hours,2} ч: создан лист с диаграммой ({grouped.Count} интервалов)");
}

summarySheet.Cells[1, 1, summaryRow - 1, 5].AutoFitColumns();

var outputPath = "../btc_price_changes_analysis_v2.xlsx";
package.SaveAs(new FileInfo(outputPath));

Console.WriteLine();
Console.WriteLine($"Сохранено: {outputPath}");
Console.WriteLine("Файл содержит 24 листа с диаграммами (по одному на каждый час) + лист Сводка");
Console.WriteLine("Период: последний год");
