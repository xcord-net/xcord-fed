using Microsoft.EntityFrameworkCore;
using Serilog;
using Xcord.Api;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "Xcord.Instance")
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

// Register all services
builder.AddXcordServices();

var app = builder.Build();

// Bootstrap: migrate database, ensure RSA key pair, load key for JWT validation
// Skip entirely in Testing environment — app.Services access triggers a premature host build
// in WebApplicationFactory's deferred host model, before test configuration is applied.
// The test fixture handles DB schema, RSA keys, and JWT configuration directly.
if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    await db.Database.MigrateAsync();
    Log.Information("Database migrations applied");

    // Ensure RSA key pair exists
    var jwtService = scope.ServiceProvider.GetRequiredService<IJwtService>();
    await jwtService.EnsureRsaKeyPairAsync();
    Log.Information("RSA key pair verified");

    // Load RSA public key into singleton for JWT validation
    var rsaKeySingleton = scope.ServiceProvider.GetRequiredService<RsaKeySingleton>();
    var publicKeySetting = db.SystemSettings.FirstOrDefault(s => s.Key == "RsaPublicKey");
    if (publicKeySetting != null)
    {
        rsaKeySingleton.LoadPublicKey(publicKeySetting.Value);
        Log.Information("RSA public key loaded for JWT validation");
    }

    // Configure JWT validation with the loaded RSA key
    var rsaKey = app.Services.GetRequiredService<RsaKeySingleton>();
    var jwtBearerOptions = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerOptions>>();
    var currentOptions = jwtBearerOptions.Get(Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme);
    if (currentOptions != null)
    {
        currentOptions.TokenValidationParameters.IssuerSigningKey = rsaKey.GetPublicKey();
    }

    // Bootstrap encryption key: config → DB → generate on first boot
    var encKeyHolder = app.Services.GetRequiredService<EncryptionKeyHolder>();
    var configKey = builder.Configuration.GetSection("Encryption:EncryptionKey").Value;
    var dbKey = db.SystemSettings.FirstOrDefault(s => s.Key == "EncryptionKey");

    if (!string.IsNullOrEmpty(configKey))
    {
        // Config-provided key (standalone mode) — store in DB if not already there
        if (dbKey == null)
        {
            db.SystemSettings.Add(new Xcord.Entities.SystemSetting
            {
                Key = "EncryptionKey",
                Value = configKey,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }
        encKeyHolder.SetKey(configKey);
        Log.Information("Encryption key loaded from configuration");
    }
    else if (dbKey != null)
    {
        // Key already in DB (normal restart)
        encKeyHolder.SetKey(dbKey.Value);
        Log.Information("Encryption key loaded from database");
    }
    else
    {
        // First boot — generate new key
        var newKey = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        db.SystemSettings.Add(new Xcord.Entities.SystemSetting
        {
            Key = "EncryptionKey",
            Value = newKey,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
        encKeyHolder.SetKey(newKey);
        Log.Information("Generated new encryption key on first boot");
    }
}

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Forwarded headers (must be first to correctly resolve client IPs behind reverse proxy)
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor
        | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto
});

// Middleware Stack (exact order per architecture)
app.UseExceptionHandler();
app.UseSerilogRequestLogging();
app.UseSecurityHeaders();
app.UseRateLimiter();

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseCors();

// Access Token Cookie → Authorization Header
app.Use(async (context, next) =>
{
    if (!context.Request.Headers.ContainsKey("Authorization") &&
        context.Request.Cookies.TryGetValue("access_token", out var token))
    {
        context.Request.Headers.Authorization = $"Bearer {token}";
    }
    await next();
});

app.UseAuthentication();
app.UseAuthorization();
app.UseStaticFiles();

// Map endpoints
app.MapHealthEndpoint();
app.MapHandlerEndpoints(typeof(Xcord.Features.FeaturesAssemblyMarker).Assembly);
app.MapHub<MainHub>("/hubs/main");

// Admin SPA Fallback
app.MapWhen(
    ctx => ctx.Request.Path.StartsWithSegments("/admin"),
    adminApp =>
    {
        adminApp.Run(async context =>
        {
            context.Response.ContentType = "text/html";
            await context.Response.SendFileAsync("wwwroot/admin/index.html");
        });
    });

// Chat Client SPA Fallback
app.MapFallbackToFile("index.html");

try
{
    Log.Information("Starting Xcord.Api");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

// Make Program accessible for WebApplicationFactory in integration tests
public partial class Program { }
