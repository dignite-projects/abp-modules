using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace Dignite.Abp.OpenIddict.ClientIdMetadataDocuments;

/// <summary>The startup check: a library cannot guess the host's API scope, so it refuses to start without one.</summary>
public class AbpOpenIddictClientIdMetadataDocumentsModule_Tests
{
    [Fact]
    public async Task Should_Refuse_To_Start_Without_Scopes()
    {
        var exception = await Should.ThrowAsync<AbpException>(() => InitializeAsync(_ => { }));

        exception.Message.ShouldContain(nameof(AbpClientIdMetadataDocumentOptions.Scopes));
    }

    [Fact]
    public async Task Should_Start_Without_Scopes_When_Disabled()
    {
        await InitializeAsync(options => options.IsEnabled = false);
    }

    private static async Task InitializeAsync(Action<AbpClientIdMetadataDocumentOptions> configure)
    {
        var services = new ServiceCollection();
        services.Configure(configure);
        await using var serviceProvider = services.BuildServiceProvider();

        await new AbpOpenIddictClientIdMetadataDocumentsModule()
            .OnApplicationInitializationAsync(new ApplicationInitializationContext(serviceProvider));
    }
}
