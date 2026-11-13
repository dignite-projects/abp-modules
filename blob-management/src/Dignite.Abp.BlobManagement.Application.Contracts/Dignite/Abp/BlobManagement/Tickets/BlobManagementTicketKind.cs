namespace Dignite.Abp.BlobManagement.Tickets;

public enum BlobManagementTicketKind
{
    /// <summary>Lets a browser store one file, once.</summary>
    Upload = 0,

    /// <summary>Lets a browser read one file, until the ticket expires.</summary>
    Download = 1,

    /// <summary>
    /// Lets whoever holds it add one file to a container, once, as the user who issued it - whose permissions are checked
    /// again when it is used. Issued by <c>IBlobManagementFileAppService.CreateUploadTicketAsync</c>.
    /// </summary>
    FileUpload = 2
}
