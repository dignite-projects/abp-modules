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

namespace Dignite.Drive.Containers;

/// <summary>
/// Checks the Drive containers once the host has configured them, when the application starts. A configuration that
/// cannot work fails the start; one that works but is probably not what was meant is a warning:
/// <list type="bullet">
/// <item>The default container declared a Drive container (every name would be one): fails.</item>
/// <item>A system container with no allowed client: no remote caller can use it (in-process callers can).</item>
/// <item>An allowed client id the authorization server does not know - checked only where ABP's
/// <see cref="IApplicationFinder"/> is registered (a host with <c>Volo.Abp.OpenIddict.Domain</c>, that is a Drive
/// installed next to its authorization server); a standalone Drive service cannot look clients up and skips it.</item>
/// </list>
/// </summary>
public class DriveContainerValidator : ITransientDependency
{
    public ILogger<DriveContainerValidator> Logger { get; set; } = NullLogger<DriveContainerValidator>.Instance;

    protected AbpBlobStoringOptions BlobStoringOptions { get; }

    protected IServiceProvider ServiceProvider { get; }

    public DriveContainerValidator(IOptions<AbpBlobStoringOptions> blobStoringOptions, IServiceProvider serviceProvider)
    {
        BlobStoringOptions = blobStoringOptions.Value;
        ServiceProvider = serviceProvider;
    }

    /// <summary>Logs and returns the warnings; throws <see cref="AbpException"/> for a configuration that cannot work.</summary>
    public virtual async Task<List<string>> ValidateAsync()
    {
        var defaultContainerName = BlobContainerNameAttribute.GetContainerName<DefaultContainer>();
        if (BlobStoringOptions.Containers.GetConfiguration<DefaultContainer>().GetDriveContainerConfiguration().IsDriveContainer)
        {
            throw new AbpException(
                "The default BLOB container is declared a Drive container, which makes every container name one. Call UseDriveContainer on named containers only.");
        }

        var warnings = new List<string>();
        var clientIds = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        BlobStoringOptions.Containers.ConfigureAll((name, configuration) =>
        {
            var drive = configuration.GetDriveContainerConfiguration();
            if (name == defaultContainerName || !drive.IsDriveContainer || drive.Kind != DriveContainerKind.System)
            {
                return;
            }

            if (drive.AllowedClientIds.Length == 0)
            {
                warnings.Add($"The Drive system container '{name}' allows no client, so no remote caller can use it; only callers in this process can.");
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
                "The Drive's allowed client ids are not checked: this host cannot look clients up (no IApplicationFinder, which comes with Volo.Abp.OpenIddict.Domain).");
            return warnings;
        }

        foreach (var (clientId, containers) in clientIds)
        {
            try
            {
                if (!await ExistsAsync(finder, clientId))
                {
                    warnings.Add(
                        $"The client '{clientId}' allowed in the Drive container(s) {string.Join(", ", containers.Select(c => $"'{c}'"))} is not known to the authorization server.");
                }
            }
            catch (Exception exception)
            {
                warnings.Add($"The Drive could not check the allowed client '{clientId}': {exception.Message}");
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
