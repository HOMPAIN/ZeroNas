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
        private static string RunCommand(string command)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = $"-c \"{command}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            process?.WaitForExit();
            return process?.StandardOutput.ReadToEnd() ?? string.Empty;
        }

        // Получить список доступных Wi-Fi сетей
        public static List<WiFiNetwork> GetAvailableNetworks()
        {
            var output = RunCommand("nmcli -t -f SSID,SECURITY dev wifi list");
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
            RunCommand($"nmcli con delete \"{ssid}\"");

            var command = $"nmcli dev wifi connect \"{ssid}\" password \"{password}\"";
            var output = RunCommand(command);

            return output.Contains("successfully activated") ||
                   (output.Contains("Device 'wlan") && output.Contains("successfully connected"));
        }

        // Получить список сохранённых сетей
        public static List<SavedNetwork> GetSavedNetworks()
        {
            // Получаем имя и тип соединений
            var output = RunCommand("nmcli -t -f NAME,TYPE con show");
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
            var output = RunCommand($"nmcli con delete \"{ssid}\"");
            return !output.Contains("Error") && !output.Contains("not found");
        }

        // Получить имя текущего подключения
        private static string GetCurrentConnectionName()
        {
            // Получаем активные соединения с типом
            var output = RunCommand("nmcli -t -f NAME,TYPE con show --active");
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
            var output = RunCommand("nmcli -t -f NAME,TYPE con show --active");
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
            var ssid = GetCurrentActiveWiFiConnectionName(); // теперь включает AP
            if (string.IsNullOrEmpty(ssid))
                return null;

            // IP-адрес (предполагаем, что Wi-Fi интерфейс — wlan0)
            var ipOutput = RunCommand("ip -4 addr show wlan0 | grep -oP '(?<=inet\\s)\\d+(\\.\\d+){3}'");
            var ipAddress = ipOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim();

            // Скорость
            string speed = "N/A";
            try
            {
                var speedPath = "/sys/class/net/wlan0/speed";
                if (File.Exists(speedPath))
                {
                    speed = File.ReadAllText(speedPath).Trim() + " Mbps";
                }
            }
            catch { /* игнорируем */ }

            // Уровень сигнала
            string signal = "N/A";
            var iwOutput = RunCommand("iw wlan0 station dump | grep 'signal:'");
            var match = Regex.Match(iwOutput, @"signal:\s*(-?\d+)\s*dBm");
            if (match.Success)
            {
                signal = match.Groups[1].Value + " dBm";
            }

            return new CurrentConnectionInfo
            {
                Ssid = ssid,
                IpAddress = ipAddress ?? "N/A",
                Speed = speed,
                SignalStrength = signal
            };
        }

        // Создать точку доступа Wi-Fi (hotspot)
        public static bool CreateOpenHotspot(string ssid)
        {
            if (string.IsNullOrWhiteSpace(ssid))
                return false;

            try
            {
                // Отключаем активные Wi-Fi соединения
                RunCommand("nmcli -t -f NAME,TYPE con show --active | grep '802-11-wireless' | cut -d: -f1 | xargs -r nmcli con down");

                // Удаляем старый профиль
                RunCommand($"nmcli con delete \"{ssid}\" 2>/dev/null");

                // Создаём БЕЗ ПАРОЛЯ
                var createCmd = $"nmcli con add type wifi ifname wlan0 con-name \"{ssid}\" ssid \"{ssid}\"";
                RunCommand(createCmd);

                // Настраиваем как ОТКРЫТУЮ AP
                var modifyCmd =
                    $"nmcli con modify \"{ssid}\" " +
                    "802-11-wireless.mode ap " +
                    "802-11-wireless.band bg " +
                    "ipv4.method shared " +
                    "ipv4.addresses 192.168.0.1/24";

                RunCommand(modifyCmd);

                var upOutput = RunCommand($"nmcli con up \"{ssid}\" ifname wlan0");

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
            var output = RunCommand($"nmcli con down \"{ssid}\"");
            return !output.Contains("Error");
        }

        // Удалить точку доступа
        public static bool DeleteHotspot(string ssid)
        {
            return DeleteSavedNetwork(ssid);
        }
    }
}
