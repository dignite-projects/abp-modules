namespace Dignite.Abp.FlexFields.Web;

/// <summary>Model for a <c>Views/Shared/FlexFields/{FieldTypeName}.cshtml</c> display partial.</summary>
public class FlexFieldViewModel
{
    public FlexFieldValue Field { get; }

    /// <summary>Renders bare, without a label wrapper, for use inside a table cell.</summary>
    public bool ShowInList { get; }

    public FlexFieldViewModel(FlexFieldValue field, bool showInList)
    {
        Field = field;
        ShowInList = showInList;
    }
}
