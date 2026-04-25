using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xcord.Entities;
using Xcord.Infrastructure.Data;

namespace Xcord.Infrastructure.Services;

/// <summary>
/// Default <see cref="IKeyRotationService"/>: generates a new DEK, wraps it under
/// the configured KEK, atomically swaps the active row in encrypted_data_keys,
/// and registers the new key with the in-memory key holder.
/// </summary>
public sealed class KeyRotationService : IKeyRotationService
{
    private readonly AppDbContext _db;
    private readonly IKekProvider _kekProvider;
    private readonly EncryptionKeyHolder _keyHolder;
    private readonly ILogger<KeyRotationService> _logger;

    public KeyRotationService(
        AppDbContext db,
        IKekProvider kekProvider,
        EncryptionKeyHolder keyHolder,
        ILogger<KeyRotationService> logger)
    {
        _db = db;
        _kekProvider = kekProvider;
        _keyHolder = keyHolder;
        _logger = logger;
    }

    public async Task<int> RotateDataKeyAsync(CancellationToken cancellationToken = default)
    {
        var kek = _kekProvider.GetKek();
        if (kek == null)
        {
            throw new InvalidOperationException(
                "Cannot rotate the DEK without a KEK. Configure Encryption:Kek or " +
                "/run/secrets/xcord-kek before invoking key rotation.");
        }

        // Determine next version. The DB column is int but the wire format is a
        // single byte, so we cap at 255. A fresh deployment usually starts at v1.
        var maxVersion = await _db.EncryptedDataKeys
            .AsNoTracking()
            .Select(k => (int?)k.Version)
            .MaxAsync(cancellationToken) ?? 0;

        var nextVersion = maxVersion + 1;
        if (nextVersion > byte.MaxValue)
        {
            throw new InvalidOperationException(
                $"DEK version space exhausted (last version was {maxVersion}). " +
                "Re-encrypting all ciphertext to a single fresh version is required " +
                "before further rotation.");
        }

        var newDek = RandomNumberGenerator.GetBytes(32);
        var wrapped = KeyWrappingService.WrapDek(newDek, kek);
        var now = DateTimeOffset.UtcNow;

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            // Demote the previously-active row, if any.
            var currentActive = await _db.EncryptedDataKeys
                .Where(k => k.IsActive)
                .ToListAsync(cancellationToken);
            foreach (var row in currentActive)
            {
                row.IsActive = false;
            }

            // Save the demotion FIRST so the partial unique index does not see
            // two active rows simultaneously when the new row is inserted.
            if (currentActive.Count > 0)
            {
                await _db.SaveChangesAsync(cancellationToken);
            }

            _db.EncryptedDataKeys.Add(new EncryptedDataKey
            {
                Version = nextVersion,
                WrappedKey = wrapped,
                IsActive = true,
                CreatedAt = now
            });

            await _db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }

        // Update the in-memory key holder so the next Encrypt() uses the new version.
        var dekBase64 = Convert.ToBase64String(newDek);
        _keyHolder.AddKey((byte)nextVersion, dekBase64, isActive: true);

        _logger.LogInformation("DEK rotated to version {Version}", nextVersion);

        return nextVersion;
    }
}
