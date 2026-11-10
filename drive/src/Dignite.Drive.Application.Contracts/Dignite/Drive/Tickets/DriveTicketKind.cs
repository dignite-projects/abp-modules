namespace Dignite.Drive.Tickets;

public enum DriveTicketKind
{
    /// <summary>Lets a browser store one file, once.</summary>
    Upload = 0,

    /// <summary>Lets a browser read one file, until the ticket expires.</summary>
    Download = 1
}
