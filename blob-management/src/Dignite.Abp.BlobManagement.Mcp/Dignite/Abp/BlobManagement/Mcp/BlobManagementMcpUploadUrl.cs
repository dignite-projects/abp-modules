using System;

namespace Dignite.Abp.BlobManagement.Mcp;

/// <summary>An upload link as <c>blob_create_upload_url</c> hands it over: where to send the file, and how.</summary>
public class BlobManagementMcpUploadUrl
{
    /// <summary>
    /// Send the file here as a form <c>POST</c>, the file in the field <see cref="FormField"/>. The link is the credential:
    /// good for one upload, until <see cref="ExpiresAt"/>.
    /// </summary>
    public string Url { get; set; } = default!;

    public string Method { get; set; } = "POST";

    public string FormField { get; set; } = "content";

    public DateTime ExpiresAt { get; set; }

    public string ContainerName { get; set; } = default!;

    /// <summary>The name the file gets, whatever it is called on the sending machine.</summary>
    public string FileName { get; set; } = default!;

    /// <summary>
    /// A command that sends the file from sh, bash, zsh or cmd.exe, with <c>FILE_PATH</c> to be replaced by the path of the
    /// file. Its output is the stored file.
    /// </summary>
    public string Command { get; set; } = default!;

    /// <summary><see cref="Command"/> for PowerShell: <c>curl.exe</c>, which Windows ships, as <c>curl</c> is another command there.</summary>
    public string PowerShellCommand { get; set; } = default!;
}
