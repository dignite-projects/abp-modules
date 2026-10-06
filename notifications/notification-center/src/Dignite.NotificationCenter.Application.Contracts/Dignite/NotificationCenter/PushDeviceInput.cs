using System.ComponentModel.DataAnnotations;

namespace Dignite.NotificationCenter;

/// <summary>Identifies one device of the current user by the token its push provider issued.</summary>
public class PushDeviceInput
{
    /// <summary>The push provider that issued <see cref="Token"/>, e.g. <c>"Expo"</c>.</summary>
    [Required]
    [StringLength(PushDeviceConsts.MaxProviderLength)]
    public string Provider { get; set; } = default!;

    /// <summary>The device token, e.g. an <c>ExponentPushToken[…]</c>.</summary>
    [Required]
    [StringLength(PushDeviceConsts.MaxTokenLength)]
    public string Token { get; set; } = default!;
}
