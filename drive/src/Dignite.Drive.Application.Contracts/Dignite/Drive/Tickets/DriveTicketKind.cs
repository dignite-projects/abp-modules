namespace Dignite.Drive.Tickets;

public enum DriveTicketKind
{
    /// <summary>Lets a browser store one file, once.</summary>
    Upload = 0,

    /// <summary>Lets a browser read one file, until the ticket expires.</summary>
    Download = 1,

    /// <summary>
    /// Lets whoever holds it add one file to a user drive, once, as the user who issued it - whose permissions are checked
    /// again when it is used. Issued by <c>IDriveFileAppService.CreateUploadTicketAsync</c>.
    /// </summary>
    FileUpload = 2
}
