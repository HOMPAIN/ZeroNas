using System.IO;
using System.Reflection;
using System.Text.Json;

namespace BlazorWebSSD
{
    //конфигурация дисков в системе
    public class DisksConfig : ConfigBase<DisksConfig>
    {
        public List<String> IDBackup { get; set; } = new List<String>();//id дисков используемые для бэкапа
        public List<String> IDShare { get; set; } = new List<String>();//id дисков используемые для шагринга


        public DisksConfig()
        {
            // Путь по умолчанию
            SavePath = "DisksConfig.txt";
        }
        //получить тип использования диска по ID.
        public int FindDisk(String _ID)
        {
            if (IDBackup.Contains(_ID))
                return 1;//диск для бэкапа

            if (IDShare.Contains(_ID))
                return 2;//диск для шаринга

            return 0;
        }
        public void AddDisk(String _ID, int _Type)
        {
            if(_Type!=1 && _Type != 2)
            {
                throw new FileNotFoundException($"Регистрация диска с недопустимым типом, тип: {_Type}");
            }
            if(FindDisk(_ID)!=0)
            {
                throw new FileNotFoundException($"Диск с таким ID уже зарегистрирован: {_ID}");
            }

            if(_Type==1)
                IDBackup.Add(_ID);
            if(_Type==2)
                IDShare.Add(_ID);

            Save();
        }
    }
}
