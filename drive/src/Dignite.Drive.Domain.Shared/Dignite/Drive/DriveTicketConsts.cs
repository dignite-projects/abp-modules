namespace Dignite.Drive;

public static class DriveTicketConsts
{
    /// <summary>A ticket's lifetime when the issuer names none: long enough to pick a file and send it.</summary>
    public const int DefaultLifetimeSeconds = 10 * 60;

    /// <summary>The longest lifetime an issuer may ask for.</summary>
    public const int MaxLifetimeSeconds = 60 * 60;
}
