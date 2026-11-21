using System;
using System.IO;
using Volo.Abp.BlobStoring;

namespace Dignite.Abp.BlobStoring.Pipeline;

/// <summary>
/// One entry of <see cref="BlobStoringPipelineConfigurationNames.StoredContentTypes"/>: the type content of one type is
/// stored as, and, when the contributor converts only some of it, the rule that says which.
/// </summary>
internal sealed class StoredContentTypeDeclaration
{
    public StoredContentTypeDeclaration(string storedContentType, Func<BlobContainerConfiguration, Stream, bool>? condition)
    {
        StoredContentType = storedContentType;
        Condition = condition;
    }

    public string StoredContentType { get; }

    /// <summary><c>null</c>: every such content is converted.</summary>
    public Func<BlobContainerConfiguration, Stream, bool>? Condition { get; }
}
