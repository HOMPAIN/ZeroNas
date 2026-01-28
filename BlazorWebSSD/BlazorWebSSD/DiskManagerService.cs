namespace BlazorWebSSD
{
    public class DiskService
    {
        private readonly DiskManager _manager = new();

        public IReadOnlyList<DiskInfo> Disks => _manager.Disks;

        public void Refresh()
        {
            _manager.Refresh();
        }
    }
}
