
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Builder;
using StackExchange.Redis;
using Microsoft.AspNetCore.DataProtection.StackExchangeRedis;

var builder = WebApplication.CreateBuilder(args);
// Ensure data protection keys are persisted so antiforgery tokens survive app restarts

// Add services to the container.
builder.Services.AddControllersWithViews();

// Session (used to store JWT for this portal) and HttpContext access
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(1);
    options.Cookie.IsEssential = true; // ensure session cookie is always written
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.None; // allow over HTTP in dev/containers
});
builder.Services.AddHttpContextAccessor();

// Add antiforgery services
builder.Services.AddAntiforgery(options =>
{
    // In containers/dev we often run HTTP only; ensure cookie is sent over HTTP
    options.Cookie.SecurePolicy = CookieSecurePolicy.None;
    // options.HeaderName = "X-CSRF-TOKEN"; // Uncomment and configure if you use a custom header
});

// Configure Data Protection: prefer Redis for key persistence, fallback to file system
var dataProtectionBuilder = builder.Services
    .AddDataProtection()
    .SetApplicationName("backoffice-portal");

var redisConfiguration = builder.Configuration["Redis:Configuration"]
                      ?? Environment.GetEnvironmentVariable("REDIS_CONFIGURATION");

if (!string.IsNullOrWhiteSpace(redisConfiguration))
{
    // Persist keys to Redis so keys survive container restarts and are shared across replicas
    var mux = ConnectionMultiplexer.Connect(redisConfiguration);
    dataProtectionBuilder.PersistKeysToStackExchangeRedis(mux, "DataProtection-Keys");
}
else
{
    // Fallback to a shared file system path if provided
    var keysDirectoryPath = Environment.GetEnvironmentVariable("DATA_PROTECTION_KEYS_PATH");
    if (string.IsNullOrEmpty(keysDirectoryPath))
    {
        keysDirectoryPath = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "DataProtectionKeys");
        Console.WriteLine($"Warning: DATA_PROTECTION_KEYS_PATH environment variable not set. Using fallback path: {keysDirectoryPath}");
    }
    Directory.CreateDirectory(keysDirectoryPath);
    dataProtectionBuilder.PersistKeysToFileSystem(new DirectoryInfo(keysDirectoryPath));
}


// Persist data protection keys to Redis so multiple instances can share the key ring
// Redis connection string can be provided via configuration key: "DataProtection:Redis" or "ServiceUrls:Redis"
//var redisConnection = builder.Configuration["DataProtection:Redis"] ?? builder.Configuration["ServiceUrls:Redis"] ?? "localhost:6379";
// Register ConnectionMultiplexer for DI
builder.Services.AddHttpClient("OrderService", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ServiceUrls:Order"] ?? "https://localhost:5001");
});
builder.Services.AddHttpClient("CatalogService", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ServiceUrls:Catalog"] ?? "https://localhost:5002");
});
builder.Services.AddHttpClient("NotificationService", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ServiceUrls:Notification"] ?? "https://localhost:5003");
});

// Local Api client wrapper
builder.Services.AddScoped<BackofficePortal.Services.ApiClient>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
//app.Use(async (context, next) =>
//{
//    context.Response.Cookies.Delete(".AspNetCore.Antiforgery");
//    await next();
//});

// In containers there may be no HTTPS endpoint configured; avoid redirect errors
var urls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? string.Empty;
if (urls.Contains("https://", StringComparison.OrdinalIgnoreCase))
{
    app.UseHttpsRedirection();
}
app.UseRouting();
app.UseAntiforgery(); // This should typically come after UseRouting and before UseAuthorization

// Enable session middleware (services.AddSession was registered earlier)
app.UseSession();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
