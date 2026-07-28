using System;
using Volo.Abp.Domain.Entities;

namespace Dignite.Abp.FlexFields.MongoDB;

/// <summary>
/// Plays the role of a downstream host's own entity (e.g. a CMS's <c>Entry</c>): an ordinary aggregate that
/// also carries a flex field value bag. The bag is an embedded document here rather than a JSON column -
/// which is the whole reason this provider queries it in place.
/// </summary>
public class TestArticle : AggregateRoot<Guid>, IHasFlexFields
{
    public virtual string Title { get; set; } = string.Empty;

    public virtual FlexFieldDictionary FlexFields { get; set; }

    protected TestArticle()
    {
        FlexFields = new FlexFieldDictionary();
    }

    public TestArticle(Guid id, string title)
        : base(id)
    {
        Title = title;
        FlexFields = new FlexFieldDictionary();
    }
}
