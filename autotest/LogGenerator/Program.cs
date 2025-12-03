using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Globalization;

string outputDir = "./log";
string fileName = "live_app.log";
double errFrequency = 0.4; // 20% ошибок
int durationMinutes = 5;
int intervalSeconds = 2;

// Парсим аргументы (опционально)
for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--output-dir" && i + 1 < args.Length) outputDir = args[++i];
    else if (args[i] == "--file-name" && i + 1 < args.Length) fileName = args[++i];
    else if (args[i] == "--err-frequency" && i + 1 < args.Length)
        errFrequency = double.Parse(args[++i], CultureInfo.InvariantCulture);
    else if (args[i] == "--duration" && i + 1 < args.Length) durationMinutes = int.Parse(args[++i]);
    else if (args[i] == "--interval" && i + 1 < args.Length) intervalSeconds = int.Parse(args[++i]);
}

Directory.CreateDirectory(outputDir);
string filePath = Path.Combine(outputDir, fileName);

Console.WriteLine($"🟢 Запуск 'живого' генератора логов");
Console.WriteLine($"📁 Файл: {filePath}");
Console.WriteLine($"⏱️  Длительность: {durationMinutes} мин, интервал: {intervalSeconds} сек");
Console.WriteLine($"📉 Частота ERR: {errFrequency:P0}");
Console.WriteLine($"⏹️  Нажмите Ctrl+C чтобы остановить досрочно\n");

var cts = new CancellationTokenSource();
var token = cts.Token;

// Обработка Ctrl+C
Console.CancelKeyPress += (sender, e) =>
{
    e.Cancel = true;
    cts.Cancel();
    Console.WriteLine("\n🛑 Остановка по запросу...");
};

try
{
    var endTime = DateTime.Now.AddMinutes(durationMinutes);
    var random = new Random();

    while (DateTime.Now < endTime && !token.IsCancellationRequested)
    {
        string level = "INF";
        double r = random.NextDouble();
        if (r < errFrequency)
            level = "ERR";
        else if (r < errFrequency + 0.1)
            level = "DGB";

        string timestamp = DateTime.Now.ToString("yyyy:MM:dd HH:mm");
        string line = $"{timestamp} [{level}] - Live log message at {DateTime.Now:HH:mm:ss}";

        // Добавляем в файл (append)
        File.AppendAllText(filePath, line + Environment.NewLine);

        Console.WriteLine($"📄 Записано: {line}");

        // Ждём, но прерываемся при отмене
        await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), token);
    }
}
catch (OperationCanceledException)
{
    // Нормально при Ctrl+C
}
finally
{
    cts.Dispose();
}

Console.WriteLine($"\n✅ Генерация завершена. Лог: {filePath}");