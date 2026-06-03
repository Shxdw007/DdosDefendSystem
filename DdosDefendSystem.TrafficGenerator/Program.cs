using System.Globalization;
using System.IO;
using DdosDefendSystem.Shared;

namespace DdosDefendSystem.TrafficGenerator;

class Program
{
    private static readonly string LogFilePath = LogFilePaths.AgentAccessLog;
    private static readonly Random Rnd = new();

    static async Task Main(string[] args)
    {
        Console.Title = "DDoS Traffic Generator";
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("=========================================");
        Console.WriteLine("  Генератор трафика Nginx запущен!  ");
        Console.WriteLine($"  Цель: {LogFilePath}");
        Console.WriteLine("=========================================\n");

        Directory.CreateDirectory(Path.GetDirectoryName(LogFilePath)!);

        if (!File.Exists(LogFilePath))
        {
            await File.WriteAllTextAsync(LogFilePath, "");
        }

        while (true)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Выбери режим стрельбы:");
            Console.WriteLine("1. Обычный трафик (случайные IP, редкие запросы)");
            Console.WriteLine("2. Правило 1 — DDoS (/api/login, один IP)");
            Console.WriteLine("3. Правило 2 — Slowloris (>30 медленных запросов с одного IP)");
            Console.WriteLine("4. Правило 3 — бан подсети (5 IP из /24 шлют POST /payment)");
            Console.WriteLine("0. Выход");
            Console.Write("> ");

            var key = Console.ReadLine();

            if (key == "1") await GenerateNormalTraffic();
            else if (key == "2") await GenerateRule1Attack();
            else if (key == "3") await GenerateRule2SlowlorisAttack();
            else if (key == "4") await GenerateRule3SubnetAttack();
            else if (key == "0") break;
        }
    }

    private static string FormatResponseTime(double seconds) =>
        seconds.ToString("F2", CultureInfo.InvariantCulture);

    private static string BuildNginxLogLine(string ip, string time, string method, string uri, int status, double responseTime) =>
        $"{ip} - - [{time}] \"{method} {uri} HTTP/1.1\" {status} {FormatResponseTime(responseTime)}";

    private static async Task<(FileStream stream, StreamWriter writer)> OpenLogWriterAsync()
    {
        var stream = new FileStream(LogFilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
        var writer = new StreamWriter(stream) { AutoFlush = true };
        return (stream, writer);
    }

    private static async Task GenerateNormalTraffic()
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("\n[🚶] Запущен обычный фон. Нажми CTRL+C для остановки (или закрой окно).");

        var (stream, writer) = await OpenLogWriterAsync();
        using (stream)
        using (writer)
        {
            var uris = new[] { "/", "/about", "/images/logo.png", "/api/search" };
            var methods = new[] { "GET", "GET", "GET", "POST" };

            for (int i = 0; i < 50; i++)
            {
                string ip = $"{Rnd.Next(10, 200)}.{Rnd.Next(1, 255)}.{Rnd.Next(1, 255)}.{Rnd.Next(1, 255)}";
                string uri = uris[Rnd.Next(uris.Length)];
                string method = methods[Rnd.Next(methods.Length)];
                string time = DateTime.UtcNow.ToString("dd/MMM/yyyy:HH:mm:ss +0000", CultureInfo.InvariantCulture);
                double responseTime = Rnd.NextDouble() * 3.0;

                string logLine = BuildNginxLogLine(ip, time, method, uri, 200, responseTime);

                await writer.WriteLineAsync(logLine);
                Console.WriteLine($"[ФОН] {ip} -> {uri} ({FormatResponseTime(responseTime)}s)");

                await Task.Delay(Rnd.Next(200, 1500));
            }
        }

        Console.WriteLine("\n");
    }

    private static async Task GenerateRule1Attack()
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("\n[ПРАВИЛО 1] Один IP спамит POST /api/login (>10 запросов за 5 сек)...");

        var (stream, writer) = await OpenLogWriterAsync();
        using (stream)
        using (writer)
        {
            string attackerIp = "66.66.66.66";
            string time = DateTime.UtcNow.ToString("dd/MMM/yyyy:HH:mm:ss +0000", CultureInfo.InvariantCulture);

            for (int i = 0; i < 20; i++)
            {
                string logLine = BuildNginxLogLine(attackerIp, time, "POST", "/api/login", 200, 0.15);
                await writer.WriteLineAsync(logLine);
                Console.WriteLine($"[П1] {attackerIp} -> /api/login ({i + 1}/20)");

                await Task.Delay(50);
            }
        }

        Console.WriteLine("Атака завершена!\n");
    }

    private static async Task GenerateRule2SlowlorisAttack()
    {
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine("\n[ПРАВИЛО 2] Slowloris: один IP шлёт 35 медленных запросов (ResponseTime > 2.0)...");

        var (stream, writer) = await OpenLogWriterAsync();
        using (stream)
        using (writer)
        {
            string attackerIp = "77.77.77.77";
            string time = DateTime.UtcNow.ToString("dd/MMM/yyyy:HH:mm:ss +0000", CultureInfo.InvariantCulture);

            for (int i = 0; i < 35; i++)
            {
                double slowResponse = 2.10 + Rnd.NextDouble() * 1.5;
                string logLine = BuildNginxLogLine(attackerIp, time, "GET", "/slow-endpoint", 200, slowResponse);
                await writer.WriteLineAsync(logLine);
                Console.WriteLine($"[П2] {attackerIp} медленный запрос {FormatResponseTime(slowResponse)}s ({i + 1}/35)");

                await Task.Delay(80);
            }
        }

        Console.WriteLine("Slowloris-атака завершена! Ожидай бан IP на 15 мин.\n");
    }

    private static async Task GenerateRule3SubnetAttack()
    {
        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.WriteLine("\n[ПРАВИЛО 3] 5 уникальных IP из подсети 172.16.50.0/24 шлют POST /payment...");

        var (stream, writer) = await OpenLogWriterAsync();
        using (stream)
        using (writer)
        {
            string time = DateTime.UtcNow.ToString("dd/MMM/yyyy:HH:mm:ss +0000", CultureInfo.InvariantCulture);
            var attackerIps = new[]
            {
                "172.16.50.10",
                "172.16.50.11",
                "172.16.50.12",
                "172.16.50.13",
                "172.16.50.14"
            };

            for (int i = 0; i < attackerIps.Length; i++)
            {
                string ip = attackerIps[i];
                string logLine = BuildNginxLogLine(ip, time, "POST", "/payment", 200, 0.25);
                await writer.WriteLineAsync(logLine);
                Console.WriteLine($"[П3] {ip} -> POST /payment ({i + 1}/{attackerIps.Length})");

                await Task.Delay(100);
            }
        }

        Console.WriteLine("Атака на подсеть завершена! Ожидай бан 172.16.50.0/24 на 10 мин.\n");
    }
}
