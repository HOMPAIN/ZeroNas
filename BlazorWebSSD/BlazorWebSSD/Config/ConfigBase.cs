using System.Reflection;
using System.Text.Json;

namespace BlazorWebSSD
{
    //базовый класс конфига с возможностью сохранения и загрузки в файл
    public abstract class ConfigBase<T> where T : ConfigBase<T>, new()
    {
        public string SavePath { get; set; } = "";//путь сохранения

        // Сохраняет текущий экземпляр в файл
        public void Save()
        {
            Save(SavePath);
        }

        public void Save(string path)
        {
            SavePath = path;
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };
            string jsonString = JsonSerializer.Serialize(this as T, options);
            File.WriteAllText(path, jsonString);
        }

        // Загружает данные из файла в текущий экземпляр
        public void Load()
        {
            Load(SavePath);
        }

        public void Load(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException($"Файл конфигурации не найден: {path}");

            string jsonString = File.ReadAllText(path);
            var loaded = JsonSerializer.Deserialize<T>(jsonString) ?? new T();

            var thisType = this.GetType();
            foreach (var prop in thisType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!prop.CanWrite) continue;

                var loadedValue = prop.GetValue(loaded);
                prop.SetValue(this, loadedValue);
            }
        }

        // Статический метод для загрузки нового экземпляра из файла
        public static T LoadFromFile(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException($"Файл конфигурации не найден: {path}");

            string jsonString = File.ReadAllText(path);
            var config = JsonSerializer.Deserialize<T>(jsonString);
            return config ?? new T();
        }
    }
}
