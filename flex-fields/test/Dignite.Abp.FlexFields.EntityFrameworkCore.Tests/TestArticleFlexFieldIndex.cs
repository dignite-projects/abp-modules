using System;

namespace Dignite.Abp.FlexFields.EntityFrameworkCore;

/// <summary>
/// The downstream's per-host index table: inherits the kernel's value columns and declares its own
/// foreign key to the host, exactly as a CMS's <c>EntryFlexFieldIndex</c> would.
/// </summary>
public class TestArticleFlexFieldIndex : FlexFieldIndexBase<TestArticle>
{
    /// <summary>The downstream's own FK - the kernel maps no relationships.</summary>
    public virtual Guid ArticleId { get; set; }

    protected TestArticleFlexFieldIndex()
    {
    }

    public TestArticleFlexFieldIndex(Guid id, Guid articleId, Guid fieldId, FlexFieldIndexValue value)
        : base(id)
    {
        ArticleId = articleId;
        SetValue(fieldId, value);
    }
}
