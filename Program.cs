using Microsoft.EntityFrameworkCore;
using VatTuPro.Data;
using VatTuPro.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddDbContext<AppDbContext>(opts =>
    opts.UseSqlite("Data Source=vattu.db"));

builder.Services.AddScoped<WarehouseService>();

var app = builder.Build();

// Migrate & seed database. If schema is out of sync (e.g. new tables added),
// recreate the database so seed data is applied with the latest model.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
    try 
    { 
        _ = db.AuditLogs.OrderBy(x => x.Id).FirstOrDefault(); 
        _ = db.Transactions.OrderBy(x => x.Id).FirstOrDefault();
        _ = db.TransactionItems.OrderBy(x => x.Id).FirstOrDefault();
        _ = db.StorageZones.OrderBy(x => x.Id).FirstOrDefault();  // detect new schema
    }
    catch
    {
        db.Database.EnsureDeleted();
        db.Database.EnsureCreated();
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<VatTuPro.Components.App>()
    .AddInteractiveServerRenderMode();

var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
app.Run($"http://0.0.0.0:{port}");
