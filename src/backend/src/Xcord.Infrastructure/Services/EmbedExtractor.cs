using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Xcord.Entities;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Services;
using Xcord;

namespace Xcord.Infrastructure.Services;

/// <summary>
/// Background service that extracts embeds from URLs in messages.
/// </summary>
public sealed class EmbedExtractor : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<EmbedExtractor> _logger;
    private const int PollingIntervalSeconds = 2;
    private const int MaxUrlsPerMessage = 5;

    public EmbedExtractor(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<EmbedExtractor> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("EmbedExtractor starting with polling interval {PollingIntervalSeconds} seconds", PollingIntervalSeconds);

        // Wait a bit before starting to allow the application to fully initialize
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingEmbedsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing embeds");
            }

            await Task.Delay(TimeSpan.FromSeconds(PollingIntervalSeconds), stoppingToken);
        }

        _logger.LogInformation("EmbedExtractor stopping");
    }

    /// <summary>
    /// Process messages that need embed extraction.
    /// </summary>
    private async Task ProcessPendingEmbedsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var snowflakeGenerator = scope.ServiceProvider.GetRequiredService<SnowflakeIdGenerator>();
        var storageService = scope.ServiceProvider.GetRequiredService<IStorageService>();
        var outboxWriter = scope.ServiceProvider.GetRequiredService<IOutboxWriter>();
        var httpClient = scope.ServiceProvider.GetRequiredService<SsrfSafeHttpClient>();
        var ogParser = scope.ServiceProvider.GetRequiredService<OpenGraphParser>();

        // Find messages with URLs that haven't been processed yet
        var pendingMessages = await context.Messages
            .Where(m => !m.EmbedsProcessed && m.Metadata != null && m.Metadata.Contains("\"urls\""))
            .OrderBy(m => m.CreatedAt)
            .Take(10) // Process up to 10 messages per iteration
            .ToListAsync(cancellationToken);

        if (pendingMessages.Count == 0)
        {
            return;
        }

        _logger.LogDebug("Processing {Count} messages for embed extraction", pendingMessages.Count);

        foreach (var message in pendingMessages)
        {
            try
            {
                await ProcessMessageEmbedsAsync(
                    message,
                    snowflakeGenerator,
                    storageService,
                    outboxWriter,
                    httpClient,
                    ogParser,
                    context,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process embeds for message {MessageId}", message.Id);

                // Mark as processed even on failure to avoid infinite retries
                message.EmbedsProcessed = true;
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Process embeds for a single message.
    /// </summary>
    private async Task ProcessMessageEmbedsAsync(
        Message message,
        SnowflakeIdGenerator snowflakeGenerator,
        IStorageService storageService,
        IOutboxWriter outboxWriter,
        SsrfSafeHttpClient httpClient,
        OpenGraphParser ogParser,
        AppDbContext context,
        CancellationToken cancellationToken)
    {
        // Parse URLs from metadata
        var metadata = JsonSerializer.Deserialize<MessageMetadata>(message.Metadata!);
        if (metadata?.Urls == null || metadata.Urls.Count == 0)
        {
            message.EmbedsProcessed = true;
            return;
        }

        var urls = metadata.Urls.Take(MaxUrlsPerMessage).ToList();
        _logger.LogDebug("Extracting embeds for {Count} URLs in message {MessageId}", urls.Count, message.Id);

        // Parallelize embed extraction across all URLs using Task.WhenAll to avoid
        // sequential HTTP round-trips.
        var extractionTasks = urls
            .Select((url, index) => ExtractEmbedSafeAsync(
                url,
                message.Id,
                index,
                snowflakeGenerator,
                storageService,
                httpClient,
                ogParser,
                cancellationToken))
            .ToList();

        var embedResults = await Task.WhenAll(extractionTasks);

        var embedPosition = 0;
        foreach (var embed in embedResults.Where(e => e != null))
        {
            embed!.Position = embedPosition++;
            context.Embeds.Add(embed);
        }

        // Mark message as processed
        message.EmbedsProcessed = true;

        // Write outbox event if any embeds were created
        if (embedPosition > 0)
        {
            await outboxWriter.WriteAsync(
                context,
                "Message.Embedded",
                new { messageId = message.Id, conversationId = message.ConversationId },
                cancellationToken);
        }
    }

    /// <summary>
    /// Wraps ExtractEmbedAsync with error handling so failed tasks don't cancel Task.WhenAll.
    /// </summary>
    private async Task<Embed?> ExtractEmbedSafeAsync(
        string url,
        long messageId,
        int position,
        SnowflakeIdGenerator snowflakeGenerator,
        IStorageService storageService,
        SsrfSafeHttpClient httpClient,
        OpenGraphParser ogParser,
        CancellationToken cancellationToken)
    {
        try
        {
            return await ExtractEmbedAsync(url, messageId, position, snowflakeGenerator, storageService, httpClient, ogParser, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract embed for URL {Url} in message {MessageId}", url, messageId);
            return null;
        }
    }

    /// <summary>
    /// Extract embed data from a URL.
    /// </summary>
    private async Task<Embed?> ExtractEmbedAsync(
        string url,
        long messageId,
        int position,
        SnowflakeIdGenerator snowflakeGenerator,
        IStorageService storageService,
        SsrfSafeHttpClient httpClient,
        OpenGraphParser ogParser,
        CancellationToken cancellationToken)
    {
        try
        {
            // Fetch HTML content
            var html = await httpClient.GetAsync(url);

            // Parse OpenGraph metadata
            var ogData = ogParser.Parse(html);

            // Skip if no useful metadata was found
            if (string.IsNullOrEmpty(ogData.Title) &&
                string.IsNullOrEmpty(ogData.Description) &&
                string.IsNullOrEmpty(ogData.ImageUrl))
            {
                _logger.LogDebug("No OpenGraph metadata found for {Url}", url);
                return null;
            }

            var embedId = snowflakeGenerator.NextId();
            var embed = new Embed
            {
                Id = embedId,
                MessageId = messageId,
                Url = url.Length > 2048 ? url.Substring(0, 2048) : url,
                Title = TruncateString(ogData.Title, 256),
                Description = TruncateString(ogData.Description, 4096),
                SiteName = TruncateString(ogData.SiteName, 128),
                Color = TruncateString(ogData.Color, 7),
                Position = position
            };

            // Download and proxy image if available
            if (!string.IsNullOrEmpty(ogData.ImageUrl))
            {
                try
                {
                    var imageBytes = await httpClient.DownloadImageAsync(ogData.ImageUrl);

                    // Determine file extension from content
                    var extension = GetImageExtension(imageBytes);
                    if (extension != null)
                    {
                        // Generate S3 key: embeds/yyyy/MM/embedId.ext
                        var now = DateTimeOffset.UtcNow;
                        var s3Key = $"embeds/{now:yyyy}/{now:MM}/{embedId}{extension}";

                        // Upload to S3
                        var contentType = GetImageContentType(extension);
                        await storageService.UploadAsync(s3Key, imageBytes, contentType);

                        // Generate download URL (valid for 7 days)
                        var imageUrl = await storageService.GenerateDownloadUrlAsync(s3Key, TimeSpan.FromDays(7));

                        embed.ImageS3Key = s3Key;
                        embed.ImageUrl = TruncateString(imageUrl, 512);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to download/proxy image for embed {EmbedId}", embedId);
                    // Continue without image
                }
            }

            return embed;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract embed from {Url}", url);
            throw;
        }
    }

    /// <summary>
    /// Truncate a string to a maximum length.
    /// </summary>
    private static string? TruncateString(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return value.Length <= maxLength ? value : value.Substring(0, maxLength);
    }

    /// <summary>
    /// Detect image file extension from byte signature.
    /// </summary>
    private static string? GetImageExtension(byte[] imageBytes)
    {
        if (imageBytes.Length < 4)
        {
            return null;
        }

        // PNG: 89 50 4E 47
        if (imageBytes[0] == 0x89 && imageBytes[1] == 0x50 && imageBytes[2] == 0x4E && imageBytes[3] == 0x47)
        {
            return ".png";
        }

        // JPEG: FF D8 FF
        if (imageBytes[0] == 0xFF && imageBytes[1] == 0xD8 && imageBytes[2] == 0xFF)
        {
            return ".jpg";
        }

        // GIF: 47 49 46
        if (imageBytes[0] == 0x47 && imageBytes[1] == 0x49 && imageBytes[2] == 0x46)
        {
            return ".gif";
        }

        // WEBP: 52 49 46 46 ... 57 45 42 50
        if (imageBytes.Length >= 12 &&
            imageBytes[0] == 0x52 && imageBytes[1] == 0x49 && imageBytes[2] == 0x46 && imageBytes[3] == 0x46 &&
            imageBytes[8] == 0x57 && imageBytes[9] == 0x45 && imageBytes[10] == 0x42 && imageBytes[11] == 0x50)
        {
            return ".webp";
        }

        return null;
    }

    /// <summary>
    /// Get content type for image extension.
    /// </summary>
    private static string GetImageContentType(string extension)
    {
        return extension switch
        {
            ".png" => "image/png",
            ".jpg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };
    }

    /// <summary>
    /// Message metadata structure for URL extraction.
    /// </summary>
    private sealed class MessageMetadata
    {
        public List<string>? Urls { get; set; }
    }
}
