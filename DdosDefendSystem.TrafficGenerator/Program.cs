using System;
using System.IO;
using System.Threading.Tasks;

namespace DdosDefendSystem.TrafficGenerator;

class Program
{
    private const string LogFilePath = @"C:\Users\Shxdw\source\repos\DdosDefendSystem\DdosDefendSystem.Agent\test_access.log";
    private static readonly Random Rnd = new();

    static async Task Main(string[] args)
    {
        Console.Title = "DDoS Traffic Generator";
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("=========================================");
        Console.WriteLine("  Генератор трафика Nginx запущен!  ");
        Console.WriteLine($"  Цель: {LogFilePath}");
        Console.WriteLine("=========================================\n");

        if (!File.Exists(LogFilePath))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Файл логов не найден! Убедись, что Агент запущен хотя бы один раз.");
            Console.ResetColor();
            return;
        }

        while (true)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Выбери режим стрельбы:");
            Console.WriteLine("1. Обычный трафик (случайные IP, редкие запросы)");
            Console.WriteLine("2. DDoS АТАКА (один IP спамит /api/login)");
            Console.WriteLine("0. Выход");
            Console.Write("> ");

            var key = Console.ReadLine();

            if (key == "1") await GenerateNormalTraffic();
            else if (key == "2") await GenerateDdosAttack();
            else if (key == "0") break;
        }
    }

    private static async Task GenerateNormalTraffic()
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("\n[🚶] Запущен обычный фон. Нажми CTRL+C для остановки (или закрой окно).");

        using var stream = new FileStream(LogFilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
        using var writer = new StreamWriter(stream);
        writer.AutoFlush = true;

        var uris = new[] { "/", "/about", "/images/logo.png", "/api/search" };
        var methods = new[] { "GET", "GET", "GET", "POST" };

        for (int i = 0; i < 50; i++) 
        {
            string ip = $"{Rnd.Next(10, 200)}.{Rnd.Next(1, 255)}.{Rnd.Next(1, 255)}.{Rnd.Next(1, 255)}";
            string uri = uris[Rnd.Next(uris.Length)];
            string method = methods[Rnd.Next(methods.Length)];
            string time = DateTime.UtcNow.ToString("dd/MMM/yyyy:HH:mm:ss +0000", new System.Globalization.CultureInfo("en-US"));

            string logLine = $"{ip} - - [{time}] \"{method} {uri} HTTP/1.1\" 200 {Rnd.NextDouble():F2}";

            await writer.WriteLineAsync(logLine);
            Console.WriteLine($"[ФОН] {ip} -> {uri}");

            await Task.Delay(Rnd.Next(200, 1500)); 
        }
        Console.WriteLine("\n");
    }

    private static async Task GenerateDdosAttack()
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("\n[🚀] ВНИМАНИЕ! ЗАПУСК DDoS АТАКИ...");

        using var stream = new FileStream(LogFilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
        using var writer = new StreamWriter(stream);
        writer.AutoFlush = true;

        string attackerIp = "66.66.66.66"; 
        string time = DateTime.UtcNow.ToString("dd/MMM/yyyy:HH:mm:ss +0000", new System.Globalization.CultureInfo("en-US"));

        for (int i = 0; i < 20; i++)
        {
            string logLine = $"{attackerIp} - - [{time}] \"POST /api/login HTTP/1.1\" 200 0.15";
            await writer.WriteLineAsync(logLine);
            Console.WriteLine($"[АТАКА] {attackerIp} спамит /api/login ({i + 1}/20)");

            await Task.Delay(50); 
        }
        Console.WriteLine("Атака завершена!\n");
    }
}