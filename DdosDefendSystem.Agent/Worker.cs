using System.Net.Http.Json;
using DdosDefendSystem.Agent.Services;
using DdosDefendSystem.Shared.Models;

namespace DdosDefendSystem.Agent;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly NginxLogParser _parser;
    private readonly IHttpClientFactory _httpClientFactory;
    private const string LogFilePath = "test_access.log";

    // Добавили IHttpClientFactory в конструктор
    public Worker(ILogger<Worker> logger, NginxLogParser parser, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _parser = parser;
        _httpClientFactory = httpClientFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DDoS Agent запущен. Читаем лог: {Path}", LogFilePath);

        if (!File.Exists(LogFilePath))
        {
            await File.WriteAllTextAsync(LogFilePath, "", stoppingToken);
        }

        using var stream = new FileStream(LogFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        stream.Seek(0, SeekOrigin.End);

        // Создаем клиента для отправки данных
        var httpClient = _httpClientFactory.CreateClient("CoordinatorClient");

        while (!stoppingToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(stoppingToken);

            if (line != null)
            {
                var logEvent = _parser.Parse(line);

                if (logEvent != null)
                {
                    _logger.LogInformation("Пойман запрос: IP: {Ip} | Метод: {Method} | URI: {Uri}",
                        logEvent.IpAddress, logEvent.HttpMethod, logEvent.Uri);

                    var payload = new List<RequestLog> { logEvent };

                    try
                    {
                        var response = await httpClient.PostAsJsonAsync("/api/logs", payload, stoppingToken);

                        if (response.IsSuccessStatusCode)
                        {
                            _logger.LogInformation("Лог успешно отправлен Координатору!");
                        }
                        else
                        {
                            _logger.LogWarning("Ошибка отправки: Координатор вернул статус {Status}", response.StatusCode);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError("Сервер Координатора недоступен: {Message}", ex.Message);
                    }
                }
            }
            else
            {
                await Task.Delay(500, stoppingToken);
            }
        }
    }
}