using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace Dignite.Abp.AspNetCore.Mcp;

public class AbpMcpResourceListAggregator_Tests
{
    [Fact]
    public async Task Should_Reject_A_Contributed_Resource_Outside_Its_Module_Scheme()
    {
        var services = new ServiceCollection();
        services.AddAbpMcpModule("alpha", mcp => mcp.AddResourceListContributor<StrayContributor>());
        using var provider = services.BuildServiceProvider();

        var aggregator = new AbpMcpResourceListAggregator(provider.GetRequiredService<AbpMcpModuleRegistry>());

        var exception = await Should.ThrowAsync<AbpException>(() => aggregator.ListAsync(provider, CancellationToken.None));
        exception.Message.ShouldContain("'beta://x'");
    }

    [Fact]
    public async Task Should_Skip_A_Contributor_That_Lists_Nothing()
    {
        var services = new ServiceCollection();
        services.AddAbpMcpModule("alpha", mcp => mcp.AddResourceListContributor<EmptyContributor>());
        using var provider = services.BuildServiceProvider();

        var result = await new AbpMcpResourceListAggregator(provider.GetRequiredService<AbpMcpModuleRegistry>())
            .ListAsync(provider, CancellationToken.None);

        result.Resources.ShouldBeEmpty();
    }

    /// <summary>
    /// A URI already listed - statically, or by an earlier contributor - is listed once, not again.
    /// </summary>
    [Fact]
    public async Task Should_List_Each_Uri_Once()
    {
        var services = new ServiceCollection();
        services.AddAbpMcpModule("alpha", mcp => mcp
            .AddResources<StaticAlphaResources>()
            .AddResourceListContributor<RepeatingContributor>());
        using var provider = services.BuildServiceProvider();

        var result = await new AbpMcpResourceListAggregator(provider.GetRequiredService<AbpMcpModuleRegistry>())
            .ListAsync(provider, CancellationToken.None);

        result.Resources.Select(resource => resource.Uri).ShouldBe(new[] { "alpha://dynamic" });
    }

    [McpServerResourceType]
    public class StaticAlphaResources
    {
        [McpServerResource(UriTemplate = "alpha://static", Name = "alpha_static")]
        public string Static() => "static";
    }

    public class RepeatingContributor : IAbpMcpResourceListContributor
    {
        public Task<IReadOnlyCollection<Resource>?> ListAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyCollection<Resource>?>(new[]
            {
                new Resource { Uri = "alpha://static", Name = "static" },
                new Resource { Uri = "alpha://dynamic", Name = "dynamic" },
                new Resource { Uri = "alpha://dynamic", Name = "dynamic" }
            });
        }
    }

    public class StrayContributor : IAbpMcpResourceListContributor
    {
        public Task<IReadOnlyCollection<Resource>?> ListAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyCollection<Resource>?>(new[] { new Resource { Uri = "beta://x", Name = "x" } });
        }
    }

    public class EmptyContributor : IAbpMcpResourceListContributor
    {
        public Task<IReadOnlyCollection<Resource>?> ListAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyCollection<Resource>?>(null);
        }
    }
}
