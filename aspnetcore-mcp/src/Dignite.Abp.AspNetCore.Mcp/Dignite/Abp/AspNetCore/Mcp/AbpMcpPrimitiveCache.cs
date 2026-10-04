using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Dignite.Abp.AspNetCore.Mcp;

/// <summary>
/// Remembers which registration produced which SDK primitive instance, per service provider.
/// <para>
/// The ownership cannot be carried on the primitive itself: the SDK's only slot for it is the create
/// options' <c>Metadata</c>, and setting that REPLACES the metadata it would otherwise build from the
/// method's attributes - including <c>[Authorize]</c>, which the authorization filter reads from there.
/// Tagging a tool that way would silently make it callable by everyone.
/// </para>
/// </summary>
public sealed class AbpMcpPrimitiveCache
{
    private readonly ConcurrentDictionary<AbpMcpPrimitiveRegistration, object> _primitives = new();
    private readonly ConcurrentDictionary<object, AbpMcpPrimitiveRegistration> _owners = new(ReferenceEqualityComparer.Instance);

    public T GetOrCreate<T>(AbpMcpPrimitiveRegistration registration, Func<T> factory)
        where T : class
    {
        var primitive = (T)_primitives.GetOrAdd(registration, _ => factory());
        _owners.TryAdd(primitive, registration);
        return primitive;
    }

    /// <summary>The registration that produced <paramref name="primitive"/>, or null if none of ours did.</summary>
    public AbpMcpPrimitiveRegistration? FindOwner(object primitive)
    {
        return _owners.TryGetValue(primitive, out var registration) ? registration : null;
    }
}
