using System;

namespace Dignite.Abp.FlexFields;

/// <summary>
/// Test helpers for building <see cref="FlexFieldData"/> instances directly - this project references
/// Abstractions only, so there is no <c>IFlexField</c> entity available to stand in for a downstream's
/// field definition (contrast the EF Core test project's <c>TestField</c>). <see cref="FlexFieldData"/> is
/// a data carrier to instantiate, not to subclass, so these are extension methods rather than a test
/// double type.
/// </summary>
public static class FlexFieldDataTestExtensions
{
    public static FlexFieldData Create(string name, string fieldTypeName, string? description = null)
    {
        return new FlexFieldData(Guid.NewGuid(), name, displayName: name, fieldTypeName: fieldTypeName, description: description);
    }

    /// <summary>
    /// Merges this definition with the per-usage flags and a value, the way a host's
    /// <c>IFlexFieldProvider&lt;TEntity&gt;</c> implementation would.
    /// </summary>
    public static FlexFieldValue ToValue(this FlexFieldData field, object? value, bool searchable = true, bool required = false)
    {
        return new FlexFieldValue(field, required, searchable, value);
    }
}
