using Microsoft.AspNetCore.Http.Metadata;

namespace Dignite.Abp.AspNetCore.Mcp;

/// <summary>
/// The MCP endpoint's request-body limit (<see cref="AbpMcpServerOptions.MaxRequestBodySize"/>), as the
/// endpoint metadata ASP.NET Core's routing middleware applies to <c>IHttpMaxRequestBodySizeFeature</c>
/// before the body is read.
/// </summary>
public sealed class AbpMcpRequestSizeLimitMetadata : IRequestSizeLimitMetadata
{
    public AbpMcpRequestSizeLimitMetadata(long maxRequestBodySize)
    {
        MaxRequestBodySize = maxRequestBodySize;
    }

    public long? MaxRequestBodySize { get; }
}
