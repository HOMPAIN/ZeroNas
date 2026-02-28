//мастер управления светодиодом
namespace BlazorWebSSD
{
    //перечисление различных вариантов работы светодиода
    public enum LedState
    {
        GreenConst, //зелёный горит постоянно
        GreenFastBlink,//зелёный мигает быстро
        GreenSlowBlink,//зелёный мигает медленно

        OrangeConst,//оранжевый горит постоянно
        OrangeFastBlink,//оранжевый мигает быстро
        OrangeSlowBlink,//оранжевый мигает медленно

        RedConst,//красный горит постоянно
        RedFastBlink,//красный мигает быстро
        RedSlowBlink,//красный мигает медленно

        Off,//выключить
    }
    public class LedMaster : IHostedService
    {
        const int GreenPin = 4;
        const int RedPin = 3;
        const int PinDelta = 256;//сдвиг дла обращения к нужной группе пинов
        //состояние типа свечения светодиода
        LedState State = LedState.RedFastBlink;
        //состояние работы потока светодиода
        public bool IsRunning { get; private set; }
        //токен остановки потока управления светодиода

        private CancellationTokenSource? _cts;

        public void SetState(LedState _State)
        {
            State = _State;
        }
        //запуска таска светодиода
        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            IsRunning = true;

            //установка GPIO пинов светодиодов, как выходы
            GPIO.pinMode(PinDelta + GreenPin, GPIO.OUTPUT);
            GPIO.pinMode(PinDelta + RedPin, GPIO.OUTPUT);


            //таск с бесконечным циклом мигая в нужном режиме
            _ = Task.Run(async () =>
            {
                Console.WriteLine("Светодиод прошёл инициализацию");
                while (!_cts.Token.IsCancellationRequested)
                {
                    if (State == LedState.GreenSlowBlink || State == LedState.OrangeSlowBlink || State == LedState.RedSlowBlink)
                        await Task.Delay(1000, _cts.Token);
                    else
                        await Task.Delay(200, _cts.Token);

                    if (State == LedState.GreenConst || State == LedState.GreenSlowBlink || State == LedState.GreenFastBlink)
                        GreenOn();

                    if (State == LedState.OrangeConst || State == LedState.OrangeSlowBlink || State == LedState.OrangeFastBlink)
                        OrangeOn();

                    if (State == LedState.RedConst || State == LedState.RedSlowBlink || State == LedState.RedFastBlink)
                        RedOn();

                    if (State == LedState.Off)
                        Off();

                    if (State == LedState.GreenSlowBlink || State == LedState.OrangeSlowBlink || State == LedState.RedSlowBlink)
                        await Task.Delay(1000, _cts.Token);
                    else
                        await Task.Delay(200, _cts.Token);

                    if (State != LedState.GreenConst && State != LedState.OrangeConst && State != LedState.RedConst)
                        Off();
                }

                IsRunning = false;
            }, _cts.Token);
        }

        //остановка таска светодиода
        public async Task StopAsync(CancellationToken cancellationToken)
        {
            Off();
            //установка GPIO пинов светодиодов, как входы
            GPIO.pinMode(PinDelta + GreenPin, GPIO.INPUT);
            GPIO.pinMode(PinDelta + RedPin, GPIO.INPUT);

            _cts?.Cancel();
            IsRunning = false;
        }
        //зажечь зелёный
        private void GreenOn()
        {
            GPIO.digitalWrite(PinDelta + RedPin, GPIO.HIGH);
            GPIO.digitalWrite(PinDelta + GreenPin, GPIO.LOW);
        }
        //зажечь оранжевый
        private void OrangeOn()
        {
            GPIO.digitalWrite(PinDelta + RedPin, GPIO.LOW);
            GPIO.digitalWrite(PinDelta + GreenPin, GPIO.LOW);
        }
        //зажечь красный
        private void RedOn()
        {
            GPIO.digitalWrite(PinDelta + GreenPin, GPIO.HIGH);
            GPIO.digitalWrite(PinDelta + RedPin, GPIO.LOW);
        }
        //погасить
        private void Off()
        {
            GPIO.digitalWrite(PinDelta + RedPin, GPIO.HIGH);
            GPIO.digitalWrite(PinDelta + GreenPin, GPIO.HIGH);
        }
    }
}
