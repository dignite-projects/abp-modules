using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Volo.Abp;

namespace Dignite.Abp.AspNetCore.Mcp;

/// <summary>
/// Fills in the once-per-server parts of the SDK's <see cref="McpServerOptions"/> from the host's
/// <see cref="AbpMcpServerOptions"/> and the contributing modules' instruction sections.
/// <para>
/// In stateless mode the SDK builds a fresh <see cref="McpServerOptions"/> for every HTTP request, so
/// <see cref="Configure"/> runs per request. Everything it copies is fixed once the application has started,
/// so it is worked out once - this is registered as a singleton - and only assigned per request.
/// </para>
/// </summary>
public class AbpMcpServerOptionsSetup : IConfigureOptions<McpServerOptions>
{
    private readonly Lazy<Implementation> _serverInfo;
    private readonly Lazy<string?> _instructions;
    private readonly Lazy<bool> _hasResourceListContributors;

    protected AbpMcpServerOptions Options { get; }

    protected AbpMcpModuleRegistry Registry { get; }

    protected IApplicationInfoAccessor ApplicationInfoAccessor { get; }

    public AbpMcpServerOptionsSetup(
        IOptions<AbpMcpServerOptions> options,
        AbpMcpModuleRegistry registry,
        IApplicationInfoAccessor applicationInfoAccessor)
    {
        Options = options.Value;
        Registry = registry;
        ApplicationInfoAccessor = applicationInfoAccessor;

        _serverInfo = new Lazy<Implementation>(CreateServerInfo);
        _instructions = new Lazy<string?>(ComposeInstructions);
        _hasResourceListContributors = new Lazy<bool>(
            () => Registry.Modules.Any(module => module.ResourceListContributors.Count > 0));
    }

    public virtual void Configure(McpServerOptions options)
    {
        options.ServerInfo = _serverInfo.Value;
        options.ServerInstructions = _instructions.Value;

        // The server's one resources/list slot, taken only when some module has dynamic resources to list;
        // otherwise the SDK's own listing of the static resources is left as it is.
        if (_hasResourceListContributors.Value)
        {
            options.Handlers.ListResourcesHandler = ListResourcesAsync;
        }
    }

    protected virtual Implementation CreateServerInfo()
    {
        return new Implementation
        {
            Name = Options.ServerName
                   ?? ApplicationInfoAccessor.ApplicationName
                   ?? Assembly.GetEntryAssembly()?.GetName().Name
                   ?? "Dignite.Abp.Mcp",
            Version = Options.ServerVersion
        };
    }

    /// <summary>
    /// The host's opening section, then each module's sections in registration order - which is module
    /// dependency order, so a module's text always follows the text of the modules it builds on.
    /// </summary>
    protected virtual string? ComposeInstructions()
    {
        var sections = new[] { Options.Instructions }
            .Concat(Registry.Modules.SelectMany(module => module.Instructions))
            .Where(section => !section.IsNullOrWhiteSpace())
            .Select(section => section!.Trim())
            .ToList();

        return sections.Count == 0 ? null : string.Join("\n\n", sections);
    }

    private static ValueTask<ListResourcesResult> ListResourcesAsync(
        RequestContext<ListResourcesRequestParams> request,
        CancellationToken cancellationToken)
    {
        var services = request.Services
            ?? throw new AbpException("An MCP resources/list request carries no service provider.");
        return new ValueTask<ListResourcesResult>(services
            .GetRequiredService<AbpMcpResourceListAggregator>()
            .ListAsync(services, cancellationToken));
    }
}
