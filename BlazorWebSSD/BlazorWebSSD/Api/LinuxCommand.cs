using System.Diagnostics;

namespace BlazorWebSSD
{
    /// <summary>
    /// Статический хелпер для выполнения системных команд в Linux.
    /// Все методы автоматически используют sudo для привилегированных операций.
    /// </summary>
    public static class LinuxCommand
    {
        /// <summary>
        /// Выполняет команду и возвращает вывод, если код выхода = 0.
        /// </summary>
        public static string? Run(string command)
        {
            return Execute(command, null, useSudo: false, timeoutMs: null);
        }
        /// <summary>
        /// Выполняет команду и возвращает вывод, если код выхода = 0.
        /// </summary>
        public static string? Run(string command, string arguments)
        {
            return Execute(command, arguments, useSudo: false, timeoutMs: null);
        }
        /// <summary>
        /// Выполняет команду с sudo и возвращает вывод, если код выхода = 0.
        /// </summary>
        public static string? RunSudo(string command)
        {
            return Execute(command, null, useSudo: true, timeoutMs: null);
        }
        /// <summary>
        /// Выполняет команду с sudo и возвращает вывод, если код выхода = 0.
        /// </summary>
        public static string? RunSudo(string command, string arguments)
        {
            return Execute(command, arguments, useSudo: true, timeoutMs: null);
        }

        /// <summary>
        /// Выполняет команду с возможностью отключения sudo.
        /// </summary>
        public static string? Run(string command, string arguments, bool useSudo)
        {
            return Execute(command, arguments, useSudo, timeoutMs: null);
        }

        /// <summary>
        /// Выполняет команду с таймаутом на выполнение (в миллисекундах).
        /// </summary>
        public static string? Run(string command, string arguments, int timeoutMs)
        {
            return Execute(command, arguments, useSudo: true, timeoutMs);
        }

        /// <summary>
        /// Асинхронная версия выполнения команды.
        /// </summary>
        public static async Task<string?> RunAsync(string command, string arguments, CancellationToken ct = default)
        {
            return await ExecuteAsync(command, arguments, useSudo: true, ct);
        }

        /// <summary>
        /// Ядро выполнения: запускает процесс и возвращает stdout при успехе.
        /// </summary>
        private static string? Execute(string command, string? arguments, bool useSudo, int? timeoutMs)
        {
            try
            {
                // Безопасная обработка null-аргументов
                var safeArgs = arguments ?? string.Empty;

                var finalCommand = useSudo ? "sudo" : command;
                var finalArgs = useSudo ? $"{command} {safeArgs}".Trim() : safeArgs;

                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = finalCommand,
                        Arguments = finalArgs,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();

                // Читаем вывод асинхронно, чтобы избежать блокировки буфера
                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();

                if (timeoutMs.HasValue)
                {
                    if (!process.WaitForExit(timeoutMs.Value))
                    {
                        process.Kill();
                        Console.Error.WriteLine($"Command timed out: {finalCommand} {finalArgs}");
                        return null;
                    }
                }
                else
                {
                    process.WaitForExit();
                }

                var output = outputTask.Result;
                var error = errorTask.Result;

                if (process.ExitCode == 0)
                    return output;
                else
                {
                    Console.Error.WriteLine($"Command failed: {finalCommand} {finalArgs}\nError: {error}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Exception in LinuxCommandExecutor: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Асинхронная реализация ядра.
        /// </summary>
        private static async Task<string?> ExecuteAsync(string command, string? arguments, bool useSudo, CancellationToken ct = default)
        {
            try
            {
                var safeArgs = arguments ?? string.Empty;
                var finalCommand = useSudo ? "sudo" : command;
                var finalArgs = useSudo ? $"{command} {safeArgs}".Trim() : safeArgs;

                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = finalCommand,
                        Arguments = finalArgs,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();

                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();

                await Task.WhenAll(
                    process.WaitForExitAsync(ct),
                    outputTask,
                    errorTask
                );

                var output = await outputTask;
                var error = await errorTask;

                if (process.ExitCode == 0)
                    return output;
                else
                {
                    Console.Error.WriteLine($"Command failed: {finalCommand} {finalArgs}\nError: {error}");
                    return null;
                }
            }
            catch (OperationCanceledException)
            {
                Console.Error.WriteLine($"Command cancelled: {command} {arguments ?? "(null)"}");
                return null;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Exception in LinuxCommandExecutor: {ex.Message}");
                return null;
            }
        }
    }
}
