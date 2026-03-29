using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace Xcord.Infrastructure.Services.Bots;

public sealed class BotAgentManifest
{
    public string Id { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string Category { get; set; } = null!;
    public BotAgentParameter[] Parameters { get; set; } = [];
}

public sealed class BotAgentParameter
{
    public string Key { get; set; } = null!;
    public string Label { get; set; } = null!;
    public string Type { get; set; } = null!; // "string", "number", "boolean"
    public JsonElement? Default { get; set; }
}

public sealed class BotAgentRegistry
{
    private readonly Dictionary<string, BotAgentManifest> _agents = new();
    private readonly string _botsDirectory;
    private readonly ILogger<BotAgentRegistry> _logger;

    public BotAgentRegistry(ILogger<BotAgentRegistry> logger, IWebHostEnvironment env)
    {
        _logger = logger;
        // Look for bots/ directory relative to content root
        _botsDirectory = Path.Combine(env.ContentRootPath, "bots");
        LoadManifests();
    }

    private void LoadManifests()
    {
        if (!Directory.Exists(_botsDirectory))
        {
            _logger.LogWarning("Bots directory not found at {Path}", _botsDirectory);
            return;
        }

        foreach (var dir in Directory.GetDirectories(_botsDirectory))
        {
            var manifestPath = Path.Combine(dir, "manifest.json");
            if (!File.Exists(manifestPath)) continue;

            try
            {
                var json = File.ReadAllText(manifestPath);
                var manifest = JsonSerializer.Deserialize<BotAgentManifest>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (manifest != null)
                {
                    _agents[manifest.Id] = manifest;
                    _logger.LogInformation("Loaded bot agent: {Name} ({Id})", manifest.Name, manifest.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load bot manifest from {Path}", manifestPath);
            }
        }
    }

    public IReadOnlyCollection<BotAgentManifest> GetAll() => _agents.Values.ToList();
    public BotAgentManifest? GetById(string id) => _agents.GetValueOrDefault(id);

    public string? GetScriptPath(string id)
    {
        // Find the directory containing this agent's manifest
        foreach (var dir in Directory.GetDirectories(_botsDirectory))
        {
            var manifestPath = Path.Combine(dir, "manifest.json");
            if (!File.Exists(manifestPath)) continue;
            var json = File.ReadAllText(manifestPath);
            var manifest = JsonSerializer.Deserialize<BotAgentManifest>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (manifest?.Id == id) return Path.Combine(dir, "index.js");
        }
        return null;
    }
}
