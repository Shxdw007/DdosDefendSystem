using System.Net.Http.Json;
using DdosDefendSystem.Agent.Services;
using DdosDefendSystem.Shared;
using DdosDefendSystem.Shared.Models;

namespace DdosDefendSystem.Agent;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly NginxLogParser _parser;
    private readonly IHttpClientFactory _httpClientFactory;
    private static readonly string LogFilePath = LogFilePaths.AgentAccessLog;

    public Worker(ILogger<Worker> logger, NginxLogParser parser, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _parser = parser;
        _httpClientFactory = httpClientFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DDoS Agent запущен. Читаем лог: {Path}", LogFilePath);

        EnsureDevLogFileExists();

        var httpClient = _httpClientFactory.CreateClient("CoordinatorClient");

        while (!stoppingToken.IsCancellationRequested)
        {
            if (!File.Exists(LogFilePath))
            {
                _logger.LogWarning("Файл лога не найден: {Path}. Повтор через 5 сек...", LogFilePath);
                await Task.Delay(5000, stoppingToken);
                continue;
            }

            try
            {
                await TailLogFileAsync(httpClient, stoppingToken);
            }
            catch (IOException ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "Ошибка чтения лога. Повтор через 2 сек...");
                await Task.Delay(2000, stoppingToken);
            }
        }
    }

    private static void EnsureDevLogFileExists()
    {
        if (LogFilePaths.IsSystemManagedLog(LogFilePath))
            return;

        var directory = Path.GetDirectoryName(LogFilePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        if (!File.Exists(LogFilePath))
            File.WriteAllText(LogFilePath, string.Empty);
    }

    private async Task TailLogFileAsync(HttpClient httpClient, CancellationToken stoppingToken)
    {
        using var stream = new FileStream(LogFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        stream.Seek(0, SeekOrigin.End);

        while (!stoppingToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(stoppingToken);

            if (line == null)
            {
                await Task.Delay(500, stoppingToken);
                continue;
            }

            await ProcessLineAsync(line, httpClient, stoppingToken);
        }
    }

    private async Task ProcessLineAsync(string line, HttpClient httpClient, CancellationToken stoppingToken)
    {
        RequestLog? logEvent;

        try
        {
            logEvent = _parser.Parse(line);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ошибка парсинга строки лога: {Line}", line);
            return;
        }

        if (logEvent == null)
        {
            _logger.LogWarning("Не удалось распарсить строку лога: {Line}", line);
            return;
        }

        _logger.LogInformation(
            "Пойман запрос: IP: {Ip} | Метод: {Method} | URI: {Uri} | ResponseTime: {ResponseTime}",
            logEvent.IpAddress, logEvent.HttpMethod, logEvent.Uri, logEvent.ResponseTime);

        try
        {
            var response = await httpClient.PostAsJsonAsync("/api/logs", new List<RequestLog> { logEvent }, stoppingToken);

            if (response.IsSuccessStatusCode)
                _logger.LogInformation("Лог успешно отправлен Координатору!");
            else
                _logger.LogWarning("Ошибка отправки: Координатор вернул статус {Status}", response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError("Сервер Координатора недоступен: {Message}", ex.Message);
        }
    }
}
