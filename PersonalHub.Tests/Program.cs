using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalHub.Routing;
using PersonalHub.Services;

// Package-free regression checks: dotnet run --project PersonalHub.Tests
var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.WebHost.UseUrls("http://127.0.0.1:0");
builder.Services.AddControllersWithViews().AddApplicationPart(typeof(PersonalHub.Controllers.ItemsController).Assembly);
await using var app = builder.Build();
app.UseRouting();
// Inspect matched endpoints without starting the production app or accessing its databases.
app.Use(async (context, next) =>
{
    var action = context.GetEndpoint()?.Metadata.GetMetadata<ControllerActionDescriptor>();
    if (action is null) { await next(); return; }
    await context.Response.WriteAsync($"{(action.RouteValues.TryGetValue("area", out var routeArea) ? routeArea : "")}/{action.ControllerName}/{action.ActionName}");
});
app.MapApplicationRoutes();
await app.StartAsync();
var links = app.Services.GetRequiredService<LinkGenerator>();
var actions = app.Services.GetRequiredService<IActionDescriptorCollectionProvider>().ActionDescriptors.Items.OfType<ControllerActionDescriptor>();
var count = 0;
foreach (var action in actions)
{
    var area = (action.RouteValues.TryGetValue("area", out var routeArea) ? routeArea : "") ?? "";
    var path = links.GetPathByAction(action.ActionName, action.ControllerName, new { area, id = 42 });
    Check(path is not null, $"No URL for {area}/{action.ControllerName}/{action.ActionName}");
    Check(path!.StartsWith("/MoveInPlanner/") == (area == "MoveInPlanner"), $"Wrong area: {path}");
    count++;
}
using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
foreach (var (url, expected) in new[]
{
    ("/Items/Detail?id=42", "/Items/Detail"),
    ("/Items/Edit?id=42", "/Items/Edit"),
    ("/Dashboard/EditPayment?id=42", "/Dashboard/EditPayment"),
    ("/Dashboard/Index", "/Dashboard/Index"),
    ("/SavingPots/Index", "/SavingPots/Index"),
    ("/Statistics/Index", "/Statistics/Index"),
    ("/MoveInPlanner/Items/Details/42", "MoveInPlanner/Items/Details"),
    ("/MoveInPlanner/Items/Edit/42", "MoveInPlanner/Items/Edit"),
    ("/MoveInPlanner/Dashboard", "MoveInPlanner/Dashboard/Index"),
    ("/PersonalVault/Detail/42", "/Items/Detail"),
    ("/MoveInPlanner/Items/Detail/42", "/Items/Detail"),
    ("/MoveInPlanner/Dashboard/EditPayment/42", "/Dashboard/EditPayment")
})
    Check(await client.GetStringAsync(url) == expected, $"Wrong destination for {url}");

// Cross-section links must override the planner's ambient route values.
var ambient = new DefaultHttpContext { RequestServices = app.Services };
ambient.Request.RouteValues = new RouteValueDictionary(new { area = "MoveInPlanner", controller = "Items", action = "Edit" });
Check(links.GetPathByAction(ambient, "Index", "Dashboard", new { area = "" }) == "/Dashboard/Index", "Cross-section dashboard link");
Check(links.GetPathByAction(ambient, "Login", "Auth", new { area = "" }) == "/Auth/Login", "Planner login link");

var tempRoot = Path.Combine(Path.GetTempPath(), "personalhub-images-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(Path.Combine(tempRoot, "images", "items"));
try
{
    var folder = Path.Combine(tempRoot, "images", "items");
    await File.WriteAllBytesAsync(Path.Combine(folder, "source.JPG"), [1, 2, 3]);
    await File.WriteAllTextAsync(Path.Combine(folder, "ignore.txt"), "not an image");
    var service = new ImageService(new ConfigurationBuilder().Build(), new TestEnvironment(tempRoot), NullLogger<ImageService>.Instance);
    Check(service.GetExistingImages().SequenceEqual(new[] { "source.JPG" }), "Image listing filter");
    foreach (var invalid in new[] { "../source.JPG", "..\\source.JPG", "/source.JPG", "C:source.JPG", "missing.jpg", "ignore.txt" })
        Check(!service.IsExistingImage(invalid), $"Accepted invalid image: {invalid}");
    var copy = await service.CopyExistingImageAsync("source.JPG", 42);
    var copyPath = Path.Combine(tempRoot, copy.TrimStart('/'));
    Check((await File.ReadAllBytesAsync(copyPath)).SequenceEqual(new byte[] { 1, 2, 3 }), "Image copy contents");
    Check(copy != "/images/items/source.JPG", "Image copy must have independent ownership");
    await service.DeleteImageAsync(copy);
    Check(File.Exists(Path.Combine(folder, "source.JPG")), "Deleting selected copy removed original");
}
finally { Directory.Delete(tempRoot, recursive: true); }
Console.WriteLine($"PASS: {count} action URLs, 12 route destinations, cross-area links and image storage checks.");
await app.StopAsync();

static void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

sealed class TestEnvironment(string root) : IWebHostEnvironment
{
    public string WebRootPath { get; set; } = root;
    public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    public string ApplicationName { get; set; } = "Tests";
    public string EnvironmentName { get; set; } = "Development";
    public string ContentRootPath { get; set; } = root;
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
