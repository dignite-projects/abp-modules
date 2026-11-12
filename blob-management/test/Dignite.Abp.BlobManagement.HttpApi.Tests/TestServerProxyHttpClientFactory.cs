using System.Net.Http;
using Volo.Abp.AspNetCore.TestBase;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Http.Client.Proxying;

namespace Dignite.Abp.BlobManagement;

/// <summary>
/// Sends the C# client proxies' requests into the test server, so the remote provider reaches the Blob Management running in
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
