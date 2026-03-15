using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xcord.Infrastructure.Options;

namespace Xcord.Infrastructure.Services;

/// <summary>
/// S3/MinIO storage service implementation using AWS SDK.
/// </summary>
public sealed class S3StorageService : IStorageService, IDisposable
{
    private readonly AmazonS3Client _s3Client;
    private readonly string _bucketName;
    private readonly ILogger<S3StorageService> _logger;
    private bool _bucketInitialized = false;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public S3StorageService(
        IOptions<StorageOptions> options,
        ILogger<S3StorageService> logger)
    {
        var storageOptions = options.Value;
        _bucketName = storageOptions.Bucket;
        _logger = logger;

        var config = new AmazonS3Config
        {
            ServiceURL = storageOptions.Endpoint,
            ForcePathStyle = true // Required for MinIO compatibility
        };

        _s3Client = new AmazonS3Client(
            storageOptions.AccessKey,
            storageOptions.SecretKey,
            config);
    }

    /// <summary>
    /// Ensure bucket exists (lazy initialization).
    /// </summary>
    private async Task EnsureBucketExistsAsync()
    {
        if (_bucketInitialized)
        {
            return;
        }

        await _initLock.WaitAsync();
        try
        {
            if (_bucketInitialized)
            {
                return;
            }

            var bucketExists = await Amazon.S3.Util.AmazonS3Util.DoesS3BucketExistV2Async(_s3Client, _bucketName);
            if (!bucketExists)
            {
                _logger.LogInformation("Creating S3 bucket: {BucketName}", _bucketName);
                await _s3Client.PutBucketAsync(_bucketName);
            }

            _bucketInitialized = true;
            _logger.LogInformation("S3 bucket verified: {BucketName}", _bucketName);
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task<string> GenerateUploadUrlAsync(string key, string contentType, long maxSize, TimeSpan expiry)
    {
        await EnsureBucketExistsAsync();

        var request = new GetPreSignedUrlRequest
        {
            BucketName = _bucketName,
            Key = key,
            Verb = HttpVerb.PUT,
            Expires = DateTime.UtcNow.Add(expiry),
            ContentType = contentType
        };

        // Add metadata to enforce content type and size constraints
        request.Metadata.Add("x-amz-meta-content-type", contentType);

        var url = await _s3Client.GetPreSignedURLAsync(request);
        return url;
    }

    public async Task<string> GenerateDownloadUrlAsync(string key, TimeSpan expiry)
    {
        await EnsureBucketExistsAsync();

        var request = new GetPreSignedUrlRequest
        {
            BucketName = _bucketName,
            Key = key,
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.Add(expiry)
        };

        var url = await _s3Client.GetPreSignedURLAsync(request);
        return url;
    }

    public async Task DeleteAsync(string key)
    {
        await EnsureBucketExistsAsync();

        var request = new DeleteObjectRequest
        {
            BucketName = _bucketName,
            Key = key
        };

        await _s3Client.DeleteObjectAsync(request);
        _logger.LogInformation("Deleted S3 object: {Key}", key);
    }

    public async Task<bool> ExistsAsync(string key)
    {
        await EnsureBucketExistsAsync();

        try
        {
            var request = new GetObjectMetadataRequest
            {
                BucketName = _bucketName,
                Key = key
            };

            await _s3Client.GetObjectMetadataAsync(request);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public async Task UploadAsync(string key, byte[] data, string contentType)
    {
        await EnsureBucketExistsAsync();

        using var stream = new MemoryStream(data);
        var request = new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = key,
            InputStream = stream,
            ContentType = contentType
        };

        await _s3Client.PutObjectAsync(request);
        _logger.LogInformation("Uploaded S3 object: {Key} ({Size} bytes)", key, data.Length);
    }

    public async Task<byte[]> DownloadAsync(string key)
    {
        await EnsureBucketExistsAsync();

        var request = new GetObjectRequest
        {
            BucketName = _bucketName,
            Key = key
        };

        using var response = await _s3Client.GetObjectAsync(request);
        using var ms = new MemoryStream();
        await response.ResponseStream.CopyToAsync(ms);
        _logger.LogInformation("Downloaded S3 object: {Key} ({Size} bytes)", key, ms.Length);
        return ms.ToArray();
    }

    public async Task<long> GetBucketSizeAsync(CancellationToken ct)
    {
        await EnsureBucketExistsAsync();

        long totalSize = 0;
        string? continuationToken = null;

        do
        {
            var request = new ListObjectsV2Request
            {
                BucketName = _bucketName,
                ContinuationToken = continuationToken
            };

            var response = await _s3Client.ListObjectsV2Async(request, ct);

            foreach (var obj in response.S3Objects)
            {
                totalSize += obj.Size;
            }

            continuationToken = response.IsTruncated ? response.NextContinuationToken : null;
        } while (continuationToken != null);

        return totalSize;
    }

    public void Dispose()
    {
        _s3Client?.Dispose();
        _initLock?.Dispose();
    }
}
