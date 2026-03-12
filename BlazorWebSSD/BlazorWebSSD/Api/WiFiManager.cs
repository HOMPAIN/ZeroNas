using System.Diagnostics;
using System.Text.RegularExpressions;

namespace BlazorWebSSD
{
    public class WiFiNetwork
    {
        public string Ssid { get; set; }
        public string Security { get; set; }
    }

    public class SavedNetwork
    {
        public string Ssid { get; set; }
    }

    public class CurrentConnectionInfo
    {
        public string Ssid { get; set; }
        public string IpAddress { get; set; }
        public string SignalStrength { get; set; }
        public string Speed { get; set; }
    }

    public static class WiFiManager
    {

        // Получить список доступных Wi-Fi сетей
        public static List<WiFiNetwork> GetAvailableNetworks()
        {
            var output = LinuxCommand.Run("nmcli","-t -f SSID,SECURITY dev wifi list");
            var networks = new List<WiFiNetwork>();

            foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = line.Split(':');
                if (parts.Length >= 2 && !string.IsNullOrEmpty(parts[0]))
                {
                    networks.Add(new WiFiNetwork
                    {
                        Ssid = parts[0],
                        Security = parts[1]
                    });
                }
            }
            return networks;
        }

        // Подключиться к сети по SSID и паролю
        public static bool ConnectToNetwork(string ssid, string password)
        {
            LinuxCommand.Run("nmcli", $"con delete \"{ssid}\"");

            var command = $"nmcli dev wifi connect \"{ssid}\" password \"{password}\"";
            var output = LinuxCommand.Run(command);

            return output.Contains("successfully activated") ||
                   (output.Contains("Device 'wlan") && output.Contains("successfully connected"));
        }

