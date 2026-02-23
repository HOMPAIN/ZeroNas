using System.ComponentModel;

namespace BlazorWebSSD
{
    //сервис отвечает за запуск и остановку объекта вервера при запуске и остановке приложения
    public class NasService : IHostedService
    {
        private readonly MyServer _worker;
        DisksConfig DisksConfig;
        SharedFoldersConfig SharedFoldersConfig;

        public string MountPoint="/mnt";//каталог для монтирования дисков

        public NasService(MyServer worker, DisksConfig _DC, SharedFoldersConfig _SFC)
        {
            _worker = worker;
            try
            {
                Console.WriteLine("Загрузка конфигурации дисков");
                _DC.Load();
                Console.WriteLine("Загрузка конфигурации сетевых папок");
                _SFC.Load();

                DisksConfig = _DC;
                SharedFoldersConfig = _SFC;
            }
            catch
            { }
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            return  _worker.StartAsync(cancellationToken);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return _worker.StopAsync();
        }
        //смонтировать все сконфигурированные диски 
        public void MountAllDisks()
        {
            List<DiskInfo>? disks = DiskManager.GetDisks();
            if (disks == null) return;
            //диски для шаринга
            for(int i=0;i<DisksConfig.IDShare.Count;i++)
            {
                foreach (DiskInfo disk in disks)
                {
                    if (disk.Partitions == null) continue;
                    if (disk.Partitions.Count != 1)  continue;
                    if (disk.Serial != DisksConfig.IDShare[i]) continue;
                    string path = Path.Combine(MountPoint, "DiskShare" + i);
                    disk.Partitions[0].Mount(path);
                }
            }
            //диски для бэкапа
            for (int i = 0; i < DisksConfig.IDBackup.Count; i++)
            {
                foreach (DiskInfo disk in disks)
                {
                    if (disk.Partitions == null) continue;
                    if (disk.Partitions.Count != 1) continue;
                    if (disk.Serial != DisksConfig.IDBackup[i]) continue;
                    string path = Path.Combine(MountPoint, "DiskBackUp" + i);
                    disk.Partitions[0].Mount(path);
                }
            }

        }
        //смонтировать все диски для сетевых папок
        public void MountAllShare()
        {
            List<DiskInfo>? disks = DiskManager.GetDisks();
            if (disks == null) return;
            //диски для шаринга
            for (int i = 0; i < DisksConfig.IDShare.Count; i++)
            {
                foreach (DiskInfo disk in disks)
                {
                    if (disk.Partitions == null) continue;
                    if (disk.Partitions.Count != 1) continue;
                    if (disk.Serial != DisksConfig.IDShare[i]) continue;
                    string path = Path.Combine(MountPoint, "DiskShare" + i);
                    disk.Partitions[0].Mount(path);
                }
            }
        }
        //смонтировать все диски для бэкапа
        public void MountAllBackUp()
        {
            List<DiskInfo>? disks = DiskManager.GetDisks();
            if (disks == null) return;
            //диски для бэкапа
            for (int i = 0; i < DisksConfig.IDBackup.Count; i++)
            {
                foreach (DiskInfo disk in disks)
                {
                    if (disk.Partitions == null) continue;
                    if (disk.Partitions.Count != 1) continue;
                    if (disk.Serial != DisksConfig.IDBackup[i]) continue;
                    string path = Path.Combine(MountPoint, "DiskBackUp" + i);
                    disk.Partitions[0].Mount(path);
                }
            }
        }
        //размантировать все сконфигурированные диски 
        public void UnmountAll()
        {
            List<DiskInfo>? disks = DiskManager.GetDisks();
            if (disks == null) return;
            //диски для шаринга
            for (int i = 0; i < DisksConfig.IDShare.Count; i++)
            {
                foreach (DiskInfo disk in disks)
                {
                    if (disk.Partitions == null) continue;
                    if (disk.Partitions.Count != 1) continue;
                    if (disk.Serial != DisksConfig.IDShare[i]) continue;
                    disk.Partitions[0].Unmount();
                }
            }
            //диски для бэкапа
            for (int i = 0; i < DisksConfig.IDBackup.Count; i++)
            {
                foreach (DiskInfo disk in disks)
                {
                    if (disk.Partitions == null) continue;
                    if (disk.Partitions.Count != 1) continue;
                    if (disk.Serial != DisksConfig.IDBackup[i]) continue;
                    disk.Partitions[0].Unmount();
                }
            }
        }
        //размонтировать все тетевые папки
        public void UnmountAllShared()
        {
            List<DiskInfo>? disks = DiskManager.GetDisks();
            if (disks == null) return;
            //диски для шаринга
            for (int i = 0; i < DisksConfig.IDShare.Count; i++)
            {
                foreach (DiskInfo disk in disks)
                {
                    if (disk.Partitions == null) continue;
                    if (disk.Partitions.Count != 1) continue;
                    if (disk.Serial != DisksConfig.IDShare[i]) continue;
                    disk.Partitions[0].Unmount();
                }
            }
        }
        //размонтировать все папки для бэкапа
        public void UnmountAllBackUp()
        {
            List<DiskInfo>? disks = DiskManager.GetDisks();
            if (disks == null) return;
            //диски для бэкапа
            for (int i = 0; i < DisksConfig.IDBackup.Count; i++)
            {
                foreach (DiskInfo disk in disks)
                {
                    if (disk.Partitions == null) continue;
                    if (disk.Partitions.Count != 1) continue;
                    if (disk.Serial != DisksConfig.IDBackup[i]) continue;
                    disk.Partitions[0].Unmount();
                }
            }
        }
        //расшарить все сетевые папки
        public void SambaShareAll()
        {
            //сначала удалим все шары
            SambaManager.RemoveAllShares();

            //теперь добавим новые согласно конфигу
            for (int j = 0; j < SharedFoldersConfig.SharedFolders.Count; j++)
            {
                SharedFolder share = SharedFoldersConfig.SharedFolders[j];
                //ищим на каком диске и в какой папке находится сетевое хранилище
                for (int i = 0; i < DisksConfig.IDShare.Count; i++)
                {
                    if (DisksConfig.IDShare[i] == share.MainDisk)
                    {
                        string path = Path.Combine(MountPoint,"DiskShare" + i , share.Folder);
                        //расшариваем папку
                        SambaManager.AddShare(share.Folder, path, share.UsersRW);
                        break;
                    }
                }
            }
        }
        //отменить шаринг всех папок
        public void UnSambaShareAll()
        {
            SambaManager.RemoveAllShares();
        }
    }
}
