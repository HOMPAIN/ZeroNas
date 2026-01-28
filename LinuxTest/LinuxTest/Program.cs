// See https://aka.ms/new-console-template for more information
using LinuxTest;

var dm = new DiskManager();
dm.Refresh();

foreach (var disk in dm.Disks)
{
    Console.WriteLine($"Disk: {disk.DeviceName}");
    Console.WriteLine($"  Model:    {disk.Model}");
    Console.WriteLine($"  Serial:   {disk.Serial}");
    Console.WriteLine($"  Size:     {disk.DeviceSizeBytes / (1024L * 1024 * 1024):F1} GB");

    if (disk.Partitions.Count == 0)
    {
        Console.WriteLine("  Partitions: none");
    }
    else
    {
        Console.WriteLine("  Partitions:");
        foreach (var part in disk.Partitions)
        {
            Console.WriteLine($"    - {part.DeviceName}");
            Console.WriteLine($"      Size:  {part.SizeBytes / (1024L * 1024):F0} MB");
            Console.WriteLine($"      Used:  {part.UsedBytes / (1024L * 1024):F0} MB");
            Console.WriteLine($"      Mount: {part.MountPoint ?? "—"}");
        }
    }
    Console.WriteLine();
}

/*
// Найти нужный раздел
var ssdPartition = dm.Disks
    .FirstOrDefault(d => d.DeviceName == "sda")
    ?.Partitions.FirstOrDefault();

if (ssdPartition != null)
{
    // Смонтировать
    if (ssdPartition.Mount("/mnt/ssd"))
    {
        Console.WriteLine($"Смонтировано: {ssdPartition.MountPoint}");
    }

    Console.ReadLine();

    // Позже — размонтировать
    if (ssdPartition.Unmount())
    {
        Console.WriteLine("Размонтировано успешно");
    }
}*/

var userManager = new UsersManager();
var users = userManager.GetAllUsersWithGroups();

foreach (var user in users)
{
    Console.WriteLine($"{user.Username} (UID: {user.Uid})");
    Console.WriteLine($"  Home: {user.HomeDirectory}, Shell: {user.Shell}");
    Console.WriteLine($"  Groups: {string.Join(", ", user.Groups)}");
    Console.WriteLine();
}

// Добавить пользователя с домашней папкой и в группу sudo
//userManager.AddUser("testuser", createHome: true, groups: new[] { "sudo" });

// Удалить пользователя и его домашнюю папку
//userManager.RemoveUser("testuser", removeHome: true);


//тест вифи
// Список доступных сетей
foreach (var net in WiFiManager.GetAvailableNetworks())
    Console.WriteLine($"SSID: {net.Ssid}, Security: {net.Security}");

// Подключение (раскомментируйте, если нужно)
// WiFiManager.ConnectToNetwork("MyWiFi", "password123");

// Сохранённые сети
foreach (var saved in WiFiManager.GetSavedNetworks())
    Console.WriteLine($"Saved: {saved.Ssid}");

// Информация о текущем подключении
var info = WiFiManager.GetCurrentConnectionInfo();
if (info != null)
{
    Console.WriteLine($"Connected to: {info.Ssid}");
    Console.WriteLine($"IP: {info.IpAddress}");
    Console.WriteLine($"Signal: {info.SignalStrength}");
    Console.WriteLine($"Speed: {info.Speed}");
}
else
{
    Console.WriteLine("No active Wi-Fi connection.");
}

//тест самбы
const string username = "test_samba_user";
const string password = "123456781";
const string shareName = "media_share";
const string sharePath = "/media"; // убедитесь, что папка существует

try
{
    Console.WriteLine("🧪 Начинаем тест SambaManager...\n");

    // 1. Создать пользователя
    Console.WriteLine($"➡️  Создаём пользователя: {username}");
    SambaManager.AddUser(username, password);
    Console.WriteLine("✅ Пользователь создан.\n");

    // 2. Показать список пользователей
    Console.WriteLine("👥 Список пользователей Samba:");
    var s_users = SambaManager.GetSambaUsers();
    foreach (var user in s_users)
    {
        Console.WriteLine($"  - {user}");
    }
    Console.WriteLine();

    // 3. Убедиться, что папка /media существует
    if (!System.IO.Directory.Exists(sharePath))
    {
        throw new System.IO.DirectoryNotFoundException($"Папка {sharePath} не найдена. Создайте её или укажите другую.");
    }

    // 4. Создать сетевую папку (шару) с доступом для пользователя на чтение и запись
    Console.WriteLine($"📁 Добавляем шару '{shareName}' → {sharePath} для пользователя {username} (чтение+запись)");
    SambaManager.AddShare(shareName, sharePath, new List<string> { username }, readOnly: false);
    Console.WriteLine("✅ Шара добавлена.\n");

    // 5. Показать список доступных папок
    Console.WriteLine("📂 Доступные шары:");
    var shares = SambaManager.GetShares();
    foreach (var share in shares)
    {
        Console.WriteLine($"  Имя: {share.ShareName}");
        Console.WriteLine($"  Путь: {share.Path}");
        Console.WriteLine($"  Только чтение: {share.ReadOnly}");
        Console.WriteLine();
    }

    // 6. Дождаться ввода с клавиатуры
    Console.WriteLine("⏳ Нажмите Enter для продолжения (удаления шары и пользователя)...");
    Console.ReadLine();

    // 7. Удалить сетевое расположение (все шары — или можно удалить только одну)
    // В текущей реализации RemoveAllShares() удаляет всё, кроме [global]
    // Если вы хотите удалять только одну шару — нужно доработать метод.
    // Пока воспользуемся RemoveAllShares(), так как в тесте она одна.
    Console.WriteLine("🗑️  Удаляем все шары...");
    SambaManager.RemoveAllShares();
    Console.WriteLine("✅ Все шары удалены.\n");

    // 8. Удалить пользователя
    Console.WriteLine($"👤 Удаляем пользователя: {username}");
    SambaManager.RemoveUser(username);
    Console.WriteLine("✅ Пользователь удалён.\n");

    Console.WriteLine("🎉 Тест завершён успешно!");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Ошибка: {ex.Message}");
    Console.WriteLine(ex.StackTrace);
}
finally
{
    Console.WriteLine("\nНажмите любую клавишу для выхода...");
    Console.ReadKey();
}