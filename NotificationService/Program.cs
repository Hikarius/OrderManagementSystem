using MassTransit;
using NotificationService.Consumers;
using Microsoft.EntityFrameworkCore;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Serilog;
using Serilog.Formatting.Compact;

var builder = WebApplication.CreateBuilder(args);

// Serilog JSON console
builder.Host.UseSerilog((ctx, lc) => lc
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "NotificationService")
    .WriteTo.Console(new RenderedCompactJsonFormatter()));

// Add services to the container.
builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer(); // Required for OpenAPI/Swagger
builder.Services.AddSwaggerGen(); // Adds Swagger generation services

// JWT auth simple
var jwtKey = builder.Configuration["Jwt:Key"] ?? Environment.GetEnvironmentVariable("JWT_KEY") ?? "change_this_in_production";
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

// MassTransit 8.x doesn't require license

// FluentValidation registration
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();
// MediatR pipeline behavior for FluentValidation
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<Program>());
builder.Services.AddTransient(typeof(MediatR.IPipelineBehavior<,>), typeof(Shared.Application.MediatR.ValidationBehavior<,>));

// MassTransit with RabbitMQ — register bus & consumer
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<OrderCreatedConsumer>();
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMq:Host"] ?? "rabbitmq");
        cfg.ConfigureEndpoints(context);
    });
});
// Configure EF Core DbContext for NotificationService and expose as DbContext
var notificationConnectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? builder.Configuration["ConnectionStrings:DefaultConnection"]
    ?? Environment.GetEnvironmentVariable("CONNECTION_STRING")
    ?? "Host=localhost;Database=notifications;Username=postgres;Password=postgres";

builder.Services.AddDbContext<NotificationService.Data.DataContext>(options =>
    options.UseNpgsql(notificationConnectionString));

builder.Services.AddScoped<DbContext, NotificationService.Data.DataContext>();

// Health checks
builder.Services.AddHealthChecks()
    .AddCheck<Shared.Infrastructure.Health.DbContextHealthCheck<NotificationService.Data.DataContext>>("db")
    .AddCheck<Shared.Infrastructure.Health.RabbitMqHealthCheck>("rabbitmq");

// Register Redis multiplexer and idempotency store
var redisCfg = builder.Configuration["Redis:Configuration"] ?? Environment.GetEnvironmentVariable("REDIS_CONFIGURATION") ?? "localhost:6379";
builder.Services.AddSingleton<StackExchange.Redis.IConnectionMultiplexer>(sp => StackExchange.Redis.ConnectionMultiplexer.Connect(redisCfg));
builder.Services.AddScoped<Shared.Infrastructure.Redis.IdempotencyStore>();

var app = builder.Build();
// Note: IEventPublisher registration intentionally omitted in NotificationService.
// The concrete registration follows the pattern implemented in OrderService/Program.cs

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger(); // Enables Swagger UI
    app.UseSwaggerUI(); // Configures Swagger UI
}

// Apply pending EF Core migrations automatically on startup
using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<NotificationService.Data.DataContext>();
        db.Database.Migrate();
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while migrating the database for NotificationService.");
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
