using BlazorWebSSD;
using BlazorWebSSD.Components;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddSingleton<DisksConfig>();
builder.Services.AddSingleton<SharedFoldersConfig>();
builder.Services.AddSingleton<MyServer>();
builder.Services.AddSingleton<NasService>();

// Регистрация фоновой службы
builder.Services.AddHostedService<NasService>();

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
