using Dignite.Abp.AspNetCore.Mcp;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Dignite.FileExplorer.Mcp;

public class FileExplorerMcpServerOptionsSetup_Tests
{
    [Fact]
    public void Should_Raise_The_Endpoint_Limit_To_Fit_A_Base64_Upload()
    {
        var serverOptions = new AbpMcpServerOptions { MaxRequestBodySize = 4 * 1024 * 1024 };

        Setup(maxUploadSize: 5 * 1024 * 1024).PostConfigure(null, serverOptions);

        // 5 MB of bytes is 6.67 MB of base64, plus the envelope allowance.
        serverOptions.MaxRequestBodySize.ShouldBe(4 * ((5L * 1024 * 1024 + 2) / 3) + FileExplorerMcpServerOptionsSetup.EnvelopeAllowance);
    }

    [Fact]
    public void Should_Never_Lower_A_Larger_Limit()
    {
        var serverOptions = new AbpMcpServerOptions { MaxRequestBodySize = 50 * 1024 * 1024 };

        Setup(maxUploadSize: 1024).PostConfigure(null, serverOptions);

        serverOptions.MaxRequestBodySize.ShouldBe(50 * 1024 * 1024);
    }

    [Fact]
    public void Should_Leave_A_Disabled_Endpoint_Limit_Alone()
    {
        var serverOptions = new AbpMcpServerOptions { MaxRequestBodySize = null };

        Setup(maxUploadSize: 5 * 1024 * 1024).PostConfigure(null, serverOptions);

        serverOptions.MaxRequestBodySize.ShouldBeNull();
    }

    private static FileExplorerMcpServerOptionsSetup Setup(long maxUploadSize)
    {
        return new FileExplorerMcpServerOptionsSetup(Options.Create(new FileExplorerMcpOptions { MaxUploadSize = maxUploadSize }));
    }
}
