using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Abp.AspNetCore.Mcp.Errors;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Validation;

namespace Dignite.Abp.AspNetCore.Mcp;

[McpServerToolType]
public class TestTools : ITransientDependency
{
    [McpServerTool(Name = "test_echo", ReadOnly = true)]
    [Description("Returns its input.")]
    public virtual string Echo(string text) => text;

    [McpServerTool(Name = "test_secret", ReadOnly = true)]
    [Authorize(TestPermissionDefinitionProvider.Secret)]
    public virtual string Secret() => "secret";

    [McpServerTool(Name = "test_greet", ReadOnly = true)]
    public virtual string Greet() => "base";

    [McpServerTool(Name = "test_fail_validation")]
    public virtual string FailValidation()
    {
        throw new AbpValidationException(
            "The arguments are invalid.",
            new List<ValidationResult> { new("Slug is required.", new[] { "slug" }) });
    }

    [McpServerTool(Name = "test_fail_not_found")]
    public virtual string FailNotFound()
    {
        throw new TestNameNotFoundException("widget");
    }

    [McpServerTool(Name = "test_fail_mcp")]
    public virtual string FailWithMcpException()
    {
        throw new McpException("Written for the caller.");
    }

    [McpServerTool(Name = "test_fail_protocol")]
    public virtual string FailWithProtocolException()
    {
        throw new McpProtocolException("Not a tool outcome.", McpErrorCode.InvalidParams);
    }
}

/// <summary>
/// Replaces <see cref="TestTools"/> the ABP way. The SDK's own registration would construct
/// <see cref="TestTools"/> directly and never run this override.
/// </summary>
[Dependency(ReplaceServices = true)]
[ExposeServices(typeof(TestTools))]
public class ReplacedTestTools : TestTools
{
    public override string Greet() => "replaced";
}

[McpServerResourceType]
public class TestResources : ITransientDependency
{
    [McpServerResource(UriTemplate = "test://info", Name = "test_info", MimeType = "text/plain")]
    public virtual string Info() => "info";
}

/// <summary>Lists one resource per call that only exists at runtime.</summary>
public class TestResourceListContributor : IAbpMcpResourceListContributor, ITransientDependency
{
    public Task<IReadOnlyCollection<Resource>?> ListAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyCollection<Resource>?>(new[]
        {
            new Resource { Uri = "test://dynamic/1", Name = "test_dynamic_1" }
        });
    }
}

/// <summary>
/// A user-friendly exception (so its message reaches the model) that is a not-found, not a business-rule
/// conflict - the case <see cref="IHasMcpToolErrorKind"/> exists for.
/// </summary>
public class TestNameNotFoundException : UserFriendlyException, IHasMcpToolErrorKind
{
    public TestNameNotFoundException(string name)
        : base($"There is no widget named '{name}'.", code: "Test:001")
    {
    }

    public string McpToolErrorKind => McpToolErrorKinds.NotFound;
}
