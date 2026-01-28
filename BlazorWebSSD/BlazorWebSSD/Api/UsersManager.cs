using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace BlazorWebSSD
{
    /// <summary>
    /// Представляет запись о пользователе в системе Linux.
    /// </summary>
    public class UserEntry
    {
        public string Username { get; set; }          // Имя пользователя
        public int Uid { get; set; }                  // Уникальный идентификатор пользователя (User ID)
        public int Gid { get; set; }                  // Идентификатор основной группы пользователя (Group ID)
        public string HomeDirectory { get; set; }     // Путь к домашней директории пользователя
        public string Shell { get; set; }             // Оболочка (shell), используемая пользователем
        public List<string> Groups { get; set; } = new List<string>(); // Список групп, в которые входит пользователь
    }

    /// <summary>
    /// Управляет пользователями в Linux-системе (чтение, добавление, удаление).
    /// </summary>
    public class UsersManager
    {
        public List<UserEntry> ?Users { get; set; }
        private const string PasswdPath = "/etc/passwd"; // Путь к файлу с базовой информацией о пользователях
        private const string GroupPath = "/etc/group";   // Путь к файлу с информацией о группах

        /// <summary>
        /// Считывает всех пользователей из /etc/passwd и сопоставляет им группы из /etc/group.
        /// </summary>
        /// <returns>Список объектов UserEntry со всеми пользователями и их группами.</returns>
        public List<UserEntry> GetAllUsersWithGroups()
        {
            var users = ReadPasswdFile();        // Список пользователей из /etc/passwd
            var groups = ReadGroupFile();        // Список групп из /etc/group

            foreach (var user in users)
            {
                // Основная группа по GID
                var primaryGroup = groups.FirstOrDefault(g => g.Gid == user.Gid)?.Name;
                if (!string.IsNullOrEmpty(primaryGroup))
                    user.Groups.Add(primaryGroup);

                // Дополнительные группы, в которые входит пользователь
                foreach (var group in groups)
                {
                    if (group.Members.Contains(user.Username))
                        user.Groups.Add(group.Name);
                }
            }
            Users = users;
            return users;
        }

        /// <summary>
        /// Создаёт нового системного пользователя с помощью утилиты useradd.
        /// </summary>
        /// <param name="username">Имя нового пользователя.</param>
        /// <param name="createHome">Создавать ли домашнюю директорию (по умолчанию true).</param>
        /// <param name="shell">Путь к оболочке (по умолчанию /bin/bash).</param>
        /// <param name="groups">Массив дополнительных групп, в которые добавить пользователя.</param>
        public void AddUser(string username, bool createHome = true, string shell = "/bin/bash", string[] groups = null)
        {
            if (string.IsNullOrWhiteSpace(username))
                throw new ArgumentException("Username cannot be null or empty.", nameof(username));

            string args;
            if (createHome)
                args = $"-m -s {shell} {username}";
            else
                args = $"-s {shell} {username}";

            RunCommand("useradd", args);

            if (groups != null && groups.Length > 0)
            {
                var groupList = string.Join(",", groups);
                RunCommand("usermod", $"-aG {groupList} {username}");
            }
        }

        /// <summary>
        /// Удаляет пользователя из системы с помощью утилиты userdel.
        /// </summary>
        /// <param name="username">Имя удаляемого пользователя.</param>
        /// <param name="removeHome">Удалять ли домашнюю директорию и почтовый ящик (по умолчанию true).</param>
        /// <exception cref="ArgumentException">Если имя пользователя пустое или null.</exception>
        /// <exception cref="InvalidOperationException">Если команда userdel завершилась с ошибкой.</exception>
        public void RemoveUser(string username, bool removeHome = true)
        {
            if (string.IsNullOrWhiteSpace(username))
                throw new ArgumentException("Username cannot be null or empty.", nameof(username));

            // Флаг -r удаляет домашнюю директорию
            var args = removeHome ? $"-r {username}" : username;

            RunCommand("userdel", args);
        }

        // --- Вспомогательные методы ---

        /// <summary>
        /// Выполняет системную команду и проверяет код возврата.
        /// </summary>
        /// <param name="command">Имя исполняемой команды (например, "useradd").</param>
        /// <param name="arguments">Аргументы команды.</param>
        /// <exception cref="InvalidOperationException">Если команда завершилась с ненулевым кодом.</exception>
        private void RunCommand(string command, string arguments)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = command,                   // Исполняемый файл
                Arguments = arguments,                // Аргументы командной строки
                RedirectStandardOutput = true,        // Перехват стандартного вывода
                RedirectStandardError = true,         // Перехват ошибок
                UseShellExecute = false,              // Не использовать оболочку
                CreateNoWindow = true                 // Не показывать окно консоли
            };

            using var process = Process.Start(startInfo);
            process?.WaitForExit();

            if (process?.ExitCode != 0)
            {
                var error = process?.StandardError.ReadToEnd() ?? "Unknown error";
                throw new InvalidOperationException($"Command '{command} {arguments}' failed with exit code {process?.ExitCode}. Error: {error}");
            }
        }

        /// <summary>
        /// Читает файл /etc/passwd и возвращает список пользователей.
        /// </summary>
        private List<UserEntry> ReadPasswdFile()
        {
            var users = new List<UserEntry>();       // Результирующий список пользователей
            if (!File.Exists(PasswdPath))
                throw new FileNotFoundException($"File not found: {PasswdPath}");

            foreach (var line in File.ReadLines(PasswdPath))
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                    continue;

                var parts = line.Split(':');        // Структура passwd: имя:пароль:UID:GID:GECOS:домашняя_папка:оболочка
                if (parts.Length < 7) continue;

                if (int.TryParse(parts[2], out int uid) &&
                    int.TryParse(parts[3], out int gid))
                {
                    users.Add(new UserEntry
                    {
                        Username = parts[0],
                        Uid = uid,
                        Gid = gid,
                        HomeDirectory = parts[5],
                        Shell = parts[6]
                    });
                }
            }
            return users;
        }

        /// <summary>
        /// Читает файл /etc/group и возвращает список групп.
        /// </summary>
        private List<GroupEntry> ReadGroupFile()
        {
            var groups = new List<GroupEntry>();     // Результирующий список групп
            if (!File.Exists(GroupPath))
                throw new FileNotFoundException($"File not found: {GroupPath}");

            foreach (var line in File.ReadLines(GroupPath))
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                    continue;

                var parts = line.Split(':');         // Структура group: имя:пароль:GID:список_пользователей
                if (parts.Length < 4) continue;

                var members = parts[3].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                if (int.TryParse(parts[2], out int gid))
                {
                    groups.Add(new GroupEntry
                    {
                        Name = parts[0],
                        Gid = gid,
                        Members = members.ToList()
                    });
                }
            }
            return groups;
        }

        /// <summary>
        /// Вспомогательный класс для хранения данных о группе.
        /// </summary>
        private class GroupEntry
        {
            public string Name { get; set; }               // Название группы
            public int Gid { get; set; }                   // Идентификатор группы
            public List<string> Members { get; set; } = new List<string>(); // Список пользователей в группе
        }
    }
}
