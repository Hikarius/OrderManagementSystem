using Microsoft.EntityFrameworkCore;
using MassTransit;

var builder = WebApplication.CreateBuilder(args);
// Services are configured below.

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<Program>());


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

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
