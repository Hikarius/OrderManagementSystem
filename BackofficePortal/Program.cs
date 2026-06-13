
var builder = WebApplication.CreateBuilder(args);
// Ensure data protection keys are persisted so antiforgery tokens survive app restarts

// Add services to the container.
builder.Services.AddControllersWithViews();

// Session (used to store JWT for this portal) and HttpContext access
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession();
builder.Services.AddHttpContextAccessor();



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
app.Use(async (context, next) =>
{
    context.Response.Cookies.Delete(".AspNetCore.Antiforgery");
    await next();
});
app.UseHttpsRedirection();
app.UseRouting();

// Enable session middleware (services.AddSession was registered earlier)
app.UseSession();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
