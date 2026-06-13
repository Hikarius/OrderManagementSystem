using MassTransit;
using Microsoft.EntityFrameworkCore;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

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

// FluentValidation registration
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();
// MediatR pipeline behavior for FluentValidation
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<Program>());
builder.Services.AddTransient(typeof(MediatR.IPipelineBehavior<,>), typeof(Shared.Application.MediatR.ValidationBehavior<,>));

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

// Global exception handling middleware (Problem Details) from Shared
app.UseMiddleware<Shared.Middleware.ExceptionHandlingMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
