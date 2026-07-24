using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Modularity;
using Volo.Abp.Security.Claims;
using Volo.Abp.Testing;
using Volo.Abp.Uow;

namespace Dignite.NotificationCenter;

/// <summary>
/// Generic base for every NotificationCenter integration test. Concrete test classes never inherit
/// this directly — they inherit a provider-agnostic <c>*_Tests&lt;TStartupModule&gt;</c> which in turn
/// inherits this, and each provider test project binds <typeparamref name="TStartupModule"/> to its
/// own startup module (EF Core / MongoDB).
/// </summary>
public abstract class NotificationCenterTestBase<TStartupModule> : AbpIntegratedTest<TStartupModule>
    where TStartupModule : IAbpModule
{
    protected override void SetAbpApplicationCreationOptions(AbpApplicationCreationOptions options)
    {
        options.UseAutofac();
    }

    protected virtual async Task WithUnitOfWorkAsync(
        Func<Task> func,
        bool? isTransactional = null)
    {
        using var uow = GetRequiredService<IUnitOfWorkManager>().Begin(
            requiresNew: true,
            isTransactional: isTransactional ?? false);
        await func();
        await uow.CompleteAsync();
    }

    protected virtual IDisposable ChangeCurrentUser(Guid userId)
    {
        var identity = new ClaimsIdentity(new[] { new Claim(AbpClaimTypes.UserId, userId.ToString()) }, "Test");
        return GetRequiredService<ICurrentPrincipalAccessor>().Change(new ClaimsPrincipal(identity));
    }
}
