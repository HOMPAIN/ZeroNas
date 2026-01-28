using Microsoft.Extensions.Logging;

namespace BlazorWebSSD
{
    public class WiFiMonitoringService : BackgroundService
    {
        private readonly ILogger<WiFiMonitoringService> _logger;

        public WiFiMonitoringService(ILogger<WiFiMonitoringService> logger)
        {
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("WiFi Monitoring Service запущен.");

            while (!stoppingToken.IsCancellationRequested)
            {
                
                try
                {
                    // Проверяем текущее подключение
                    var activeSsid = WiFiManager.GetCurrentActiveWiFiConnectionName();

                    if (activeSsid == null)
                        _logger.LogInformation(null);
                    else
                        _logger.LogInformation(activeSsid);

                    if (string.IsNullOrEmpty(activeSsid))
                    {
                        _logger.LogWarning("Активное Wi-Fi подключение отсутствует.");

                        // Создаём точку доступа
                        /*bool success = WiFiManager.CreateOpenHotspot("MyWiFi");

                        if (success)
                        {
                            _logger.LogInformation("Точка доступа 'MyWiFi' успешно создана.");
                        }
                        else
                        {
                            _logger.LogError("Не удалось создать точку доступа.");
                        }*/
                    }
                    else
                    {
                        _logger.LogDebug($"Подключено к: {activeSsid}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка в WiFi Monitoring Service");
                }

                // Ждём 30 секунд перед следующей проверкой
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }
    }
}
