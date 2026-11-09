using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenIddict.Abstractions;
using OpenIddict.Core;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.OpenIddict.Applications;
using Volo.Abp.Uow;

namespace Dignite.Abp.OpenIddict.ClientIdMetadataDocuments;

/// <summary>
/// Deletes CIMD registrations that have expired and that no authorization or token refers to. A client in use keeps
/// its registration (it is refreshed on its next authorization request); one that is gone - or was only ever a
/// document someone published to get a row into the table - does not.
/// </summary>
internal class ClientIdMetadataDocumentCleanupService : ITransientDependency
{
    private const int PageSize = 500;

    private readonly IOpenIddictApplicationRepository _applicationRepository;
    private readonly IOpenIddictApplicationCache<OpenIddictApplicationModel> _applicationCache;
    private readonly IOpenIddictAuthorizationManager _authorizationManager;
    private readonly IOpenIddictTokenManager _tokenManager;
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly ICurrentTenant _currentTenant;

    public ILogger<ClientIdMetadataDocumentCleanupService> Logger { get; set; } = NullLogger<ClientIdMetadataDocumentCleanupService>.Instance;

    public ClientIdMetadataDocumentCleanupService(
        IOpenIddictApplicationRepository applicationRepository,
        IOpenIddictApplicationCache<OpenIddictApplicationModel> applicationCache,
        IOpenIddictAuthorizationManager authorizationManager,
        IOpenIddictTokenManager tokenManager,
        IUnitOfWorkManager unitOfWorkManager,
        ICurrentTenant currentTenant)
    {
        _applicationRepository = applicationRepository;
        _applicationCache = applicationCache;
        _authorizationManager = authorizationManager;
        _tokenManager = tokenManager;
        _unitOfWorkManager = unitOfWorkManager;
        _currentTenant = currentTenant;
    }

    /// <returns>How many registrations were deleted.</returns>
    public virtual async Task<int> CleanAsync(CancellationToken cancellationToken = default)
    {
        using (_currentTenant.Change(null))
        {
            var candidates = await FindCandidatesAsync(cancellationToken);

            var deleted = 0;
            foreach (var id in candidates)
            {
                try
                {
                    if (await DeleteIfStillUnusedAsync(id, cancellationToken))
                    {
                        deleted++;
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Typically refreshed or given a token between the two reads; the next run looks again.
                    Logger.LogWarning(ex, "Could not delete expired CIMD registration {ApplicationId}.", id);
                }
            }

            if (deleted > 0)
            {
                Logger.LogInformation("Deleted {Count} expired CIMD registration(s).", deleted);
            }

            return deleted;
        }
    }

    private async Task<List<Guid>> FindCandidatesAsync(CancellationToken cancellationToken)
    {
        var candidates = new List<Guid>();
        var now = DateTimeOffset.UtcNow;

        using var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: false);
        for (var skip = 0; ; skip += PageSize)
        {
            // The repository's filter is "ClientId contains"; only an https client_id can be a CIMD registration.
            var page = await _applicationRepository.GetListAsync(
                nameof(OpenIddictApplication.ClientId), skip, PageSize, "https://", cancellationToken);

            foreach (var application in page)
            {
                if (IsExpiredRegistration(application, now) && !await IsReferencedAsync(application.Id, cancellationToken))
                {
                    candidates.Add(application.Id);
                }
            }

            if (page.Count < PageSize)
            {
                break;
            }
        }

        await uow.CompleteAsync(cancellationToken);
        return candidates;
    }

    private async Task<bool> DeleteIfStillUnusedAsync(Guid id, CancellationToken cancellationToken)
    {
        using var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: true);

        var application = await _applicationRepository.FindAsync(id, cancellationToken: cancellationToken);
        if (application == null
            || !IsExpiredRegistration(application, DateTimeOffset.UtcNow)
            || await IsReferencedAsync(id, cancellationToken))
        {
            await uow.CompleteAsync(cancellationToken);
            return false;
        }

        // Hard delete: a soft-deleted row would stay in the table, which is what this cleanup is for.
        await _applicationRepository.HardDeleteAsync(application, autoSave: true, cancellationToken: cancellationToken);
        await uow.CompleteAsync(cancellationToken);

        // ABP caches applications in the distributed cache (AbpOpenIddictApplicationCache) and nothing evicts an entry
        // when a row is deleted behind the manager's back; a cached copy would keep the client "registered". Evicted
        // after the commit, so a read between the two cannot cache the row again.
        await _applicationCache.RemoveAsync(application.ToModel(), cancellationToken);
        return true;
    }

    private static bool IsExpiredRegistration(OpenIddictApplication application, DateTimeOffset now) =>
        ClientIdMetadataDocumentRules.IsClientIdUrl(application.ClientId)
        && ClientIdMetadataDocumentRegistration.TryGetExpiresAt(application.Properties, out var expiresAt)
        && expiresAt <= now;

    private async Task<bool> IsReferencedAsync(Guid applicationId, CancellationToken cancellationToken)
    {
        var identifier = applicationId.ToString();

        await foreach (var _ in _authorizationManager.FindByApplicationIdAsync(identifier, cancellationToken))
        {
            return true;
        }

        await foreach (var _ in _tokenManager.FindByApplicationIdAsync(identifier, cancellationToken))
        {
            return true;
        }

        return false;
    }
}
