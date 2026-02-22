using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Sockets;

namespace Xcord.Infrastructure.Services;

/// <summary>
/// SSRF-safe HTTP client wrapper that validates target IPs before making requests.
/// </summary>
public sealed class SsrfSafeHttpClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SsrfSafeHttpClient> _logger;

    // Private IP ranges to block (SSRF prevention)
    private static readonly (IPAddress Start, IPAddress End)[] PrivateIpRanges = new[]
    {
        (IPAddress.Parse("10.0.0.0"), IPAddress.Parse("10.255.255.255")),           // 10.0.0.0/8
        (IPAddress.Parse("172.16.0.0"), IPAddress.Parse("172.31.255.255")),         // 172.16.0.0/12
        (IPAddress.Parse("192.168.0.0"), IPAddress.Parse("192.168.255.255")),       // 192.168.0.0/16
        (IPAddress.Parse("127.0.0.0"), IPAddress.Parse("127.255.255.255")),         // 127.0.0.0/8
        (IPAddress.Parse("169.254.0.0"), IPAddress.Parse("169.254.255.255")),       // 169.254.0.0/16
    };

    private static readonly IPAddress IPv6Loopback = IPAddress.IPv6Loopback; // ::1

    public SsrfSafeHttpClient(ILogger<SsrfSafeHttpClient> logger)
    {
        _logger = logger;

        // Configure HTTP client with strict timeouts and size limits
        _httpClient = new HttpClient(new SocketsHttpHandler
        {
            ConnectTimeout = TimeSpan.FromSeconds(5),
            PooledConnectionLifetime = TimeSpan.FromMinutes(5)
        })
        {
            Timeout = TimeSpan.FromSeconds(5)
        };

        // Set user agent
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Xcord/1.0 (Link Preview Bot)");
    }

    /// <summary>
    /// Safely fetch a URL with SSRF protection.
    /// </summary>
    /// <param name="url">URL to fetch</param>
    /// <param name="maxSize">Maximum response size in bytes (default: 1MB)</param>
    /// <returns>Response content as string</returns>
    /// <exception cref="InvalidOperationException">Thrown if URL resolves to a private IP</exception>
    /// <exception cref="HttpRequestException">Thrown if request fails</exception>
    public async Task<string> GetAsync(string url, long maxSize = 1_048_576)
    {
        // Parse and validate URL
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "http" && uri.Scheme != "https"))
        {
            throw new InvalidOperationException("Invalid URL");
        }

        // Resolve hostname to IP addresses
        var hostEntry = await Dns.GetHostEntryAsync(uri.Host);

        // Check all resolved IPs for SSRF risks
        foreach (var ipAddress in hostEntry.AddressList)
        {
            if (IsPrivateOrLocalIp(ipAddress))
            {
                _logger.LogWarning("SSRF attempt blocked: {Url} resolves to private IP {IpAddress}", url, ipAddress);
                throw new InvalidOperationException($"URL resolves to a private or local IP address: {ipAddress}");
            }
        }

        // Fetch content with size limit
        using var response = await _httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead);

        // Validate content type (only allow HTML)
        var contentType = response.Content.Headers.ContentType?.MediaType;
        if (contentType == null || !contentType.StartsWith("text/html", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogDebug("Skipping non-HTML content: {Url} (type: {ContentType})", url, contentType);
            throw new InvalidOperationException($"Content-Type must be text/html, got: {contentType}");
        }

        // Check content length before reading
        var contentLength = response.Content.Headers.ContentLength;
        if (contentLength.HasValue && contentLength.Value > maxSize)
        {
            throw new InvalidOperationException($"Response size ({contentLength.Value} bytes) exceeds limit ({maxSize} bytes)");
        }

        response.EnsureSuccessStatusCode();

        // Read content with size limit
        using var stream = await response.Content.ReadAsStreamAsync();
        using var limitedStream = new LimitedStream(stream, maxSize);
        using var reader = new StreamReader(limitedStream);

        return await reader.ReadToEndAsync();
    }

    /// <summary>
    /// Safely download an image with SSRF protection.
    /// </summary>
    /// <param name="url">Image URL to download</param>
    /// <param name="maxSize">Maximum image size in bytes (default: 5MB)</param>
    /// <returns>Image bytes</returns>
    public async Task<byte[]> DownloadImageAsync(string url, long maxSize = 5_242_880)
    {
        // Parse and validate URL
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "http" && uri.Scheme != "https"))
        {
            throw new InvalidOperationException("Invalid image URL");
        }

        // Resolve hostname to IP addresses
        var hostEntry = await Dns.GetHostEntryAsync(uri.Host);

        // Check all resolved IPs for SSRF risks
        foreach (var ipAddress in hostEntry.AddressList)
        {
            if (IsPrivateOrLocalIp(ipAddress))
            {
                _logger.LogWarning("SSRF attempt blocked: {Url} resolves to private IP {IpAddress}", url, ipAddress);
                throw new InvalidOperationException($"URL resolves to a private or local IP address: {ipAddress}");
            }
        }

        // Fetch image with size limit
        using var response = await _httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead);

        // Validate content type (only allow images)
        var contentType = response.Content.Headers.ContentType?.MediaType;
        if (contentType == null || !contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Content-Type must be an image, got: {contentType}");
        }

        // Check content length before reading
        var contentLength = response.Content.Headers.ContentLength;
        if (contentLength.HasValue && contentLength.Value > maxSize)
        {
            throw new InvalidOperationException($"Image size ({contentLength.Value} bytes) exceeds limit ({maxSize} bytes)");
        }

        response.EnsureSuccessStatusCode();

        // Read image bytes with size limit
        using var stream = await response.Content.ReadAsStreamAsync();
        using var limitedStream = new LimitedStream(stream, maxSize);
        using var memoryStream = new MemoryStream();

        await limitedStream.CopyToAsync(memoryStream);
        return memoryStream.ToArray();
    }

    /// <summary>
    /// Check if an IP address is private or local.
    /// </summary>
    private static bool IsPrivateOrLocalIp(IPAddress ipAddress)
    {
        // Check IPv6 loopback
        if (ipAddress.Equals(IPv6Loopback))
        {
            return true;
        }

        // Convert IPv6-mapped IPv4 addresses to IPv4
        if (ipAddress.IsIPv4MappedToIPv6)
        {
            ipAddress = ipAddress.MapToIPv4();
        }

        // Check IPv4 private ranges
        if (ipAddress.AddressFamily == AddressFamily.InterNetwork)
        {
            var addressBytes = ipAddress.GetAddressBytes();
            var addressInt = BitConverter.ToUInt32(addressBytes, 0);

            foreach (var (start, end) in PrivateIpRanges)
            {
                var startBytes = start.GetAddressBytes();
                var endBytes = end.GetAddressBytes();
                var startInt = BitConverter.ToUInt32(startBytes, 0);
                var endInt = BitConverter.ToUInt32(endBytes, 0);

                if (addressInt >= startInt && addressInt <= endInt)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Stream wrapper that enforces a maximum read size.
    /// </summary>
    private sealed class LimitedStream : Stream
    {
        private readonly Stream _baseStream;
        private readonly long _maxSize;
        private long _totalRead;

        public LimitedStream(Stream baseStream, long maxSize)
        {
            _baseStream = baseStream;
            _maxSize = maxSize;
        }

        public override bool CanRead => _baseStream.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => _totalRead;
            set => throw new NotSupportedException();
        }

        public override void Flush() => _baseStream.Flush();

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_totalRead >= _maxSize)
            {
                throw new InvalidOperationException($"Stream size exceeds maximum allowed size of {_maxSize} bytes");
            }

            var remainingBytes = (int)Math.Min(count, _maxSize - _totalRead);
            var bytesRead = _baseStream.Read(buffer, offset, remainingBytes);
            _totalRead += bytesRead;

            return bytesRead;
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (_totalRead >= _maxSize)
            {
                throw new InvalidOperationException($"Stream size exceeds maximum allowed size of {_maxSize} bytes");
            }

            var remainingBytes = (int)Math.Min(count, _maxSize - _totalRead);
            var bytesRead = await _baseStream.ReadAsync(buffer.AsMemory(offset, remainingBytes), cancellationToken);
            _totalRead += bytesRead;

            return bytesRead;
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _baseStream?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
