using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Http.Client.Authentication;
using Volo.Abp.Security.Claims;

namespace Dignite.Drive;

/// <summary>
/// Who calls the Drive over HTTP, standing in for a bearer token: an OAuth client, optionally with a user. The remote
/// provider sends it through <see cref="TestCallerAuthenticator"/>, a raw request through <see cref="ClientHeader"/> and
/// <see cref="UserHeader"/>, and <see cref="TestCallerAuthenticationHandler"/> turns it into claims on the Drive's side.
/// </summary>
public static class TestCaller
{
    public const string ClientHeader = "X-Test-Client";

    public const string UserHeader = "X-Test-User";

    private static readonly AsyncLocal<(string? ClientId, Guid? UserId)> Current = new();

    public static string? ClientId => Current.Value.ClientId;

    public static Guid? UserId => Current.Value.UserId;

    /// <summary>Calls made inside the returned scope carry <paramref name="clientId"/> and <paramref name="userId"/>.</summary>
    public static IDisposable As(string? clientId, Guid? userId = null)
    {
        var previous = Current.Value;
        Current.Value = (clientId, userId);
        return new Restore(() => Current.Value = previous);
    }

    private sealed class Restore : IDisposable
    {
        private readonly Action _action;

        public Restore(Action action)
        {
            _action = action;
        }

        public void Dispose()
        {
            _action();
        }
    }
}

/// <summary>Adds the current <see cref="TestCaller"/> to every request the C# client proxies send.</summary>
[Dependency(ReplaceServices = true)]
[ExposeServices(typeof(IRemoteServiceHttpClientAuthenticator))]
public class TestCallerAuthenticator : IRemoteServiceHttpClientAuthenticator, ITransientDependency
{
    public Task Authenticate(RemoteServiceHttpClientAuthenticateContext context)
    {
        if (TestCaller.ClientId != null)
        {
            context.Request.Headers.Add(TestCaller.ClientHeader, TestCaller.ClientId);
        }

        if (TestCaller.UserId != null)
        {
            context.Request.Headers.Add(TestCaller.UserHeader, TestCaller.UserId.ToString());
        }

        return Task.CompletedTask;
    }
}

/// <summary>Authenticates a request carrying <see cref="TestCaller.ClientHeader"/> and/or <see cref="TestCaller.UserHeader"/>.</summary>
public class TestCallerAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "TestCaller";

    public TestCallerAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new List<Claim>();
        if (Request.Headers.TryGetValue(TestCaller.ClientHeader, out var clientId))
        {
            claims.Add(new Claim(AbpClaimTypes.ClientId, clientId.ToString()));
        }

        if (Request.Headers.TryGetValue(TestCaller.UserHeader, out var userId))
        {
            claims.Add(new Claim(AbpClaimTypes.UserId, userId.ToString()));
        }

        if (claims.Count == 0)
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
    }
}
