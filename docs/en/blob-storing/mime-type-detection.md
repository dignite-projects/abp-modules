```json
//[doc-seo]
{
    "Description": "Detect the MIME type of content from its bytes with IMimeTypeDetector, reconcile it with the file extension, and replace or extend the detection policy."
}
```

# MIME Type Detection

`IMimeTypeDetector` decides the MIME type of content **from its bytes**, never from what an uploader claims. The [allowed content types](allowed-content-types.md) and [image](imaging.md) contributors use it, and your application can [inject](https://abp.io/docs/latest/framework/fundamentals/dependency-injection) it too. It is part of the `Dignite.Abp.BlobStoring.Pipeline` package.

## Usage

````csharp
public class AttachmentManager : DomainService
{
    private readonly IMimeTypeDetector _mimeTypeDetector;

    public AttachmentManager(IMimeTypeDetector mimeTypeDetector)
    {
        _mimeTypeDetector = mimeTypeDetector;
    }

    public async Task<string> GetMimeTypeAsync(Stream content, string fileName)
    {
        // May throw a business exception with the ContentTypeMismatch code
        return await _mimeTypeDetector.DetectAsync(content, fileName);
    }
}
````

`DetectAsync(stream, fileName, cancellationToken)` gets the following parameters:

* **stream** (`Stream`): The content. It must be **seekable**; it is read from position `0`, and its position is restored to `0` afterwards. Use `BlobStreamBuffering.CopyToBufferAsync` to make a capped, seekable copy of a request stream (see [Saving Uploaded Files](saving-uploaded-files.md)).
* **fileName** (`string`, optional): A file name or a path-like BLOB name. Only its **extension** is used, to reconcile it with the content.
* **cancellationToken** (`CancellationToken`, optional).

It returns the MIME type, or `application/octet-stream` (`MimeTypeDetector.DefaultMimeType`) for content it cannot identify.

## The Detection Engine

The engine is [FileSignatures](https://github.com/neilharvey/FileSignatures) (MIT), behind its `IFileFormatInspector` service. It also looks inside archives, so an Office Open XML or OpenDocument file is told apart from a plain ZIP file.

* Signatures short enough to occur in text (`BM`, `MZ`, `ID3`, ...) must be confirmed by the rest of the header.
* A header probe of its own covers formats FileSignatures does not know: WAV, AVIF, HEIC and other ISO media brands, MP3 frames without an ID3 tag, generic compound files and empty ZIP files.
* Only the first 1,445 bytes are sampled, plus the archive structure of ZIP-based formats. A ZIP file whose `[Content_Types].xml` entry is larger than 1 MB is not opened, and is classified as a plain ZIP file.

## The Detection Policy

* **Content first.** Content with a known signature decides the type.
* **The extension is reconciled by kind.** If the extension of `fileName` is of a different kind than the content (an executable named `.png`, a ZIP file named `.pdf`, a PNG image named `.txt`), the content is rejected with the `Dignite.Abp.BlobStoring:ContentTypeMismatch` error code. Within one kind, the content wins: a PNG image named `.jpg` is `image/png`. Generic containers are the exception, where the extension says which format the container holds: a ZIP file named `.epub` is `application/epub+zip`.
* **Some extensions require a signature.** Each known extension is marked as either always carrying a signature (`.png`, `.pdf`, `.docx`, `.zip`, `.exe`, ...) or not (`.txt`, `.svg`, `.mp4`, `.mp3`, `.tar`, ...). Content without a recognized signature that is named with the first kind is rejected as a mismatch; named with the second kind, it takes the type of its extension.
* **Text is sniffed.** Content without a signature is tested for text: it must not contain a [binary data byte](https://mimesniff.spec.whatwg.org/#binary-data-byte), but it does not have to be valid UTF-8, so legacy encodings still count as text. HTML, SVG and XML are recognized from their markup (the root element, after skipping a byte order mark, comments, processing instructions and a doctype). Other text takes the type of its text extension, or `text/plain`. All text types are one kind, so HTML named `.txt` is `text/html`, while text named `.png` is a mismatch.
* **Unknown is `application/octet-stream`.** That includes binary content with an unknown or missing extension. An empty stream takes the type of its extension, unless that format requires a signature.

## What the Detector Does Not Do

Magic bytes are **necessary, not sufficient**:

* A *polyglot*, a file that is a valid image and also valid script, HTML or archive, passes a signature check. The detector tells you what a file claims to be at its start, not that nothing else is hidden in it.
* An allowed type can still be hostile: a PDF file with embedded script, an Office file with macros. Allow only the types a container needs. The detector recognizes the macro-enabled Office types, so leaving them off the list rejects them.
* Serve user content with the `X-Content-Type-Options: nosniff` header and, for anything that is not meant to render inline, `Content-Disposition: attachment`, ideally from a separate origin.

## Customizing the Detection

### Recognizing More Formats

Register your own `IFileFormatInspector` with additional FileSignatures `FileFormat` types. The default registration of the module is a replaceable singleton:

````csharp
public override void ConfigureServices(ServiceConfigurationContext context)
{
    context.Services.Replace(ServiceDescriptor.Singleton<IFileFormatInspector>(
        _ => new FileFormatInspector(FileFormatLocator.GetFormats().Append(new MyFormat()))));
}
````

### Changing the Policy

`MimeTypeDetector` is the default implementation. It is a transient service, and every member is `virtual` or `protected virtual` (`DetectFromSignature`, `DetectFromContent`, `DetectTextFormat`, `GetMimeTypeFromExtension`, `GetKind`, `IsGenericContainer`, ...), so you can derive from it and [replace](https://abp.io/docs/latest/framework/fundamentals/dependency-injection#replace-a-service) it. The following example teaches the detector a text format it does not know. `GetKind` is overridden as well, because content and extension are compared by kind, and a type outside `text/*` is not of the text kind by default:

````csharp
[Dependency(ReplaceServices = true)]
[ExposeServices(typeof(IMimeTypeDetector))]
public class MyMimeTypeDetector : MimeTypeDetector
{
    public MyMimeTypeDetector(IFileFormatInspector fileFormatInspector)
        : base(fileFormatInspector)
    {
    }

    protected override (string MimeType, bool RequiresSignature)? GetMimeTypeFromExtension(string extension)
    {
        if (string.Equals(extension, ".yaml", StringComparison.OrdinalIgnoreCase))
        {
            return ("application/yaml", false);
        }

        return base.GetMimeTypeFromExtension(extension);
    }

    protected override string GetKind(string mimeType)
    {
        return mimeType == "application/yaml" ? TextKind : base.GetKind(mimeType);
    }
}
````

The second value of `GetMimeTypeFromExtension` says whether content with this extension must carry a recognized signature; text formats have none.

## See Also

* [Allowed Content Types](allowed-content-types.md)
* [Saving Uploaded Files](saving-uploaded-files.md)
