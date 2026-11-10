using System;
using System.Threading.Tasks;
using Dignite.Drive.Tickets;

namespace Dignite.Abp.BlobStoring.Drive;

/// <summary>
/// Asks the Drive for a ticket that lets a browser store or read one file of an application's container directly, without
/// the bytes passing through the application. The application authorizes its user by its own rules first - the Drive
/// checks only the application.
/// <para>
/// An uploaded file skips the application container's own pipeline contributors (the bytes never reach the
/// application); only the Drive container's run. An upload that needs the application's rules cannot use a ticket.
/// </para>
/// </summary>
public interface IDriveTicketClient
{
    /// <summary>
    /// A ticket to store <paramref name="blobName"/> in <paramref name="containerName"/> (an application container
    /// configured with <c>UseDrive</c>), once. <see cref="DriveTicketDto.Url"/> is where the browser posts the file, as the
    /// form field <c>content</c>.
    /// </summary>
    Task<DriveTicketDto> CreateUploadTicketAsync(
        string containerName,
        string blobName,
        long? maxSizeInBytes = null,
        bool overrideExisting = false,
        TimeSpan? lifetime = null);

    /// <summary>A ticket to read <paramref name="blobName"/> until it expires; <see cref="DriveTicketDto.Url"/> is the link.</summary>
    Task<DriveTicketDto> CreateDownloadTicketAsync(string containerName, string blobName, TimeSpan? lifetime = null);
}
