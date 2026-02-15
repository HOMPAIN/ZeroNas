using System.Diagnostics;
using System.Text.RegularExpressions;

namespace BlazorWebSSD
{
    public class SambaShareInfo
    {
        public string ShareName { get; set; }
        public string Path { get; set; }
        public List<string> ReadUsers { get; set; } = new();
        // Note: write users are usually all authenticated users unless 'read only = yes'
        public List<string> WriteUsers { get; set; } = new();
        public bool ReadOnly { get; set; }
    }

    public static class SambaManager
    {
        private const string SmbConfPath = "/etc/samba/smb.conf";

        // 1. Получить список папок с шарингом
        public static List<SambaShareInfo> GetShares()
        {
            if (!File.Exists(SmbConfPath))
                throw new FileNotFoundException($"Samba config not found: {SmbConfPath}");

            var lines = File.ReadAllLines(SmbConfPath);
            var shares = new List<SambaShareInfo>();
            SambaShareInfo current = null;

            foreach (var line in lines)
            {
                var trimmed = line.Trim();

                // Пропускаем комментарии и пустые строки
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#") || trimmed.StartsWith(";"))
                    continue;

                // Начало новой секции
                if (Regex.IsMatch(trimmed, @"^\[.*\]$"))
                {
                    if (current != null)
                        shares.Add(current);

                    var name = trimmed.Trim('[', ']');
                    if (name == "global") continue;

                    current = new SambaShareInfo { ShareName = name };
                }
                else if (current != null)
                {
                    var parts = trimmed.Split(new[] { '=' }, 2, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length != 2) continue;

                    var key = parts[0].Trim().ToLowerInvariant();
                    var value = parts[1].Trim();

                    switch (key)
                    {
                        case "path":
                            current.Path = value;
                            break;
                        case "read only":
                            current.ReadOnly = value.Equals("yes", StringComparison.OrdinalIgnoreCase);
                            break;
                        case "writable":
                            if (value.Equals("yes", StringComparison.OrdinalIgnoreCase))
                                current.ReadOnly = false;
                            break;
                            // Для простоты не парсим valid users — см. метод GetShareUsers
                    }
                }
            }

            if (current != null)
                shares.Add(current);

            return shares;
        }

        // 2. Получить пользователей и их права для конкретной папки
        public static (List<string> readUsers, List<string> writeUsers) GetShareUsers(string shareName)
        {
            // В стандартной Samba нет прямого способа хранить per-user права в smb.conf.
            // Обычно используется:
            // - valid users = user1,user2
            // - read only = yes/no (глобально для шары)
            // Или используются ACL на уровне файловой системы.

            // Поэтому мы просто вернём всех пользователей из Samba и пометим их как "write",
            // если шара не read-only.

            var shares = GetShares();
            var share = shares.FirstOrDefault(s => s.ShareName.Equals(shareName, StringComparison.OrdinalIgnoreCase));
            if (share == null)
                throw new ArgumentException($"Share '{shareName}' not found.");

            var allSambaUsers = GetAllSambaUsers();
            if (share.ReadOnly)
                return (allSambaUsers, new List<string>());
            else
                return (new List<string>(), allSambaUsers);
        }

        private static List<string> GetAllSambaUsers()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "pdbedit",
                    Arguments = "-L",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                process?.WaitForExit();
                if (process?.ExitCode != 0)
                    throw new InvalidOperationException("Failed to list Samba users.");

                var output = process?.StandardOutput.ReadToEnd();
                if (string.IsNullOrEmpty(output)) return new List<string>();

