using LinuxAPI;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using Terminal.Gui;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace LinCGutil
{
    //компонент для отображения раздела диска
    public class DiskPartView:View
    {
        Label LName;
        Label LSize;
        Label LMaoutPoint;
        PartitionInfo Partition;
        public DiskPartView(PartitionInfo _Partition)
        {
            Partition = _Partition;

            Width = Dim.Fill();
            Height = 4;

            FrameView line = new FrameView();
            line.Width = Dim.Fill() - 2;
            line.X = 2;
            line.Height = 1;
            Add(line);

            LName = new Label();
            LName.Text = Partition.DeviceName;
            LName.X = 2;
            LName.Y = 1;
            Add(LName);

            LSize = new Label();
            string used = (Partition.UsedBytes * 1.0 / (1024L * 1024L * 1024L)).ToString("0.0");
            string size = (Partition.SizeBytes * 1.0 / (1024L * 1024L * 1024L)).ToString("0.0");
            LSize.Text = "Used: " + used + "/" + size + "Gb";
            LSize.X = 2;
            LSize.Y = 2;
            Add(LSize);

            LMaoutPoint = new Label();
            if(Partition.MountPoint!=null)
                LMaoutPoint.Text = "Mount to: "+ Partition.MountPoint;
            else
                LMaoutPoint.Text = "Not mount";
            LMaoutPoint.X = 2;
            LMaoutPoint.Y = 3;
            Add(LMaoutPoint);
        }
    }
}
