using System.ComponentModel;

namespace BlazorWebSSD
{
    //сервис отвечает за запуск и остановку объекта вервера при запуске и остановке приложения
    public class NasService : IHostedService
    {
        private readonly MyServer _worker;
        DisksConfig DisksConfig;
        SharedFoldersConfig SharedFoldersConfig;

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
            return _worker.StartAsync(cancellationToken);
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
                        disk.Partitions[0].Mount("DiskShare"+i);
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
                    disk.Partitions[0].Mount("DiskBackUp" + i);
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
    }
}
