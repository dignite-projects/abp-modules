using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace Dignite.Abp.AspNetCore.Mcp;

/// <summary>
/// Runs every module's <see cref="IAbpMcpResourceListContributor"/> and merges what they list - the
/// server's one <c>resources/list</c> handler, installed only when at least one contributor exists. The
/// SDK appends the static resources itself.
/// </summary>
public class AbpMcpResourceListAggregator : ITransientDependency
{
    public ILogger<AbpMcpResourceListAggregator> Logger { get; set; } = NullLogger<AbpMcpResourceListAggregator>.Instance;

    protected AbpMcpModuleRegistry Registry { get; }

    public AbpMcpResourceListAggregator(AbpMcpModuleRegistry registry)
    {
        Registry = registry;
    }

    public virtual async Task<ListResourcesResult> ListAsync(IServiceProvider requestServices, CancellationToken cancellationToken)
    {
        var result = new ListResourcesResult();

        // The static resources the SDK appends after this handler, so that a contributor repeating one of
        // them - or another contributor - does not list the same URI twice.
        var listed = new HashSet<string>(
            requestServices.GetServices<McpServerResource>()
                .Where(resource => !resource.IsTemplated && resource.ProtocolResource != null)
                .Select(resource => resource.ProtocolResource!.Uri),
            StringComparer.Ordinal);

        foreach (var module in Registry.Modules)
        {
            foreach (var contributorType in module.ResourceListContributors)
            {
                var contributor = (IAbpMcpResourceListContributor)requestServices.GetRequiredService(contributorType);
                var resources = await contributor.ListAsync(cancellationToken);
                if (resources == null)
                {
                    continue;
                }

                foreach (var resource in resources)
                {
                    // The static registrations are checked once at startup; these exist only per request, so
                    // this is the only place their namespace can be enforced.
                    if (!resource.Uri.StartsWith(module.UriScheme + ":", StringComparison.Ordinal))
                    {
                        throw new AbpException(
                            $"{contributorType.FullName} listed the resource '{resource.Uri}', outside MCP module " +
                            $"'{module.Name}''s '{module.UriScheme}:' scheme.");
                    }

                    // Skipped rather than thrown: the listing is still correct without the repeat, and failing
                    // here would take every other module's resources down with it. Logged so it gets fixed.
                    if (!listed.Add(resource.Uri))
                    {
                        Logger.LogWarning(
                            "{Contributor} listed the resource '{Uri}', which is already listed; the duplicate is skipped.",
                            contributorType.FullName, resource.Uri);
                        continue;
                    }

                    result.Resources.Add(resource);
                }
            }
        }

        return result;
    }
}
