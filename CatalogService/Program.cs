using Microsoft.EntityFrameworkCore;
using CatalogService.Data;
using Shared.Infrastructure.Data;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<Program>());
// MediatR pipeline behavior for FluentValidation
builder.Services.AddTransient(typeof(MediatR.IPipelineBehavior<,>), typeof(Shared.Application.MediatR.ValidationBehavior<,>));

// JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"] ?? Environment.GetEnvironmentVariable("JWT_KEY") ?? "change_this_in_production";
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
    // Fallback to environment variable if connection string is not present in config
    connectionString = Environment.GetEnvironmentVariable("DEFAULT_CONNECTION") ?? string.Empty;
}

builder.Services.AddDbContext<DataContext>(options =>
    options.UseNpgsql(connectionString, b => b.MigrationsAssembly(typeof(DataContext).Assembly.FullName)));

// Register generic repository wiring so other services can depend on IRepository<T>
builder.Services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));

// Register Redis distributed cache (configuration from Docker Compose: Redis__Configuration)
builder.Services.AddStackExchangeRedisCache(options =>
{
    // Use the Redis configuration key used in docker-compose (Redis__Configuration)
    options.Configuration = builder.Configuration["Redis:Configuration"] ?? Environment.GetEnvironmentVariable("REDIS_CONFIGURATION") ?? "localhost:6379";
});

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddScoped<CatalogService.Data.Repositories.ProductRepository>();
builder.Services.AddScoped<CatalogService.Application.Queries.ProductQueries>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
