using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol.Protocol;

namespace Dignite.Abp.AspNetCore.Mcp;

/// <summary>
/// Contributes concrete resources to <c>resources/list</c> - the ones that cannot be static registrations
/// because they are the caller's data (one entry per document type, per cabinet...).
/// <para>
/// <b>Why a contributor and not the SDK's <c>WithListResourcesHandler</c>.</b> That handler is a single
/// slot on the one server every module shares: the second module to set it silently replaces the first.
/// <see cref="AbpAspNetCoreMcpModule"/> owns the slot and merges every contributor's resources into it,
/// alongside the static resources the SDK lists on its own.
/// </para>
/// <para>
/// Resolved from the request's scope on every <c>resources/list</c>, so it may use the current user and
/// tenant and call application services. It must do its own authorization: list only what the caller may
/// read, and return <c>null</c> or an empty list - never throw - when that is nothing, because an exception
/// here fails the whole listing for every other module too. Every URI must use the contributing module's
/// scheme; one that does not fails the request.
/// </para>
/// </summary>
public interface IAbpMcpResourceListContributor
{
    Task<IReadOnlyCollection<Resource>?> ListAsync(CancellationToken cancellationToken);
}
