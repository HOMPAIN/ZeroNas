using System;
using System.Collections.Generic;
using System.Text;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using LinuxAPI;

namespace LinCGutil
{
    internal class DiskView:View
    {
        public FrameView Frame;
        public Label LName;
        public Terminal.Gui.Views.Button BShow;
        public Label Description;
        public List<DiskPartView> Parts = new List<DiskPartView>();
        int FulllHeight = 0;//ширина развёрнутого элемента
        View LastElement;
        DiskInfo DiskInfo;
        public DiskView(DiskInfo _DiskInfo)
        {
            DiskInfo = _DiskInfo;

            Width = 30;
            Height = 3;

            Frame = new FrameView();
            Frame.Width = Dim.Fill();
            Frame.Height = Dim.Fill();
            Add(Frame);

            LName = new Label();
            LName.X = 2;
            LName.Text = " "+ DiskInfo.DeviceName+" ";
            Add(LName);

            Description = new Label();
            string size = (DiskInfo.DeviceSizeBytes * 1.0 / (1024L * 1024L * 1024L)).ToString("0.0");
            Description.Text = DiskInfo.Model+" "+ size+"Gb";
            Description.X = 1;
            Description.Y = 1;
            Add(Description);

            BShow = new Button();
            BShow.Text = "Show";
            BShow.X = 30 - 9;
            BShow.Y = 1;
            BShow.Accepting += BShow_Clicked;
            Add(BShow);

            LastElement = Description;
            FulllHeight = 3;

            for (int i = 0; i < DiskInfo.Partitions.Count; i++)
            {
                DiskPartView part = new DiskPartView(DiskInfo.Partitions[i]);
                part.X = 1;
                part.Width = Dim.Fill() - 1;
                part.Y = Pos.Bottom(LastElement);
                Add(part);
                LastElement = part;
                FulllHeight += 4;
            }
        }

        private void BShow_Clicked(object? sender, Terminal.Gui.Input.CommandEventArgs e)
        {
            if (BShow.Text == "Show")
            {
                BShow.Text = "Hide";
                Height = FulllHeight;
            }
            else
            {
                BShow.Text = "Show";
                Height = 3;
            }
        }
    }
}
