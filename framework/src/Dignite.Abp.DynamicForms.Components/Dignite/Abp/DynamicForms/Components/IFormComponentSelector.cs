using JetBrains.Annotations;

namespace Dignite.Abp.DynamicForms.Components;

public interface IFormComponentSelector
{
    /// <summary>
    /// Get form component using form name
    /// </summary>
    /// <param name="formName">
    /// <see cref="IForm.Name"/>
    /// </param>
    /// <returns></returns>
    [NotNull]
    IFormComponent Get(string formName);
}