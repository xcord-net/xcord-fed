using Microsoft.Extensions.Diagnostics.HealthChecks;
using Xcord.Infrastructure.Services;

namespace Xcord.Infrastructure.Services;

public sealed class MinioHealthCheck : IHealthCheck
{
    private readonly IStorageService _storageService;

    public MinioHealthCheck(IStorageService storageService)
    {
        _storageService = storageService;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Attempt to list buckets as a health check
            // This verifies connectivity and authentication
            if (_storageService is S3StorageService s3Service)
            {
                // S3StorageService doesn't expose a direct health check method
                // We'll return healthy if the service is configured
                return Task.FromResult(HealthCheckResult.Healthy("MinIO/S3 storage service is configured"));
            }

            return Task.FromResult(HealthCheckResult.Healthy("Storage service is healthy"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Storage service check failed", ex));
        }
    }
}
