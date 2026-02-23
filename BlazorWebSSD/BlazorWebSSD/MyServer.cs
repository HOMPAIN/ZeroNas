using System.ComponentModel;

namespace BlazorWebSSD
{
    public class MyServer
    {
        private readonly ILogger<MyServer> _logger;
        private CancellationTokenSource? _cts;

        public MyServer(ILogger<MyServer> logger)
        {
            _logger = logger;
        }

        public bool IsRunning { get; private set; }

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            IsRunning = true;

            _ = Task.Run(async () =>
            {
                while (!_cts.Token.IsCancellationRequested)
                {
                    await Task.Delay(5000, _cts.Token);
                    /*try
                    {
                        // Ваша фоновая логика здесь
                        _logger.LogInformation("Фоновая задача выполняется...");
                        await Task.Delay(5000, _cts.Token); // например, раз в 5 секунд
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.LogInformation("Фоновая задача отменена.");
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Ошибка в фоновой задаче");
                    }*/
                }

                IsRunning = false;
            }, _cts.Token);
        }

        public async Task StopAsync()
        {
            _cts?.Cancel();
            IsRunning = false;
        }

        // Пример метода для взаимодействия из UI
        public string GetStatus() => IsRunning ? "Работает" : "Остановлен";
    }
}
