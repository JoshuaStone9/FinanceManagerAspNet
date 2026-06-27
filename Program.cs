using FinanceManagerAspNet.Services;
using FinanceManagerAspNet.Data;
using FinanceManagerAspNet.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Data.SqlClient;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("FinanceManager")));

builder.Services.AddScoped<IItemRepository, ItemRepository>();
builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
builder.Services.AddScoped<ILocationRepository, LocationRepository>();
builder.Services.AddScoped<ITagRepository, TagRepository>();
builder.Services.AddScoped<IItemService, ItemService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<ILocationService, LocationService>();
builder.Services.AddScoped<ITagService, TagService>();
builder.Services.AddScoped<IImageService, ImageService>();
builder.Services.AddScoped<IStatsService, StatsService>();
builder.Services.AddScoped<IExportService, ExportService>();
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Auth/Login";
        options.AccessDeniedPath = "/Auth/Login";
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    });
builder.Services.AddAuthorization();
builder.Services.AddScoped<FinanceRepository>();
builder.Services.AddScoped<FinanceCalculator>();
builder.Services.AddScoped<AppAuthService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Dashboard/Error");
    app.UseHsts();
}

// app.UseHttpsRedirection();

app.Use(async (context, next) =>
{
    context.Response.Headers.TryAdd("X-Content-Type-Options", "nosniff");
    context.Response.Headers.TryAdd("X-Frame-Options", "DENY");
    context.Response.Headers.TryAdd("Referrer-Policy", "strict-origin-when-cross-origin");
    await next();
});

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "vault",
    pattern: "PersonalVault/{action=Index}/{id?}",
    defaults: new { controller = "Items" });

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Auth}/{action=Start}/{year?}/{month?}");

using (var scope = app.Services.CreateScope())
{
    var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    var connectionString = Environment.GetEnvironmentVariable("FM_CONNECTION_STRING")
        ?? configuration.GetConnectionString("FinanceManager")
        ?? throw new InvalidOperationException("Missing FinanceManager connection string.");

    EnsureSqlDatabaseExists(connectionString);

    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();

    var financeRepository = scope.ServiceProvider.GetRequiredService<FinanceRepository>();
    await financeRepository.EnsureModernTablesAsync();
}

app.Run();

static void EnsureSqlDatabaseExists(string connectionString)
{
    var builder = new SqlConnectionStringBuilder(connectionString);
    var databaseName = builder.InitialCatalog;

    if (string.IsNullOrWhiteSpace(databaseName))
    {
        return;
    }

    builder.InitialCatalog = "master";

    using var connection = new SqlConnection(builder.ConnectionString);
    connection.Open();

    using var command = connection.CreateCommand();
    command.CommandText = @"
IF DB_ID(@databaseName) IS NULL
BEGIN
    DECLARE @sql nvarchar(max) = N'CREATE DATABASE ' + QUOTENAME(@databaseName);
    EXEC(@sql);
END";
    command.Parameters.AddWithValue("@databaseName", databaseName);
    command.ExecuteNonQuery();
}
