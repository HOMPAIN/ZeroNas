using BlazorWebSSD;
using BlazorWebSSD.Components;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddSingleton<DiskManager>();
builder.Services.AddSingleton<UsersManager>();
builder.Services.AddSingleton<DisksConfig>();
builder.Services.AddSingleton<MyServer>();

// Регистрация фоновой службы
builder.Services.AddHostedService<MyService>();
builder.Services.AddHostedService<WiFiMonitoringService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
