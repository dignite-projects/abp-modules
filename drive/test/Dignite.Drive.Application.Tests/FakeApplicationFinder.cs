using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.OpenIddict.Applications;

namespace Dignite.Drive;

/// <summary>
/// The authorization server's clients, as ABP's <see cref="IApplicationFinder"/> reports them: a substring search, ten a
/// page. Registered only by the tests that need a host which can look clients up.
/// </summary>
public class FakeApplicationFinder : IApplicationFinder
{
    private readonly string[] _clientIds;

    public FakeApplicationFinder(params string[] clientIds)
    {
        _clientIds = clientIds;
    }

    public Task<List<ApplicationFinderResult>> SearchAsync(string filter, int page = 1)
    {
        return Task.FromResult(_clientIds
            .Where(clientId => clientId.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .Skip((page - 1) * 10)
            .Take(10)
            .Select(clientId => new ApplicationFinderResult { Id = Guid.NewGuid(), ClientId = clientId })
            .ToList());
    }
}
