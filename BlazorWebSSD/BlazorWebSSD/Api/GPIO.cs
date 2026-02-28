

namespace BlazorWebSSD
{
    public static class GPIO
    {
        // Константы для направления
        public const string INPUT = "in";
        public const string OUTPUT = "out";

        // Константы для значений
        public const int LOW = 0;
        public const int HIGH = 1;

        // Базовый путь к GPIO в sysfs
        private const string _gpioBasePath = "/sys/class/gpio";

        /// <summary>
        /// Устанавливает режим пина (вход/выход)
        /// </summary>
        /// <param name="pinNumber">Номер GPIO пина</param>
        /// <param name="mode">Режим: INPUT или OUTPUT</param>
        public static void pinMode(int pinNumber, string mode)
        {
            try
            {
                // Сначала экспортируем пин, если он еще не экспортирован
                exportPin(pinNumber);

                // Устанавливаем направление
                string directionPath = Path.Combine($"{_gpioBasePath}/gpio{pinNumber}", "direction");
                File.WriteAllText(directionPath, mode);

                Console.WriteLine($"Pin {pinNumber} mode set to {mode}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error setting pin mode for {pinNumber}: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Устанавливает значение на выходном пине
        /// </summary>
        /// <param name="pinNumber">Номер GPIO пина</param>
        /// <param name="value">Значение: HIGH (1) или LOW (0)</param>
        public static void digitalWrite(int pinNumber, int value)
        {
            //Console.WriteLine("Pin:"+pinNumber +" Val:"+value);
            try
            {
                string valuePath = Path.Combine($"{_gpioBasePath}/gpio{pinNumber}", "value");
                File.WriteAllText(valuePath, value.ToString());

                // Console.WriteLine($"Pin {pinNumber} set to {value}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error writing to pin {pinNumber}: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Читает значение с входного пина
        /// </summary>
        /// <param name="pinNumber">Номер GPIO пина</param>
        /// <returns>Значение: HIGH (1) или LOW (0)</returns>
        public static int digitalRead(int pinNumber)
        {
            try
            {
                string valuePath = Path.Combine($"{_gpioBasePath}/gpio{pinNumber}", "value");
                string value = File.ReadAllText(valuePath).Trim();
                return int.Parse(value);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading from pin {pinNumber}: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Экспортирует GPIO пин для использования
        /// </summary>
        /// <param name="pinNumber">Номер GPIO пина</param>
        private static void exportPin(int pinNumber)
        {
            try
            {
                // Проверяем, существует ли уже директория для этого пина
                string gpioPath = Path.Combine(_gpioBasePath, $"gpio{pinNumber}");
                if (!Directory.Exists(gpioPath))
                {
                    // Экспортируем пин
                    File.WriteAllText(Path.Combine(_gpioBasePath, "export"), pinNumber.ToString());

                    // Ждем немного, пока система создаст директорию
                    int attempts = 0;
                    while (!Directory.Exists(gpioPath) && attempts < 10)
                    {
                        Thread.Sleep(10);
                        attempts++;
                    }

                    if (!Directory.Exists(gpioPath))
                    {
                        throw new IOException($"Failed to export GPIO pin {pinNumber}. Directory not created.");
                    }
                }
            }
            catch (IOException ex) when (ex.Message.Contains("Device or resource busy"))
            {
                // Пин уже экспортирован - это нормально
                Console.WriteLine($"Pin {pinNumber} is already exported");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error exporting pin {pinNumber}: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Освобождает GPIO пин (unexport)
        /// </summary>
        /// <param name="pinNumber">Номер GPIO пина</param>
        public static void cleanup(int pinNumber)
        {
            try
            {
                string gpioPath = Path.Combine(_gpioBasePath, $"gpio{pinNumber}");
                if (Directory.Exists(gpioPath))
                {
                    File.WriteAllText(Path.Combine(_gpioBasePath, "unexport"), pinNumber.ToString());
                    Console.WriteLine($"Pin {pinNumber} cleaned up");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error cleaning up pin {pinNumber}: {ex.Message}");
            }
        }

        /// <summary>
        /// Освобождает все использованные пины
        /// </summary>
        public static void cleanupAll()
        {
            try
            {
                // Читаем все экспортированные пины из файла
                if (File.Exists(Path.Combine(_gpioBasePath, "exported_pins")))
                {
                    string[] exportedPins = File.ReadAllLines(Path.Combine(_gpioBasePath, "exported_pins"));
                    foreach (string pinStr in exportedPins)
                    {
                        if (int.TryParse(pinStr, out int pinNumber))
                        {
                            cleanup(pinNumber);
                        }
                    }
                }
            }
            catch
            {
                // Файл exported_pins может не существовать на всех системах
                Console.WriteLine("No exported pins file found, skipping cleanup");
            }
        }
    }
}
