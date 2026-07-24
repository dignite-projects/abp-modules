using System;

namespace Dignite.Abp.Notifications;

/// <summary>Controls inline-versus-background distribution and recipient batch sizing.</summary>
public class NotificationDistributionOptions
{
    /// <summary>Hard safeguard for every distribution batch size.</summary>
    public const int MaxBatchSize = 10_000;

    /// <summary>Maximum explicit recipients distributed inline instead of through a background job.</summary>
    public int DirectDistributionUserThreshold { get; set; } = 5;

    /// <summary>Maximum recipients resolved, persisted, and published per batch.</summary>
    public int RecipientBatchSize { get; set; } = 256;

    internal void Validate()
    {
        if (DirectDistributionUserThreshold < 0 || DirectDistributionUserThreshold > MaxBatchSize)
        {
            throw new InvalidOperationException(
                $"{nameof(NotificationDistributionOptions)}.{nameof(DirectDistributionUserThreshold)} must be " +
                $"between 0 and {MaxBatchSize}.");
        }

        if (RecipientBatchSize < 1 || RecipientBatchSize > MaxBatchSize)
        {
            throw new InvalidOperationException(
                $"{nameof(NotificationDistributionOptions)}.{nameof(RecipientBatchSize)} must be between 1 and {MaxBatchSize}.");
        }
    }
}
