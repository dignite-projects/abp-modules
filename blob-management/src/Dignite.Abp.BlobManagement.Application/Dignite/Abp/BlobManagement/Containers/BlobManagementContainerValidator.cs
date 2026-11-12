using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.BlobStoring;
using Volo.Abp.DependencyInjection;
using Volo.Abp.OpenIddict.Applications;

namespace Dignite.Abp.BlobManagement.Containers;

/// <summary>
/// Checks the Blob Management containers once the host has configured them, when the application starts. A configuration that
/// cannot work fails the start; one that works but is probably not what was meant is a warning:
/// <list type="bullet">
/// <item>The default container declared a Blob Management container (every name would be one): fails.</item>
/// <item>A system container with no allowed client: no remote caller can use it (in-process callers can).</item>
/// <item>An allowed client id the authorization server does not know - checked only where ABP's
/// <see cref="IApplicationFinder"/> is registered (a host with <c>Volo.Abp.OpenIddict.Domain</c>, that is a Blob Management
/// installed next to its authorization server); a standalone Blob Management service cannot look clients up and skips it.</item>
/// </list>
/// </summary>
public class BlobManagementContainerValidator : ITransientDependency
{
    public ILogger<BlobManagementContainerValidator> Logger { get; set; } = NullLogger<BlobManagementContainerValidator>.Instance;

    protected AbpBlobStoringOptions BlobStoringOptions { get; }

    protected IServiceProvider ServiceProvider { get; }

    public BlobManagementContainerValidator(IOptions<AbpBlobStoringOptions> blobStoringOptions, IServiceProvider serviceProvider)
    {
        BlobStoringOptions = blobStoringOptions.Value;
        ServiceProvider = serviceProvider;
    }

    /// <summary>Logs and returns the warnings; throws <see cref="AbpException"/> for a configuration that cannot work.</summary>
    public virtual async Task<List<string>> ValidateAsync()
    {
        var defaultContainerName = BlobContainerNameAttribute.GetContainerName<DefaultContainer>();
        if (BlobStoringOptions.Containers.GetConfiguration<DefaultContainer>().GetBlobManagementContainerConfiguration().IsBlobManagementContainer)
        {
            throw new AbpException(
                "The default BLOB container is declared a BlobManagement container, which makes every container name one. Call UseBlobManagementContainer on named containers only.");
        }

        var warnings = new List<string>();
        var clientIds = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        BlobStoringOptions.Containers.ConfigureAll((name, configuration) =>
        {
            var drive = configuration.GetBlobManagementContainerConfiguration();
            if (name == defaultContainerName || !drive.IsBlobManagementContainer || drive.Kind != BlobManagementContainerKind.System)
            {
                return;
            }

            if (drive.AllowedClientIds.Length == 0)
            {
                warnings.Add($"The BlobManagement system container '{name}' allows no client, so no remote caller can use it; only callers in this process can.");
            }

            foreach (var clientId in drive.AllowedClientIds)
            {
                if (!clientIds.TryGetValue(clientId, out var containers))
                {
                    clientIds[clientId] = containers = new List<string>();
                }

                containers.Add(name);
            }
        });

        warnings.AddRange(await CheckClientIdsAsync(clientIds));

        foreach (var warning in warnings)
        {
            Logger.LogWarning(warning);
        }

        return warnings;
    }

    protected virtual async Task<List<string>> CheckClientIdsAsync(Dictionary<string, List<string>> clientIds)
    {
        var warnings = new List<string>();
        if (clientIds.Count == 0)
        {
            return warnings;
        }

        var finder = ServiceProvider.GetService<IApplicationFinder>();
        if (finder == null)
        {
            Logger.LogInformation(
                "The BlobManagement's allowed client ids are not checked: this host cannot look clients up (no IApplicationFinder, which comes with Volo.Abp.OpenIddict.Domain).");
            return warnings;
        }

        foreach (var (clientId, containers) in clientIds)
        {
            try
            {
                if (!await ExistsAsync(finder, clientId))
                {
                    warnings.Add(
                        $"The client '{clientId}' allowed in the BlobManagement container(s) {string.Join(", ", containers.Select(c => $"'{c}'"))} is not known to the authorization server.");
                }
            }
            catch (Exception exception)
            {
                warnings.Add($"The BlobManagement could not check the allowed client '{clientId}': {exception.Message}");
            }
        }

        return warnings;
    }

    /// <summary>
    /// Whether <paramref name="clientId"/> exists. <see cref="IApplicationFinder.SearchAsync"/> matches by substring, ten
    /// a page, so the pages are read until an exact match or the end.
    /// </summary>
    protected virtual async Task<bool> ExistsAsync(IApplicationFinder finder, string clientId)
    {
        const int maxPages = 100;
        for (var page = 1; page <= maxPages; page++)
        {
            var results = await finder.SearchAsync(clientId, page);
            if (results.Any(result => string.Equals(result.ClientId, clientId, StringComparison.Ordinal)))
            {
                return true;
            }

            if (results.Count == 0)
            {
                return false;
            }
        }

        return false;
    }
}
