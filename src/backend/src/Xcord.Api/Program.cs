using Microsoft.AspNetCore.HttpOverrides;
using Serilog;
using Xcord.Api;

// Pre-warm the thread pool to handle concurrent CPU-bound work (e.g. BCrypt)
// without starvation. Default min threads is too low for burst auth traffic.
ThreadPool.SetMinThreads(workerThreads: 32, completionPortThreads: 32);

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "Xcord.Instance")
    .CreateLogger();

builder.Host.UseSerilog();

// Register all services
builder.AddXcordServices();

var app = builder.Build();

// Bootstrap: migrate database, ensure RSA key pair, load key for JWT validation
// Skip entirely in Testing environment - app.Services access triggers a premature host build
// in WebApplicationFactory's deferred host model, before test configuration is applied.
// The test fixture handles DB schema, RSA keys, and JWT configuration directly.
if (!app.Environment.IsEnvironment("Testing"))
{
    await BootstrapService.InitializeAsync(app);
}

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Forwarded headers (must be first to correctly resolve client IPs behind reverse proxy)
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
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

// Dev-only test seed endpoint for E2E tests
if (app.Environment.IsDevelopment())
{
    TestSeedEndpoint.Map(app);
}

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
