namespace BlazorWebSSD
{
    public class SharedFolder
    {
        public string Folder { get; set; } = "TestFolder";//название папки
        public string MainDisk { get; set; } = "test disk id";//диск хранения
        public string BackupDisk { get; set; } = "";//диск для бэкапа
        public List<string> UsersRW { get; set; } = new List<string>();//список пользователей с полным доступом
        public List<string> UsersR { get; set; } = new List<string>();//список пользователей только для чтения
        public int Backup { get; set; } = 0;//частота бэкапа 0 - нет, 1 - каждый день, 2 - неделя, 3 - месяц
    }
    //конфигурация папки для шаринга
    public class SharedFoldersConfig : ConfigBase<SharedFoldersConfig>
    {
        public List<SharedFolder> SharedFolders { get; set; } = new List<SharedFolder>();

        public SharedFoldersConfig()
        {
            //путь по умолчанию
            SavePath = "SharedFoldersConfig.txt";
        }
        //добавить или обновить сетевую папку
        public void AddUpdateFolder(SharedFolder _Folder)
        {
            RemoveFolder(_Folder.Folder);
            SharedFolders.Add(_Folder);
            Save();
        }
        //удалить сетевую папку
        public void RemoveFolder(string _Folder)
        {
            for (int i = 0; i < SharedFolders.Count; i++)
            {
                if (SharedFolders[i].Folder==_Folder)
                {
                    SharedFolders.RemoveAt(i);
                    Save();
                    break;
                }
            }
        }
        public void RemoveFolder(SharedFolder _Folder)
        {
            for (int i = 0; i < SharedFolders.Count; i++)
            {
                if (SharedFolders[i] == _Folder)
                {
                    SharedFolders.RemoveAt(i);
                    Save();
                    break;
                }
            }
        }
    }
}
