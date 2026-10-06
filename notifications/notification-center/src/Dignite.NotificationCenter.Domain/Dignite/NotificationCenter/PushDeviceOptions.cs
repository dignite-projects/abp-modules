namespace Dignite.NotificationCenter;

public class PushDeviceOptions
{
    /// <summary>
    /// How many devices one user keeps registered. Registering one more forgets the device seen least recently. This
    /// is what bounds the registry: a device whose app was uninstalled is not always reported dead, and there is no
    /// cleanup worker.
    /// </summary>
    public int MaxDevicesPerUser { get; set; } = 10;
}