                return output
                    .Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(line => line.Split(':')[0])
                    .Where(u => !string.IsNullOrEmpty(u))
                    .ToList();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Cannot retrieve Samba users. Ensure 'samba' and 'smbd' are installed.", ex);
            }
        }

        // 3. Удалить все папки из шаринга (оставить только [global])
        public static void RemoveAllShares()
        {
            var lines = File.ReadAllLines(SmbConfPath).ToList();
            var newLines = new List<string>();
            bool inGlobal = false;

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("[global]", StringComparison.OrdinalIgnoreCase))
                {
                    newLines.Add(line);
                    inGlobal = true;
                    continue;
                }
                else if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                {
                    inGlobal = false;
                    continue; // skip all share sections
                }

                if (inGlobal)
                    newLines.Add(line);
            }

            // Сохраняем только [global]
            File.WriteAllLines(SmbConfPath, newLines);
            ReloadSamba();
        }

        // 4. Добавить папку в шаринг
        public static void AddShare(string shareName, string path, List<string> users, bool readOnly = false)
        {
            Console.Write("Add share " + shareName + " to path " + path + " for ");
            foreach (var user in users)
                Console.Write(user + " ");
            Console.WriteLine();
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                Console.WriteLine($"Path does not exist: {path}, create path");
            }

            EnsureGlobalSection(); // ← гарантирует наличие [global] с security = user

            var sambaUsers = GetAllSambaUsers();
            foreach (var user in users)
            {
                if (!sambaUsers.Contains(user))
                    throw new ArgumentException($"User '{user}' is not a Samba user. Add with 'smbpasswd -a {user}'.");
            }

            var validUsers = string.Join(",", users);

            // Даем права на папку (для теста — 777)
            RunCommand("sudo", $"chmod 777 \"{path}\"");

            var shareSection = $@"
[{shareName}]
    path = {path}
    valid users = {validUsers}
    browseable = yes
    writable = {(readOnly ? "no" : "yes")}
    read only = {(readOnly ? "yes" : "no")}
    create mask = 0664
    directory mask = 0775
    guest ok = no
