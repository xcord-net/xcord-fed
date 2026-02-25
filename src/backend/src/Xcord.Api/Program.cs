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

    // Bootstrap encryption key with envelope encryption support
    var encKeyHolder = app.Services.GetRequiredService<EncryptionKeyHolder>();
    var kekProvider = app.Services.GetRequiredService<IKekProvider>();
    var kek = kekProvider.GetKek();

    var configKey = builder.Configuration.GetSection("Encryption:EncryptionKey").Value;
    var wrappedDbKey = db.SystemSettings.FirstOrDefault(s => s.Key == "WrappedEncryptionKey");
    var plaintextDbKey = db.SystemSettings.FirstOrDefault(s => s.Key == "EncryptionKey");

    if (kek != null)
    {
        // KEK available — use envelope encryption
        if (wrappedDbKey != null)
        {
            // Normal restart: unwrap DEK from DB
            var wrappedBytes = Convert.FromBase64String(wrappedDbKey.Value);
            var dekBytes = KeyWrappingService.UnwrapDek(wrappedBytes, kek);
            encKeyHolder.SetKey(Convert.ToBase64String(dekBytes));
            Log.Information("Encryption key unwrapped from database using KEK");
        }
        else if (plaintextDbKey != null)
        {
            // Migration: wrap existing plaintext DEK with KEK
            var dekBytes = Convert.FromBase64String(plaintextDbKey.Value);
            var wrappedBytes = KeyWrappingService.WrapDek(dekBytes, kek);
            db.SystemSettings.Add(new Xcord.Entities.SystemSetting
            {
                Key = "WrappedEncryptionKey",
                Value = Convert.ToBase64String(wrappedBytes),
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
            db.SystemSettings.Remove(plaintextDbKey);
            await db.SaveChangesAsync();
            encKeyHolder.SetKey(plaintextDbKey.Value);
            Log.Information("Migrated plaintext encryption key to envelope encryption");
        }
        else if (!string.IsNullOrEmpty(configKey))
        {
            // Migration: wrap config-provided DEK with KEK
            var dekBytes = Convert.FromBase64String(configKey);
            var wrappedBytes = KeyWrappingService.WrapDek(dekBytes, kek);
            db.SystemSettings.Add(new Xcord.Entities.SystemSetting
            {
                Key = "WrappedEncryptionKey",
                Value = Convert.ToBase64String(wrappedBytes),
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
            encKeyHolder.SetKey(configKey);
            Log.Information("Wrapped config encryption key with KEK and stored in database");
        }
        else
        {
            // First boot with KEK: generate DEK, wrap it, store wrapped
            var dekBytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
            var wrappedBytes = KeyWrappingService.WrapDek(dekBytes, kek);
            db.SystemSettings.Add(new Xcord.Entities.SystemSetting
            {
                Key = "WrappedEncryptionKey",
                Value = Convert.ToBase64String(wrappedBytes),
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
            encKeyHolder.SetKey(Convert.ToBase64String(dekBytes));
            Log.Information("Generated new encryption key (envelope-encrypted) on first boot");
        }
    }
    else
    {
        // No KEK — legacy mode
        if (wrappedDbKey != null)
        {
            // Fatal: wrapped key exists but no KEK to unwrap it
            throw new InvalidOperationException(
                "Database contains a wrapped encryption key (WrappedEncryptionKey) but no KEK is configured. " +
                "Provide the KEK via /run/secrets/xcord-kek or Encryption:Kek config.");
        }

        if (!string.IsNullOrEmpty(configKey))
        {
            if (plaintextDbKey == null)
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
            Log.Warning("Encryption key loaded from configuration WITHOUT envelope encryption — configure a KEK for production use");
        }
        else if (plaintextDbKey != null)
        {
            encKeyHolder.SetKey(plaintextDbKey.Value);
            Log.Warning("Encryption key loaded from database WITHOUT envelope encryption — configure a KEK for production use");
        }
        else
        {
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
            Log.Warning("Generated new encryption key WITHOUT envelope encryption — configure a KEK for production use");
        }
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
Xcord.Features.Billing.MemberBillingWebhookHandler.Map(app);
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
