using MassTransit;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// MassTransit with RabbitMQ
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<NotificationService.Consumers.OrderCreatedConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMq:Host"] ?? "localhost", h => { });

        cfg.ReceiveEndpoint("order-created-queue", e =>
        {
            // retry policy for transient exceptions and exponential backoff
            e.UseMessageRetry(r => r.Exponential(5, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(2)));
            // default fault handling will move messages to error queue; ConfigureDeadLetter can be added if desired
            e.ConfigureConsumer<NotificationService.Consumers.OrderCreatedConsumer>(context);
        });
    });
});

// Register event publisher wiring so consumers/publishers can be resolved
builder.Services.AddScoped<Shared.Application.Messaging.IEventPublisher, Shared.Infrastructure.Messaging.MassTransitEventPublisher>();

// Health checks
builder.Services.AddHealthChecks()
    .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy());

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
