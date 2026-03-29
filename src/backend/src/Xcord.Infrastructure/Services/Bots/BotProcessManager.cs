using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Xcord.Infrastructure.Services.Bots;

public sealed class BotProcessInfo
{
    public long BotTokenId { get; set; }
    public string AgentId { get; set; } = null!;
    public Process Process { get; set; } = null!;
    public DateTimeOffset StartedAt { get; set; }
    public bool IsRunning => !Process.HasExited;
}

public sealed class BotProcessManager : IDisposable
{
    private readonly ConcurrentDictionary<long, BotProcessInfo> _processes = new();
    private readonly BotAgentRegistry _registry;
    private readonly ILogger<BotProcessManager> _logger;

    public BotProcessManager(BotAgentRegistry registry, ILogger<BotProcessManager> logger)
    {
        _registry = registry;
        _logger = logger;
    }

    public async Task<bool> StartBotAsync(long botTokenId, string agentId, string botToken, string baseUrl, JsonElement? parameters)
    {
        if (_processes.ContainsKey(botTokenId)) return false; // already running

        var scriptPath = _registry.GetScriptPath(agentId);
        if (scriptPath == null || !File.Exists(scriptPath))
        {
            _logger.LogError("Bot agent script not found: {AgentId}", agentId);
            return false;
        }

        var config = new
        {
            token = botToken,
            baseUrl = baseUrl,
            agentId = agentId,
            parameters = parameters
        };

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "node",
                Arguments = scriptPath,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(scriptPath)
            },
            EnableRaisingEvents = true
        };

        process.Exited += (_, _) =>
        {
            _processes.TryRemove(botTokenId, out _);
            _logger.LogInformation("Bot process {BotTokenId} ({AgentId}) exited with code {ExitCode}",
                botTokenId, agentId, process.ExitCode);
        };

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null) _logger.LogInformation("[Bot:{AgentId}] {Output}", agentId, e.Data);
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null) _logger.LogWarning("[Bot:{AgentId}] {Error}", agentId, e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        // Send config via stdin (avoids token in process args)
        await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(config));
        process.StandardInput.Close();

        var info = new BotProcessInfo
        {
            BotTokenId = botTokenId,
            AgentId = agentId,
            Process = process,
            StartedAt = DateTimeOffset.UtcNow
        };

        _processes[botTokenId] = info;
        _logger.LogInformation("Started bot process {BotTokenId} ({AgentId})", botTokenId, agentId);
        return true;
    }

    public bool StopBot(long botTokenId)
    {
        if (!_processes.TryRemove(botTokenId, out var info)) return false;
        try
        {
            if (!info.Process.HasExited)
            {
                info.Process.Kill(entireProcessTree: true);
                info.Process.WaitForExit(5000);
            }
            info.Process.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping bot process {BotTokenId}", botTokenId);
        }
        return true;
    }

    public bool IsRunning(long botTokenId) => _processes.TryGetValue(botTokenId, out var info) && info.IsRunning;

    public IReadOnlyCollection<BotProcessInfo> GetRunningBots() => _processes.Values.ToList();

    public void Dispose()
    {
        foreach (var kvp in _processes)
        {
            try
            {
                if (!kvp.Value.Process.HasExited)
                {
                    kvp.Value.Process.Kill(entireProcessTree: true);
                    kvp.Value.Process.WaitForExit(3000);
                }
                kvp.Value.Process.Dispose();
            }
            catch { /* shutdown cleanup */ }
        }
        _processes.Clear();
    }
}
