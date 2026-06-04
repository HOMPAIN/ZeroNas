using Terminal.Gui.ViewBase;
using Terminal.Gui.App;
using Terminal.Gui.Views;
using LinCGutil;

using IApplication app = Application.Create();
app.Init();

using Window window = new() { Title = "Hello World (Esc to quit)" };
DiskManagerView dmv = new DiskManagerView();
window.Add(dmv);

app.Run(window);