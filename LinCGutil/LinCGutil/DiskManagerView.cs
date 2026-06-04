using System;
using System.Collections.Generic;
using System.Text;
using Terminal.Gui.ViewBase;
using LinuxAPI;
using System.Reflection.Emit;

namespace LinCGutil
{
    internal class DiskManagerView:View
    {
        public DiskManagerView()
        {
            Width = Dim.Fill();
            Height = Dim.Fill();

            List<DiskInfo>? disks = DiskManager.GetDisks();

            if (disks == null)
            {
                Terminal.Gui.Views.Label l_error =new Terminal.Gui.Views.Label();
                l_error.Text = "Loading disks error!!!";
                Add(l_error);
                return;
            }

            View? last = null;
            for (int i = 0; i < disks.Count; i++)
            {
                DiskView disk = new DiskView(disks[i]);
                if(last != null)
                    disk.Y = Pos.Bottom(last);
                last = disk;
                Add(disk);
            }
        }
    }
}
