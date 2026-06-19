using CatalogService.Data;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Serilog;
using Serilog.Formatting.Json;
using Shared.Infrastructure.Data;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Serilog JSON console logger
builder.Host.UseSerilog((ctx, lc) => lc
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "CatalogService")
    .WriteTo.Console(new JsonFormatter()));

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<Program>());
// MediatR pipeline behavior for FluentValidation
builder.Services.AddTransient(typeof(MediatR.IPipelineBehavior<,>), typeof(Shared.Application.MediatR.ValidationBehavior<,>));

builder.Services.AddEndpointsApiExplorer(); // Required for OpenAPI/Swagger
builder.Services.AddSwaggerGen(options =>
{
    var xmlFile = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name + ".xml";
    var xmlPath = System.IO.Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (System.IO.File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
    }

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Paste only the JWT token. The 'Bearer ' prefix is added by Swagger.",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    options.AddSecurityRequirement((document) => new OpenApiSecurityRequirement()
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
}); // Adds Swagger generation services with XML comments + JWT

// JWT Authentication
// Use the same default dev key across services so a token from OrderService works here in dev
var jwtKey = builder.Configuration["Jwt:Key"] ?? Environment.GetEnvironmentVariable("JWT_KEY") ?? "dev_super_secret_key_please_change_0123456789ABCDEF";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? Environment.GetEnvironmentVariable("JWT_ISSUER") ?? "local"
;
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = false,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

// FluentValidation registration
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// Configure EF Core with Npgsql (PostgreSQL)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    // Fallback to environment variables used by Docker and other environments
    // Docker Compose sets ConnectionStrings__DefaultConnection which maps to Configuration.GetConnectionString("DefaultConnection").
    // Some environments may set DEFAULT_CONNECTION — keep it for backward compatibility.
    connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
                       ?? Environment.GetEnvironmentVariable("DEFAULT_CONNECTION")
                       ?? string.Empty;
}

builder.Services.AddDbContext<DataContext>(options =>
    options.UseNpgsql(connectionString, b => b.MigrationsAssembly(typeof(DataContext).Assembly.FullName)));

// Expose the concrete DataContext also as DbContext so shared EfRepository which depends on DbContext can be resolved
builder.Services.AddScoped<DbContext>(sp => sp.GetRequiredService<DataContext>());

// Register generic repository wiring so other services can depend on IRepository<T>
builder.Services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));

// Register Redis distributed cache (configuration from Docker Compose: Redis__Configuration)
builder.Services.AddStackExchangeRedisCache(options =>
{
    // Use the Redis configuration key used in docker-compose (Redis__Configuration)
    options.Configuration = builder.Configuration["Redis:Configuration"] ?? Environment.GetEnvironmentVariable("REDIS_CONFIGURATION") ?? "localhost:6379";
});

// Register ConnectionMultiplexer and idempotency store for shared Redis usage
var redisConfig = builder.Configuration["Redis:Configuration"] ?? Environment.GetEnvironmentVariable("REDIS_CONFIGURATION") ?? "localhost:6379";
builder.Services.AddSingleton<StackExchange.Redis.IConnectionMultiplexer>(sp => StackExchange.Redis.ConnectionMultiplexer.Connect(redisConfig));
builder.Services.AddScoped<Shared.Infrastructure.Redis.IdempotencyStore>();


builder.Services.AddScoped<CatalogService.Data.Repositories.ProductRepository>();
builder.Services.AddScoped<CatalogService.Application.Queries.ProductQueries>();

// Health checks
builder.Services.AddHealthChecks()
    .AddCheck<Shared.Infrastructure.Health.DbContextHealthCheck<CatalogService.Data.DataContext>>("db")
    .AddCheck<Shared.Infrastructure.Health.RabbitMqHealthCheck>("rabbitmq");

var app = builder.Build();

// Configure the HTTP request pipeline.
//if (app.Environment.IsDevelopment())
//{
    app.UseSwagger(); // Enables Swagger UI
    app.UseSwaggerUI(); // Configures Swagger UI
//}

// Conditionally apply pending EF Core migrations on startup when MIGRATE_ON_STARTUP=true
var migrateOnStartup = true;
   // (Environment.GetEnvironmentVariable("MIGRATE_ON_STARTUP") ?? app.Configuration["MigrateOnStartup"])?.ToLower() == "true";
if (migrateOnStartup)
{
    using var scope = app.Services.CreateScope();
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<CatalogService.Data.DataContext>();
        db.Database.Migrate();
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while migrating the database for CatalogService.");
        throw;
    }
}

app.UseHttpsRedirection();
app.UseSerilogRequestLogging();
// Correlation ID middleware (must be early)
app.UseMiddleware<Shared.Middleware.CorrelationIdMiddleware>();
// Global exception handling middleware (Problem Details) from Shared
app.UseMiddleware<Shared.Middleware.ExceptionHandlingMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

// Redirect root URL to Swagger UI for convenience (hidden from Swagger)
app.MapGet("/", () => Results.Redirect("/swagger/index.html")).ExcludeFromDescription();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
