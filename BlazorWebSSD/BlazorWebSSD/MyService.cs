using System.ComponentModel;

namespace BlazorWebSSD
{
    //сервис отвечает за запуск и остановку объекта вервера при запуске и остановке приложения
    public class MyService : IHostedService
    {
        private readonly MyServer _worker;

        public MyService(MyServer worker, DisksConfig _DC)
        {
            _worker = worker;
            try
            {
                _DC.Load();
            }catch
            { }
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            return _worker.StartAsync(cancellationToken);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return _worker.StopAsync();
        }
    }
}
