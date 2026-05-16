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
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken).ConfigureAwait(false);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingEmbedsAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing embeds");
            }

            await Task.Delay(TimeSpan.FromSeconds(PollingIntervalSeconds), stoppingToken).ConfigureAwait(false);
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
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
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

        // Track which messages had embeds created so we can notify after save
        var messagesWithEmbeds = new List<(long MessageId, long ConversationId)>();

        foreach (var message in pendingMessages)
        {
            try
            {
                var hadEmbeds = await ProcessMessageEmbedsAsync(
                    message,
                    snowflakeGenerator,
                    storageService,
                    httpClient,
                    ogParser,
                    context,
                    cancellationToken);

                if (hadEmbeds)
                {
                    messagesWithEmbeds.Add((message.Id, message.ConversationId));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process embeds for message {MessageId}", message.Id);

                // Mark as processed even on failure to avoid infinite retries
                message.EmbedsProcessed = true;
            }
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Send notifications after save
        foreach (var (messageId, conversationId) in messagesWithEmbeds)
        {
            await notificationService.NotifyConversationAsync(
                conversationId,
                "Chat_MessageEmbedded",
                new { messageId, conversationId }, cancellationToken);
        }
    }

    /// <summary>
    /// Process embeds for a single message. Returns true if any embeds were created.
    /// </summary>
    private async Task<bool> ProcessMessageEmbedsAsync(
        Message message,
        SnowflakeIdGenerator snowflakeGenerator,
        IStorageService storageService,
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
            return false;
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

        var embedResults = await Task.WhenAll(extractionTasks).ConfigureAwait(false);

        var embeds = embedResults
            .Where(e => e != null)
            .Select((embed, idx) => { embed!.Position = idx; return embed; })
            .ToList();
        embeds.ForEach(embed => context.Embeds.Add(embed!));

        // Mark message as processed
        message.EmbedsProcessed = true;

        return embeds.Count > 0;
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
            return await ExtractEmbedAsync(url, messageId, position, snowflakeGenerator, storageService, httpClient, ogParser, cancellationToken).ConfigureAwait(false);
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
            var html = await httpClient.GetAsync(url).ConfigureAwait(false);

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
                    var imageBytes = await httpClient.DownloadImageAsync(ogData.ImageUrl).ConfigureAwait(false);

                    // Determine file extension from content
                    var extension = GetImageExtension(imageBytes);
                    if (extension != null)
                    {
                        // Generate S3 key: embeds/yyyy/MM/embedId.ext
                        var now = DateTimeOffset.UtcNow;
                        var s3Key = $"embeds/{now:yyyy}/{now:MM}/{embedId}{extension}";

                        // Upload to S3
                        var contentType = GetImageContentType(extension);
                        await storageService.UploadAsync(s3Key, imageBytes, contentType).ConfigureAwait(false);

                        // Generate download URL (valid for 7 days)
                        var imageUrl = await storageService.GenerateDownloadUrlAsync(s3Key, TimeSpan.FromDays(7)).ConfigureAwait(false);

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
    /// Thin wrapper around <see cref="ImageFormatDetector"/>; kept private so
    /// the existing call sites in <see cref="EmbedExtractor"/> don't need to
    /// change shape. See kanban #124.
    /// </summary>
    private static string? GetImageExtension(byte[] imageBytes)
    {
        var format = ImageFormatDetector.Detect(imageBytes);
        return ImageFormatDetector.GetExtension(format);
    }

    /// <summary>
    /// Get content type for an image file extension (with leading dot).
    /// Mirrors <see cref="ImageFormatDetector.GetContentType"/> but keyed on
    /// the extension produced by <see cref="GetImageExtension"/>.
    /// </summary>
    private static string GetImageContentType(string extension)
    {
        return extension switch
        {
            ".png" => ImageFormatDetector.GetContentType(ImageFormat.Png),
            ".jpg" => ImageFormatDetector.GetContentType(ImageFormat.Jpeg),
            ".gif" => ImageFormatDetector.GetContentType(ImageFormat.Gif),
            ".webp" => ImageFormatDetector.GetContentType(ImageFormat.Webp),
            _ => ImageFormatDetector.GetContentType(ImageFormat.Unknown),
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
