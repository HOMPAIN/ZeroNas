using Terminal.Gui.ViewBase;
using Terminal.Gui.App;
using Terminal.Gui.Views;
using LinCGutil;
using Terminal.Gui.Drawing;

using IApplication app = Application.Create();
app.Init();

using Window window = new() { Title = "Hello World (Esc to quit)" };

Scheme scheme = new Scheme() {
    Normal = new Terminal.Gui.Drawing.Attribute(Color.White, Color.DarkGray),
    
};

//кнопка выбора дисковой утилиты
Label LDisk = new Label();
LDisk.X = 0;
LDisk.Y = 0;
LDisk.Width = 20;
LDisk.Height = 3;
LDisk.Text = "Disk Manager";
LDisk.TextAlignment = Alignment.Center;
LDisk.VerticalTextAlignment = Alignment.Center;
LDisk.SetScheme(scheme);
window.Add(LDisk);

//кнопка выбора утилиты пользователей
Label LUser = new Label();
LUser.X = 0;
LUser.Y = Pos.Bottom(LDisk);
LUser.Width = LDisk.Width;
LUser.Height = LDisk.Height;
LUser.Text = "User Manager";
LUser.TextAlignment = Alignment.Center;
LUser.VerticalTextAlignment = Alignment.Center;
window.Add(LUser);




DiskManagerView dmv = new DiskManagerView();
dmv.X = Pos.Right(LDisk);
window.Add(dmv);

app.Run(window);