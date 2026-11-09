using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.OpenIddict.Applications;
using Volo.Abp.Uow;
using Xunit;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Dignite.Abp.OpenIddict.ClientIdMetadataDocuments;

public class ClientIdMetadataDocumentCleanup_Tests : ClientIdMetadataDocumentsIntegratedTestBase
{
    [Fact]
    public async Task Should_Hard_Delete_Expired_Registrations_That_Nothing_Refers_To()
    {
        var expired = await RegisterAsync("https://gone.example.com/metadata.json", DateTimeOffset.UtcNow.AddMinutes(-1));

        (await CleanAsync()).ShouldBe(1);

        (await FindClientAsync("https://gone.example.com/metadata.json")).ShouldBeNull();
        (await FindIncludingSoftDeletedAsync(expired)).ShouldBeNull();
    }

    [Fact]
    public async Task Should_Keep_Registrations_In_Use_Unexpired_Or_Not_Registered_From_A_Document()
    {
        var withToken = await RegisterAsync("https://token.example.com/metadata.json", DateTimeOffset.UtcNow.AddMinutes(-1));
        var withAuthorization = await RegisterAsync("https://consent.example.com/metadata.json", DateTimeOffset.UtcNow.AddMinutes(-1));
        await RegisterAsync("https://fresh.example.com/metadata.json", DateTimeOffset.UtcNow.AddMinutes(30));
        await CreateApplicationAsync(new OpenIddictApplicationDescriptor
        {
            ClientId = "https://partner.example.com/client",
            ClientType = ClientTypes.Public,
            Permissions = { Permissions.Endpoints.Authorization }
        });
        await CreateApplicationAsync(new OpenIddictApplicationDescriptor
        {
            ClientId = "MyApp_App",
            ClientType = ClientTypes.Public,
            Permissions = { Permissions.Endpoints.Authorization }
        });

        await WithScopeAsync(async services =>
        {
            await services.GetRequiredService<IOpenIddictTokenManager>().CreateAsync(new OpenIddictTokenDescriptor
            {
                ApplicationId = withToken.ToString(),
                Subject = "user",
                Status = Statuses.Valid,
                Type = TokenTypeHints.RefreshToken,
                CreationDate = DateTimeOffset.UtcNow,
                ExpirationDate = DateTimeOffset.UtcNow.AddDays(14)
            });
            await services.GetRequiredService<IOpenIddictAuthorizationManager>().CreateAsync(new OpenIddictAuthorizationDescriptor
            {
                ApplicationId = withAuthorization.ToString(),
                Subject = "user",
                Status = Statuses.Valid,
                Type = AuthorizationTypes.Permanent,
                CreationDate = DateTimeOffset.UtcNow
            });
        });

        (await CleanAsync()).ShouldBe(0);

        (await FindClientAsync("https://token.example.com/metadata.json")).ShouldNotBeNull();
        (await FindClientAsync("https://consent.example.com/metadata.json")).ShouldNotBeNull();
        (await FindClientAsync("https://fresh.example.com/metadata.json")).ShouldNotBeNull();
        (await FindClientAsync("https://partner.example.com/client")).ShouldNotBeNull();
        (await FindClientAsync("MyApp_App")).ShouldNotBeNull();
    }

    [Fact]
    public async Task A_Deleted_Registration_Should_Be_Registered_Again_On_The_Next_Authorization()
    {
        Fetcher.SetDocument(ClientId, Document());
        await EnsureAsync(ClientId);
        await SetExpiresAtAsync(DateTimeOffset.UtcNow.AddMinutes(-1));
        await CleanAsync();

        (await EnsureAsync(ClientId)).ShouldBeNull();

        (await FindClientAsync()).ShouldNotBeNull();
    }

    private Task<int> CleanAsync() =>
        WithScopeAsync(services => services.GetRequiredService<ClientIdMetadataDocumentCleanupService>().CleanAsync());

    private async Task<Guid> RegisterAsync(string clientId, DateTimeOffset expiresAt)
    {
        Fetcher.SetDocument(clientId, Document(clientId: clientId));
        (await EnsureAsync(clientId)).ShouldBeNull();
        await SetExpiresAtAsync(expiresAt, clientId);
        return Guid.Parse((await FindClientAsync(clientId))!.Id);
    }

    private Task<OpenIddictApplication?> FindIncludingSoftDeletedAsync(Guid id) =>
        WithScopeAsync(async services =>
        {
            using (services.GetRequiredService<IDataFilter>().Disable<ISoftDelete>())
            {
                return await services.GetRequiredService<IOpenIddictApplicationRepository>().FindAsync(id);
            }
        });

    private Task WithScopeAsync(Func<IServiceProvider, Task> action) =>
        WithScopeAsync(async services =>
        {
            await action(services);
            return true;
        });

    private async Task<T> WithScopeAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        using var scope = ServiceProvider.CreateScope();
        using var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
        var result = await action(scope.ServiceProvider);
        await uow.CompleteAsync();
        return result;
    }
}
