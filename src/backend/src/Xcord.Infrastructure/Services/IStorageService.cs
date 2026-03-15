namespace Xcord.Infrastructure.Services;

/// <summary>
/// Service for managing file storage with S3/MinIO.
/// </summary>
public interface IStorageService
{
    /// <summary>
    /// Generate a pre-signed upload URL for uploading a file.
    /// </summary>
    /// <param name="key">S3 object key</param>
    /// <param name="contentType">Content type for the upload</param>
    /// <param name="maxSize">Maximum allowed file size</param>
    /// <param name="expiry">URL expiration duration</param>
    /// <returns>Pre-signed PUT URL</returns>
    Task<string> GenerateUploadUrlAsync(string key, string contentType, long maxSize, TimeSpan expiry);

    /// <summary>
    /// Generate a pre-signed download URL for accessing a file.
    /// </summary>
    /// <param name="key">S3 object key</param>
    /// <param name="expiry">URL expiration duration</param>
    /// <returns>Pre-signed GET URL</returns>
    Task<string> GenerateDownloadUrlAsync(string key, TimeSpan expiry);

    /// <summary>
    /// Delete an object from S3.
    /// </summary>
    /// <param name="key">S3 object key</param>
    Task DeleteAsync(string key);

    /// <summary>
    /// Check if an object exists in S3.
    /// </summary>
    /// <param name="key">S3 object key</param>
    /// <returns>True if the object exists</returns>
    Task<bool> ExistsAsync(string key);

    /// <summary>
    /// Upload bytes directly to S3.
    /// </summary>
    /// <param name="key">S3 object key</param>
    /// <param name="data">Data to upload</param>
    /// <param name="contentType">Content type</param>
    Task UploadAsync(string key, byte[] data, string contentType);

    /// <summary>
    /// Download bytes directly from S3.
    /// </summary>
    /// <param name="key">S3 object key</param>
    /// <returns>Downloaded bytes</returns>
    Task<byte[]> DownloadAsync(string key);

    /// <summary>
    /// Get the total size of all objects in the bucket.
    /// </summary>
    /// <returns>Total size in bytes</returns>
    Task<long> GetBucketSizeAsync(CancellationToken ct);
}
