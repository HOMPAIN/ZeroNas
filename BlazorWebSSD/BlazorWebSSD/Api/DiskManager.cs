using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace BlazorWebSSD
{
    /// <summary>
    /// Представляет раздел диска (partition) в Linux.
    /// </summary>
    public class PartitionInfo
    {
        /// <summary>Имя устройства раздела (например, "sda1").</summary>
        public string DeviceName { get; set; } = "—";

        /// <summary>Размер раздела в байтах.</summary>
        public long SizeBytes { get; set; } = 0;

        /// <summary>Количество занятого места в байтах (если раздел смонтирован).</summary>
        public long UsedBytes { get; set; } = 0;

        /// <summary>Текущая точка монтирования (может быть null или "—").</summary>
        public string MountPoint { get; set; } = "—";
        /// <summary>Тип файловой системы (например, "ext4", "ntfs", "vfat"). Может быть null или "—".</summary>
        public string FileSystemType { get; set; } = "—";

        /// <summary>
        /// Монтирует раздел в указанную директорию.
        /// Создаёт директорию, если она не существует.
        /// </summary>
        /// <param name="mountPath">Путь для монтирования (например, "/mnt/mydisk").</param>
        /// <returns>true, если монтирование успешно; иначе false.</returns>
        public bool Mount(string mountPath)
        {
            Console.WriteLine(DeviceName + " mount to " + mountPath);
            if (string.IsNullOrWhiteSpace(mountPath))
                return false;

            // Убедимся, что путь абсолютный
            mountPath = Path.GetFullPath(mountPath);

            try
            {
                // Создаём точку монтирования, если не существует
                if (!Directory.Exists(mountPath))
                {
                    Directory.CreateDirectory(mountPath);
                }

                // Выполняем mount /dev/DEVICE PATH
                var devicePath = $"/dev/{DeviceName}";
                var result = RunCommand("mount", $"{devicePath} {mountPath}");
                if (result != null)
                {
                    if (mountPath == null) mountPath = "-";
                    MountPoint = mountPath;
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Ошибка при монтировании: {ex.Message}");
            }
            return false;
        }

        /// <summary>
        /// Размонтирует раздел.
        /// Если MountPoint не установлен, пытается найти текущую точку через findmnt.
        /// </summary>
        /// <returns>true, если размонтирование успешно; иначе false.</returns>
        public bool Unmount()
        {
            string? target = MountPoint;

            // Если MountPoint не задан — попробуем найти через findmnt
            if (string.IsNullOrEmpty(target) || target == "—")
            {
                var findmntOut = RunCommand("findmnt", $"-n -o TARGET --source /dev/{DeviceName}");
                if (!string.IsNullOrEmpty(findmntOut))
                {
                    target = findmntOut.Trim();
                }
            }

            if (string.IsNullOrEmpty(target))
            {
                Console.Error.WriteLine($"Не удалось определить точку монтирования для {DeviceName}");
                return false;
            }

            try
            {
                var result = RunCommand("umount", target);
                if (result != null)
                {
                    MountPoint = "-";
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Ошибка при размонтировании: {ex.Message}");
            }
            return false;
        }

        /// <summary>
        /// Форматирует раздел в указанную файловую систему.
        /// Поддерживаемые типы: "ext4", "exfat", "ntfs".
        /// Перед форматированием раздел будет размонтирован.
        /// </summary>
        /// <param name="fileSystemType">Тип файловой системы (например, "ext4").</param>
        /// <param name="label">Метка тома (опционально).</param>
        /// <returns>true, если форматирование успешно; иначе false.</returns>
        public bool Format(string fileSystemType, string? label = null)
        {
            if (string.IsNullOrWhiteSpace(fileSystemType))
                throw new ArgumentException("File system type cannot be null or empty.", nameof(fileSystemType));

            fileSystemType = fileSystemType.ToLowerInvariant();

            // Проверяем поддерживаемые типы
            if (fileSystemType != "ext4" && fileSystemType != "exfat" && fileSystemType != "ntfs")
                throw new NotSupportedException($"Unsupported file system type: {fileSystemType}");

            Console.WriteLine($"Formatting /dev/{DeviceName} as {fileSystemType}...");

            // 1. Размонтируем, если смонтирован
            if (MountPoint != "—" && MountPoint != "-")
            {
                Console.WriteLine($"Unmounting {MountPoint} before formatting...");
                if (!Unmount())
                {
                    Console.Error.WriteLine("Failed to unmount partition. Formatting aborted.");
                    return false;
                }
            }

            // 2. Собираем аргументы для mkfs
            string command;
            string args;

            switch (fileSystemType)
            {
                case "ext4":
                    command = "mkfs.ext4";
                    args = $"-F /dev/{DeviceName}";
                    if (!string.IsNullOrEmpty(label))
                        args += $" -L \"{label}\"";
                    break;

                case "exfat":
                    command = "mkfs.exfat";
                    args = $"/dev/{DeviceName}";
                    if (!string.IsNullOrEmpty(label))
                        args += $" -n \"{label}\"";
                    break;

                case "ntfs":
                    command = "mkfs.ntfs";
                    args = $"-f /dev/{DeviceName}"; // -f = fast format
                    if (!string.IsNullOrEmpty(label))
                        args += $" -L \"{label}\"";
                    break;

                default:
                    return false;
            }

            // 3. Выполняем форматирование
            try
            {
                var result = RunCommand(command, args);
                if (result != null)
                {
                    Console.WriteLine($"Successfully formatted /dev/{DeviceName} as {fileSystemType}.");
                    return true;
                }
                else
                {
                    Console.Error.WriteLine($"Failed to format /dev/{DeviceName} as {fileSystemType}.");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Exception during formatting: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Удобный метод для быстрого форматирования в ext4 (рекомендуется для Samba).
        /// </summary>
        public bool FormatAsExt4(string? label = null) => Format("ext4", label);

        // Вспомогательный метод для запуска команд (локальная копия из DiskManager или общий)
        private static string? RunCommand(string command, string args)
        {
            try
            {
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = command,
                        Arguments = args,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode == 0)
                    return output;
                else
                {
                    Console.Error.WriteLine($"Command failed: {command} {args}\nError: {error}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Exception in RunCommand: {ex.Message}");
                return null;
            }
        }
        /// <summary>
        /// Возвращает строковое представление раздела для отладки.
        /// </summary>
        public override string ToString()
        {
            string sizeGb = (SizeBytes / (1024.0 * 1024 * 1024)).ToString("F2");
            string usedGb = (UsedBytes / (1024.0 * 1024 * 1024)).ToString("F2");

            return $"/dev/{DeviceName} | FS: {FileSystemType} | Size: {sizeGb} GB | Used: {usedGb} GB | Mount: {MountPoint}";
        }
    }

    /// <summary>
    /// Представляет физический диск в системе Linux (например, USB-SSD, microSD, eMMC).
    /// Содержит информацию об устройстве и список его разделов.
    /// </summary>
    public class DiskInfo
    {
        /// <summary>Имя диска, присвоенное ядром (например, "sda", "mmcblk0").</summary>
        public string DeviceName { get; set; } = "—";

        /// <summary>Общий размер диска в байтах.</summary>
        public long DeviceSizeBytes { get; set; } = 0;

        /// <summary>Модель устройства (например, "00SSD1", "TW000").</summary>
        public string Model { get; set; } = "—";

        /// <summary>Серийный номер устройства (если доступен).</summary>
        public string Serial { get; set; } = "—";

        /// <summary>Список разделов, принадлежащих этому диску.</summary>
        public List<PartitionInfo> Partitions { get; set; } = new();
    }

    /// <summary>
    /// Управляет обнаружением и сбором информации о дисках и разделах в Linux.
    /// Использует системные утилиты lsblk, df и udevadm для получения данных.
    /// </summary>
    public static class DiskManager
    {
        /// <summary>
        /// Запрашивает информацию о дисках и разделах, выполняя системные команды
        /// и парся их вывод.
        /// </summary>
        public static Task<List<DiskInfo>?> GetDisksAsync()
        {
            return Task.Run(GetDisks);
        }
        /// <summary>
        /// Запрашивает информацию о дисках и разделах, выполняя системные команды
        /// и парся их вывод.
        /// </summary>
        public static List<DiskInfo>? GetDisks()
        {
            List<DiskInfo> disks = new List<DiskInfo>();

            // Добавлено FSTYPE в вывод
            var lsblkJson = RunCommand("lsblk", "-J -b -o NAME,SIZE,TYPE,MOUNTPOINT,FSTYPE");
            if (string.IsNullOrEmpty(lsblkJson)) return null;

            using var doc = JsonDocument.Parse(lsblkJson);
            if (!doc.RootElement.TryGetProperty("blockdevices", out var devices)) return null;

            foreach (var dev in devices.EnumerateArray())
            {
                if (!dev.TryGetProperty("name", out var nameEl) ||
                    !dev.TryGetProperty("size", out var sizeEl) ||
                    !dev.TryGetProperty("type", out var typeEl)) continue;

                var name = nameEl.GetString();
                var size = sizeEl.GetInt64();
                var type = typeEl.GetString();

                if (type == "disk")
                {
                    var disk = new DiskInfo
                    {
                        DeviceName = name!,
                        DeviceSizeBytes = size,
                        Model = ReadDeviceModel(name!),
                        Serial = ReadDeviceSerial(name!)
                    };

                    if (dev.TryGetProperty("children", out var children) && children.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var part in children.EnumerateArray())
                        {
                            if (!part.TryGetProperty("name", out var pNameEl) ||
                                !part.TryGetProperty("size", out var pSizeEl)) continue;

                            var partName = pNameEl.GetString();
                            var partSize = pSizeEl.GetInt64();
                            var mountPoint = part.TryGetProperty("mountpoint", out var mp) ? mp.GetString() : "-";
                            if (mountPoint == null) mountPoint = "-";

                            // === НОВОЕ: чтение типа файловой системы ===
                            var fsType = part.TryGetProperty("fstype", out var fs) ? fs.GetString() : "—";
                            if (string.IsNullOrEmpty(fsType)) fsType = "—";

                            var partition = new PartitionInfo
                            {
                                DeviceName = partName!,
                                SizeBytes = partSize,
                                MountPoint = mountPoint,
                                FileSystemType = fsType!
                            };

                            if (!string.IsNullOrEmpty(mountPoint) && mountPoint != "-")
                            {
                                var dfOutput = RunCommand("df", $"--output=used {mountPoint} -B1");
                                if (!string.IsNullOrEmpty(dfOutput))
                                {
                                    var lines = dfOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                                    if (lines.Length > 1 && long.TryParse(lines[1].Trim(), out var used))
                                    {
                                        partition.UsedBytes = used;
                                    }
                                }
                            }

                            disk.Partitions.Add(partition);
                        }
                    }

                    disks.Add(disk);
                }
            }
            return disks;
        }

        /// <summary>
        /// Выполняет указанную системную команду с аргументами и возвращает её stdout,
        /// если команда завершилась успешно. В противном случае — null.
        /// </summary>
        /// <param name="command">Имя исполняемого файла (например, "lsblk").</param>
        /// <param name="args">Аргументы команды.</param>
        /// <returns>Вывод команды или null при ошибке.</returns>
        private static string? RunCommand(string command, string args)
        {
            try
            {
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = command,
                        Arguments = args,
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                return process.ExitCode == 0 ? output : null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Читает модель устройства из sysfs (например, из /sys/block/sda/device/model).
        /// </summary>
        /// <param name="deviceName">Имя устройства (например, "sda").</param>
        /// <returns>Модель или "—" если недоступна.</returns>
        private static string ReadDeviceModel(string deviceName)
        {
            var modelPath1 = $"/sys/block/{deviceName}/device/model";
            var modelPath2 = $"/sys/block/{deviceName}/device/name";

            foreach (var path in new[] { modelPath1, modelPath2 })
            {
                if (File.Exists(path))
                {
                    try
                    {
                        var model = File.ReadAllText(path).Trim();
                        if (!string.IsNullOrWhiteSpace(model))
                            return model;
                    }
                    catch { /* ignore */ }
                }
            }
            return "—";
        }

        /// <summary>
        /// Получает серийный номер устройства через утилиту udevadm.
        /// Формат: ID_SERIAL=Vendor_Model_Serial → возвращает только Serial.
        /// </summary>
        /// <param name="deviceName">Имя устройства (например, "sda").</param>
        /// <returns>Серийный номер или "—" если недоступен.</returns>
        private static string ReadDeviceSerial(string deviceName)
        {
            var output = RunCommand("udevadm", $"info --name=/dev/{deviceName} --query=property");
            if (string.IsNullOrEmpty(output)) return "—";

            foreach (var line in output.Split('\n'))
            {
                if (line.StartsWith("ID_SERIAL=", StringComparison.Ordinal))
                {
                    var serial = line.Substring("ID_SERIAL=".Length).Trim();
                    if (serial.Contains('_'))
                    {
                        var parts = serial.Split('_');
                        if (parts.Length >= 3)
                            return string.Join("_", parts[2..]);
                    }
                    return serial;
                }
            }
            return "—";
        }
    }
}