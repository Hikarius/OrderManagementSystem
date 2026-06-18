using FluentValidation;
using FluentValidation.AspNetCore;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Serilog;
using Serilog.Formatting.Compact;
using Shared.Infrastructure.Http;
using StackExchange.Redis;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

var isRunningInTest = (builder.Configuration["DOTNET_RUNNING_IN_TEST"] == "1")
    || (Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_TEST") == "1");

// Serilog JSON console
builder.Host.UseSerilog((ctx, lc) => lc
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "OrderService")
    .WriteTo.Console(new RenderedCompactJsonFormatter()));

builder.Services.AddControllers();

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
        Description = "Paste only the JWT token here. The 'Bearer ' prefix is added automatically.",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    options.AddSecurityRequirement((document) => new OpenApiSecurityRequirement()
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
}); 

builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<Program>());
// MediatR pipeline behavior for FluentValidation
builder.Services.AddTransient(typeof(MediatR.IPipelineBehavior<,>), typeof(Shared.Application.MediatR.ValidationBehavior<,>));

// JWT Authentication (simple symmetric key)
// Keep default consistent with OrderController token issuer so Swagger/local works without extra config
var jwtKey = builder.Configuration["Jwt:Key"] ?? Environment.GetEnvironmentVariable("JWT_KEY") ?? "dev_super_secret_key_please_change_0123456789ABCDEF";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? Environment.GetEnvironmentVariable("JWT_ISSUER") ?? "local";
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


// Messaging configuration
if (!isRunningInTest)
{
    // MassTransit (RabbitMQ) for publishing events in non-test environments
    builder.Services.AddMassTransit(x =>
    {
        x.UsingRabbitMq((context, cfg) =>
        {
            cfg.Host(builder.Configuration["RabbitMq:Host"] ?? "rabbitmq", h => { });
        });
    });
    builder.Services.AddScoped<Shared.Application.Messaging.IEventPublisher, Shared.Infrastructure.Messaging.MassTransitEventPublisher>();
}
else
{
    // In tests, avoid spinning up RabbitMQ and use NoOp publisher
    builder.Services.AddScoped<Shared.Application.Messaging.IEventPublisher, Shared.Infrastructure.Messaging.NoOpEventPublisher>();
}
// MediatR registration (comment added to ensure patch applies cleanly)

// Configure EF Core
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    // Fallback to environment variables used by Docker and other environments
    connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
                       ?? Environment.GetEnvironmentVariable("DEFAULT_CONNECTION")
                       ?? string.Empty;
}

if (isRunningInTest)
{
    builder.Services.AddDbContext<OrderService.Data.DataContext>(options =>
        options.UseInMemoryDatabase("OrderServiceTestDb"));
}
else
{
    builder.Services.AddDbContext<OrderService.Data.DataContext>(options =>
        options.UseNpgsql(connectionString, b => b.MigrationsAssembly(typeof(OrderService.Data.DataContext).Assembly.FullName)));
}

// Register concrete DataContext as DbContext for shared repository DI
builder.Services.AddScoped<DbContext>(sp => sp.GetRequiredService<OrderService.Data.DataContext>());

builder.Services.AddScoped(typeof(Shared.Infrastructure.Data.IRepository<>), typeof(Shared.Infrastructure.Data.EfRepository<>));

if (!isRunningInTest)
{
    // Redis cache (production/dev)
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = builder.Configuration["Redis:Configuration"] ?? Environment.GetEnvironmentVariable("REDIS_CONFIGURATION") ?? "localhost:6379";
    });

    // Register ConnectionMultiplexer for direct Redis usage (IdempotencyStore)
    var redisConfiguration = builder.Configuration["Redis:Configuration"] ?? Environment.GetEnvironmentVariable("REDIS_CONFIGURATION") ?? "localhost:6379";
    builder.Services.AddSingleton<IConnectionMultiplexer>(sp => ConnectionMultiplexer.Connect(redisConfiguration));

    // Register idempotency store used by handlers
    builder.Services.AddScoped<Shared.Infrastructure.Redis.IIdempotencyStore, Shared.Infrastructure.Redis.IdempotencyStore>();
}
else
{
    // Use in-memory distributed cache in tests and a non-throwing multiplexer
    builder.Services.AddDistributedMemoryCache();
    // Provide IIdempotencyStore via simple in-memory implementation through DI in tests; actual implementation is overridden in test factory
    builder.Services.AddScoped<Shared.Infrastructure.Redis.IIdempotencyStore, Shared.Infrastructure.Redis.IdempotencyStore>();
}

builder.Services.AddScoped<OrderService.Data.Repositories.OrderRepository>();
builder.Services.AddScoped<OrderService.Application.Queries.OrderQueries>();

// Register HTTP client for CatalogService (used by handlers to call catalog)
var catalogBase = builder.Configuration["CatalogService:BaseAddress"]
                  ?? Environment.GetEnvironmentVariable("CATALOGSERVICE_BASEADDRESS")
                  ?? "http://catalogservice";
builder.Services.AddCatalogServiceClient(catalogBase);

// Health checks
builder.Services.AddHealthChecks()
    .AddCheck<Shared.Infrastructure.Health.DbContextHealthCheck<OrderService.Data.DataContext>>("db")
    .AddCheck<Shared.Infrastructure.Health.RabbitMqHealthCheck>("rabbitmq");

var app = builder.Build();

// Configure the HTTP request pipeline.
//if (app.Environment.IsDevelopment())
//{
    app.UseSwagger(); // Enables Swagger UI
    app.UseSwaggerUI(); // Configures Swagger UI
//}

// Apply pending EF Core migrations automatically on startup
using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<OrderService.Data.DataContext>();
        if (db.Database.IsRelational()) db.Database.Migrate();
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while migrating the database for OrderService.");
        throw;
    }
}

app.UseHttpsRedirection();

app.UseSerilogRequestLogging();
// Correlation ID middleware
app.UseMiddleware<Shared.Middleware.CorrelationIdMiddleware>();

// Global exception handling middleware (Problem Details) from Shared
app.UseMiddleware<Shared.Middleware.ExceptionHandlingMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

public partial class Program { }