";

            File.AppendAllText(SmbConfPath, shareSection);
            ReloadSamba();
        }

        private static void ReloadSamba()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "sudo",
                    Arguments = "systemctl reload smbd nmbd",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var process = Process.Start(psi);
                process?.WaitForExit();
                if (process?.ExitCode != 0)
                    throw new InvalidOperationException("Failed to reload Samba services.");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Cannot reload Samba. Ensure sudo permissions or run as root.", ex);
            }
        }
        // Получить список всех пользователей Samba
        public static List<string> GetSambaUsers()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "pdbedit",
                    Arguments = "-L",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                process?.WaitForExit();
                if (process?.ExitCode != 0)
                    throw new InvalidOperationException("Failed to list Samba users.");

                var output = process.StandardOutput.ReadToEnd();
                return output
                    .Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(line => line.Split(':')[0])
                    .Where(u => !string.IsNullOrEmpty(u))
                    .ToList();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Cannot retrieve Samba users. Ensure 'samba' is installed and running.", ex);
            }
        }

        // Добавить нового пользователя: системного + в Samba
        public static void AddUser(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username))
                throw new ArgumentException("Username cannot be null or empty.", nameof(username));
            if (string.IsNullOrWhiteSpace(password))
                throw new ArgumentException("Password cannot be null or empty.", nameof(password));

            // 1. Создать системного пользователя (без домашней папки, без входа)
            Console.WriteLine($"[DEBUG] Создаём системного пользователя {username}...");
            RunCommand("sudo", $"useradd -M -s /usr/sbin/nologin {username}", allowNonZeroExit: true);

            // 2. Установить пароль системному пользователю
            Console.WriteLine($"[DEBUG] Устанавливаем системный пароль для {username}...");
            RunCommandWithInput("sudo", "chpasswd", $"{username}:{password}");

            // 3. Добавить пользователя в Samba с паролем
            Console.WriteLine($"[DEBUG] Добавляем {username} в Samba...");
            RunCommandWithInput("sudo", $"smbpasswd -a -s {username}", $"{password}\n{password}");

            // 4. Включить учётную запись (опционально, но рекомендуется)
            Console.WriteLine($"[DEBUG] Включаем учётную запись Samba для {username}...");
            RunCommand("sudo", $"smbpasswd -e {username}");
        }

        // Удалить пользователя из Samba и из системы
        public static void RemoveUser(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
                throw new ArgumentException("Username cannot be null or empty.", nameof(username));

            // 1. Удалить из Samba
            RunCommand("sudo", $"smbpasswd -x {username}", allowNonZeroExit: true);

            // 2. Удалить системного пользователя (без удаления домашней папки, т.к. её нет)
            // Флаг -r удалит домашнюю папку и почту, но у нас -M, так что можно без -r
            RunCommand("sudo", $"userdel {username}", allowNonZeroExit: true);
        }

        // Вспомогательные методы для запуска команд
        private static void RunCommand(string command, string args, bool allowNonZeroExit = false)
        {
            var psi = new ProcessStartInfo
            {
                FileName = command,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            process?.WaitForExit();

            if (!allowNonZeroExit && process?.ExitCode != 0)
            {
                var error = process?.StandardError.ReadToEnd();
                throw new InvalidOperationException($"Command failed: {command} {args}. Error: {error}");
            }
        }

        private static void RunCommandWithInput(string command, string args, string input)
        {
            var psi = new ProcessStartInfo
            {
                FileName = command,
                Arguments = args,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null)
                throw new InvalidOperationException("Failed to start process.");

            using (var writer = process.StandardInput)
            {
                writer.WriteLine(input);
            }

            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                var error = process.StandardError.ReadToEnd();
                throw new InvalidOperationException($"Command failed: {command} {args}. Error: {error}");
            }
        }
        private static void EnsureGlobalSection()
        {
            if (!File.Exists(SmbConfPath))
                File.WriteAllText(SmbConfPath, "");

            var lines = File.ReadAllLines(SmbConfPath).ToList();

            // Проверяем, есть ли [global]
            bool hasGlobal = lines.Any(line => Regex.IsMatch(line.Trim(), @"^\[global\]\s*$", RegexOptions.IgnoreCase));
            if (!hasGlobal)
            {
                var globalSection = @"
[global]
   workgroup = WORKGROUP
   server string = Samba Server
   security = user
   map to guest = never
   usershare allow guests = no
   obey pam restrictions = yes
   unix password sync = yes
   passwd program = /usr/bin/passwd %u
   passwd chat = *Enter\snew\s*\spassword:* %n\n *Retype\snew\s*\spassword:* %n\n *password\supdated\ssuccessfully* .
   pam password change = yes
   socket options = TCP_NODELAY
   dns proxy = no
";
                File.WriteAllText(SmbConfPath, globalSection + "\n" + string.Join("\n", lines));
                return;
            }

            // Если [global] есть — корректируем ключевые параметры
            bool inGlobal = false;
            bool foundMapToGuest = false;
            bool foundUsershareGuests = false;

            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i].Trim();
                if (Regex.IsMatch(line, @"^\[global\]\s*$", RegexOptions.IgnoreCase))
                {
                    inGlobal = true;
                    continue;
                }
                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    inGlobal = false;
                    continue;
                }

                if (inGlobal)
                {
                    if (line.StartsWith("map to guest", StringComparison.OrdinalIgnoreCase))
                    {
                        lines[i] = "   map to guest = never";
                        foundMapToGuest = true;
                    }
                    else if (line.StartsWith("usershare allow guests", StringComparison.OrdinalIgnoreCase))
                    {
                        lines[i] = "   usershare allow guests = no";
                        foundUsershareGuests = true;
                    }
                }
            }

            // Если параметры не найдены — добавим их в конец [global]
            if (!foundMapToGuest || !foundUsershareGuests)
            {
                // Найти конец секции [global] и вставить перед первой другой секцией
                int insertIndex = -1;
                bool inGlobalNow = false;
                for (int i = 0; i < lines.Count; i++)
                {
                    var line = lines[i].Trim();
                    if (Regex.IsMatch(line, @"^\[global\]\s*$", RegexOptions.IgnoreCase))
                    {
                        inGlobalNow = true;
                    }
                    else if (inGlobalNow && line.StartsWith("[") && line.EndsWith("]"))
                    {
                        insertIndex = i;
                        break;
                    }
                }
                if (insertIndex == -1) insertIndex = lines.Count;

                if (!foundMapToGuest)
                    lines.Insert(insertIndex++, "   map to guest = never");
                if (!foundUsershareGuests)
                    lines.Insert(insertIndex, "   usershare allow guests = no");
            }

            File.WriteAllLines(SmbConfPath, lines);
        }
    }
}