        // Получить список сохранённых сетей
        public static List<SavedNetwork> GetSavedNetworks()
        {
            // Получаем имя и тип соединений
            var output = LinuxCommand.Run("nmcli","-t -f NAME,TYPE con show");
            var saved = new List<SavedNetwork>();

            foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = line.Split(':');
                if (parts.Length >= 2)
                {
                    var name = parts[0];
                    var type = parts[1];

                    // Фильтруем только Wi-Fi соединения
                    if (type == "802-11-wireless" && !string.IsNullOrEmpty(name))
                    {
                        saved.Add(new SavedNetwork { Ssid = name });
                    }
                }
            }
            return saved;
        }

        // Удалить сеть из сохранённых
        public static bool DeleteSavedNetwork(string ssid)
        {
            var output = LinuxCommand.Run("nmcli",$"con delete \"{ssid}\"");
            return !output.Contains("Error") && !output.Contains("not found");
        }

        // Получить имя текущего подключения
        private static string GetCurrentConnectionName()
        {
            // Получаем активные соединения с типом
            var output = LinuxCommand.Run("nmcli", "-t -f NAME,TYPE con show --active");
            foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = line.Split(':');
                if (parts.Length >= 2)
                {
                    var name = parts[0];
                    var type = parts[1];

                    // Только Wi-Fi!
                    if (type == "802-11-wireless" && !string.IsNullOrEmpty(name) && name != "lo")
                    {
                        return name;
                    }
                }
            }
            return null;
        }
        // Получить имя текущего подключения включая и собвенную точку доступа
        public static string GetCurrentActiveWiFiConnectionName()
        {
            var output = LinuxCommand.Run("nmcli", "-t -f NAME,TYPE con show --active");
            foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = line.Split(':');
                if (parts.Length >= 2)
                {
                    var name = parts[0];
                    var type = parts[1];

                    // Принимаем ЛЮБОЕ активное Wi-Fi-соединение (включая AP)
                    if (type == "802-11-wireless" && !string.IsNullOrEmpty(name))
                    {
                        return name;
                    }
                }
            }
            return null;
        }

        // Получить подробную информацию о текущем подключении
        public static CurrentConnectionInfo GetCurrentConnectionInfo()
        {
            var ssid = GetCurrentActiveWiFiConnectionName();
            if (string.IsNullOrEmpty(ssid))
                return null;

            // === IP-адрес: получаем весь вывод ip и парсим в C# ===
            var ipOutput = LinuxCommand.Run("ip", "-4 addr show wlan0");
            var ipAddress = "N/A";

            if (!string.IsNullOrEmpty(ipOutput))
            {
                // Ищем строку вида "inet 192.168.1.100/24 ..."
                var match = Regex.Match(ipOutput, @"inet\s+(\d{1,3}(?:\.\d{1,3}){3})");
                if (match.Success)
                    ipAddress = match.Groups[1].Value;
            }

            // === Скорость ===
            string speed = "N/A";
            try
            {
                var speedPath = "/sys/class/net/wlan0/speed";
                if (File.Exists(speedPath))
                {
                    var raw = File.ReadAllText(speedPath).Trim();
                    if (int.TryParse(raw, out var s) && s > 0)
                        speed = s + " Mbps";
                }
            }
            catch { /* игнорируем */ }

            // === Уровень сигнала ===
            string signal = "N/A";
            var iwOutput = LinuxCommand.Run("iw", "wlan0 station dump");
            if (!string.IsNullOrEmpty(iwOutput))
            {
                var match = Regex.Match(iwOutput, @"signal:\s*(-?\d+)\s*dBm");
                if (match.Success)
                    signal = match.Groups[1].Value + " dBm";
            }

            return new CurrentConnectionInfo
            {
                Ssid = ssid,
                IpAddress = ipAddress,
                Speed = speed,
                SignalStrength = signal
            };
        }
        /// <summary>
        /// Отключает все активные беспроводные (Wi-Fi) соединения через NetworkManager.
        /// </summary>
        public static void DisconnectAllActiveWiFi()
        {
            Console.WriteLine("[DEBUG] Поиск активных Wi-Fi соединений...");

            // 1. Получаем список активных подключений в машиночитаемом формате
            // -t: вывод в формате name:type (без заголовков, разделитель :)
            // -f NAME,TYPE: только имя подключения и его тип
            var output = LinuxCommand.Run("nmcli", "-t -f NAME,TYPE con show --active");

            if (string.IsNullOrEmpty(output))
            {
                Console.WriteLine("[DEBUG] Нет активных подключений.");
                return;
            }

            // 2. Парсим вывод и отключаем только беспроводные соединения
            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var disconnectedCount = 0;

            foreach (var line in lines)
            {
                var parts = line.Split(':');
                if (parts.Length < 2) continue;

                var connectionName = parts[0];
                var connectionType = parts[1];

                // Тип беспроводного соединения в nmcli: 802-11-wireless
                if (connectionType == "802-11-wireless")
                {
                    Console.WriteLine($"[DEBUG] Отключаем Wi-Fi подключение: {connectionName}");

                    // Отключаем подключение
                    var result = LinuxCommand.Run("nmcli", $"con down \"{connectionName}\"");

                    if (result != null)
                    {
                        disconnectedCount++;
                        Console.WriteLine($"[DEBUG] Успешно отключено: {connectionName}");
                    }
                    else
                    {
                        Console.Error.WriteLine($"[ERROR] Не удалось отключить: {connectionName}");
                    }
                }
            }

            Console.WriteLine($"[DEBUG] Готово. Отключено соединений: {disconnectedCount}");
        }

        // Создать точку доступа Wi-Fi (hotspot)
        public static bool CreateOpenHotspot(string ssid)
        {
            if (string.IsNullOrWhiteSpace(ssid))
                return false;

            try
            {
                // Отключаем активные Wi-Fi соединения
                DisconnectAllActiveWiFi();

                // Удаляем старый профиль
                LinuxCommand.Run("nmcli", $"con delete \"{ssid}\" 2>/dev/null");

                // Создаём БЕЗ ПАРОЛЯ
                var createCmd = $"nmcli con add type wifi ifname wlan0 con-name \"{ssid}\" ssid \"{ssid}\"";
                LinuxCommand.Run(createCmd);

                // Настраиваем как ОТКРЫТУЮ AP
                var modifyCmd =
                    $"nmcli con modify \"{ssid}\" " +
                    "802-11-wireless.mode ap " +
                    "802-11-wireless.band bg " +
                    "ipv4.method shared " +
                    "ipv4.addresses 192.168.0.1/24";

                LinuxCommand.Run(modifyCmd);

                var upOutput = LinuxCommand.Run("nmcli", $"con up \"{ssid}\" ifname wlan0");

                return upOutput.Contains("successfully activated") ||
                       upOutput.Contains("Connection successfully activated");
            }
            catch
            {
                return false;
            }
        }

        // Остановить точку доступа
        public static bool StopHotspot(string ssid)
        {
            var output = LinuxCommand.Run("nmcli", $"con down \"{ssid}\"");
            return !output.Contains("Error");
        }

        // Удалить точку доступа
        public static bool DeleteHotspot(string ssid)
        {
            return DeleteSavedNetwork(ssid);
        }
    }
}
