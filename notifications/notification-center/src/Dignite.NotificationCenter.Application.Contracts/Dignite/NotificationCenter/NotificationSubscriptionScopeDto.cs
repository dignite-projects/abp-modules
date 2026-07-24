using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Dignite.NotificationCenter;

/// <summary>
/// Identifies one current-user subscription. A scope is either definition-wide (both entity fields are
/// <see langword="null"/>) or identifies one concrete entity (both entity fields are supplied).
/// </summary>
public class NotificationSubscriptionScopeDto : IValidatableObject
{
    [Required]
    [StringLength(NotificationCenterConsts.MaxNotificationNameLength)]
    public string NotificationName { get; set; } = default!;

    [StringLength(NotificationCenterConsts.MaxEntityTypeNameLength)]
    public string? EntityTypeName { get; set; }

    [StringLength(NotificationCenterConsts.MaxEntityIdLength)]
    public string? EntityId { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if ((EntityTypeName == null) != (EntityId == null))
        {
            yield return new ValidationResult(
                "EntityTypeName and EntityId must either both be supplied or both be null.",
                new[] { nameof(EntityTypeName), nameof(EntityId) });
            yield break;
        }

        if (EntityTypeName != null && string.IsNullOrWhiteSpace(EntityTypeName))
        {
            yield return new ValidationResult(
                "EntityTypeName cannot be empty or whitespace when an entity scope is supplied.",
                new[] { nameof(EntityTypeName) });
        }

        if (EntityId != null && string.IsNullOrWhiteSpace(EntityId))
        {
            yield return new ValidationResult(
                "EntityId cannot be empty or whitespace when an entity scope is supplied.",
                new[] { nameof(EntityId) });
        }
    }
}
