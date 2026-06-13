using Microsoft.EntityFrameworkCore;
// Ensure EF Core extension methods are available (kept for clarity)
using MassTransit;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
// Services are configured below.

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<Program>());
// MediatR pipeline behavior for FluentValidation
builder.Services.AddTransient(typeof(MediatR.IPipelineBehavior<,>), typeof(Shared.Application.MediatR.ValidationBehavior<,>));

// JWT Authentication (simple symmetric key)
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

// FluentValidation registration
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();


// MassTransit (RabbitMQ) for publishing events
builder.Services.AddMassTransit(x =>
{
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMq:Host"] ?? "localhost", h => { });
    });
});

// register event publisher
builder.Services.AddScoped<Shared.Application.Messaging.IEventPublisher, Shared.Infrastructure.Messaging.MassTransitEventPublisher>();
// MediatR registration (comment added to ensure patch applies cleanly)

// Configure EF Core with Npgsql (PostgreSQL)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    connectionString = Environment.GetEnvironmentVariable("DEFAULT_CONNECTION") ?? string.Empty;
}

builder.Services.AddDbContext<OrderService.Data.DataContext>(options =>
    options.UseNpgsql(connectionString, b => b.MigrationsAssembly(typeof(OrderService.Data.DataContext).Assembly.FullName)));

builder.Services.AddScoped(typeof(Shared.Infrastructure.Data.IRepository<>), typeof(Shared.Infrastructure.Data.EfRepository<>));

// Redis cache
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration["Redis:Configuration"] ?? Environment.GetEnvironmentVariable("REDIS_CONFIGURATION") ?? "localhost:6379";
});

builder.Services.AddScoped<OrderService.Data.Repositories.OrderRepository>();
builder.Services.AddScoped<OrderService.Application.Queries.OrderQueries>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Apply pending EF Core migrations automatically on startup
using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<OrderService.Data.DataContext>();
        db.Database.Migrate();
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while migrating the database for OrderService.");
        throw;
    }
}

app.UseHttpsRedirection();

// Global exception handling middleware (Problem Details) from Shared
app.UseMiddleware<Shared.Middleware.ExceptionHandlingMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
