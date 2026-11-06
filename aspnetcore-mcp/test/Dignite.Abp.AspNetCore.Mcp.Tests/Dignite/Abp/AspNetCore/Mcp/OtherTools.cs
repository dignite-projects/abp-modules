using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Volo.Abp.DependencyInjection;

namespace Dignite.Abp.AspNetCore.Mcp;

/// <summary>
/// A second MCP module, "other", registered only by <see cref="AbpMcpMultiEndpoint_Tests"/>'s host - the one that
/// serves it at its own endpoint, apart from the "test" module.
/// </summary>
[McpServerToolType]
public class OtherTools : ITransientDependency
{
    [McpServerTool(Name = "other_ping", ReadOnly = true)]
    public virtual string Ping() => "pong";
}

[McpServerResourceType]
public class OtherResources : ITransientDependency
{
    [McpServerResource(UriTemplate = "other://info", Name = "other_info", MimeType = "text/plain")]
    public virtual string Info() => "other info";
}

[McpServerPromptType]
public class OtherPrompts : ITransientDependency
{
    [McpServerPrompt(Name = "other_hello")]
    public virtual string Hello() => "hello";
}

public class OtherResourceListContributor : IAbpMcpResourceListContributor, ITransientDependency
{
    public Task<IReadOnlyCollection<Resource>?> ListAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyCollection<Resource>?>(new[]
        {
            new Resource { Uri = "other://dynamic/1", Name = "other_dynamic_1" }
        });
    }
}

/// <summary>Endpoint metadata only the "other" endpoint's own convention adds.</summary>
public sealed class OtherEndpointMarker
{
}
