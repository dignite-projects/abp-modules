using System.Text;

namespace Dignite.Drive;

/// <summary>
/// Real file content for upload tests: the Drive detects the type from the bytes and refuses a name whose extension
/// the content contradicts, so a test file has to be what its name says it is.
/// </summary>
public static class DriveTestFiles
{
    /// <summary>Enough of a PDF for signature detection: the header and an end marker.</summary>
    public static byte[] Pdf(string text = "not really a pdf, but not an image either")
    {
        return Encoding.ASCII.GetBytes($"%PDF-1.4\n% {text}\n%%EOF\n");
    }

    public static byte[] Text(string text = "plain text")
    {
        return Encoding.UTF8.GetBytes(text);
    }
}
