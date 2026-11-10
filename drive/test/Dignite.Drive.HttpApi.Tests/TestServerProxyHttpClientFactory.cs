using System.Net.Http;
using Volo.Abp.AspNetCore.TestBase;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Http.Client.Proxying;

namespace Dignite.Drive;

/// <summary>
/// Sends the C# client proxies' requests into the test server, so the remote provider reaches the Drive running in
/// the same test host over real HTTP.
/// </summary>
[Dependency(ReplaceServices = true)]
public class TestServerProxyHttpClientFactory : IProxyHttpClientFactory, ITransientDependency
{
    private readonly ITestServerAccessor _testServerAccessor;

    public TestServerProxyHttpClientFactory(ITestServerAccessor testServerAccessor)
    {
        _testServerAccessor = testServerAccessor;
    }

    public HttpClient Create()
    {
        return _testServerAccessor.Server!.CreateClient();
    }

    public HttpClient Create(string name)
    {
        return Create();
    }
}
