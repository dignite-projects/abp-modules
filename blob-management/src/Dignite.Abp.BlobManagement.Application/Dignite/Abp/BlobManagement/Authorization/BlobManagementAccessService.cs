using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using System.Threading.Tasks;
using Dignite.Abp.BlobManagement.Permissions;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Authorization.Permissions.Resources;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Security.Claims;

namespace Dignite.Abp.BlobManagement.Authorization;

/// <summary>
/// Decides what a user may do at a place of a container - its top, or a directory: the one place Blob Management's
/// authorization is worked out, for the application services.
/// <para>
/// A place has a <b>scope</b>: the nearest restricted directory at or above it
/// (<see cref="DirectoryDescriptor.IsRestricted"/>), else the container. The caller views the place with the
/// management permission, or with the view permission granted on its scope. With the files of the place the caller may
/// do what the module-wide permissions and the container's grants allow, in any scope; with its directories, what the
/// module-wide permissions and the scope's grants allow.
/// </para>
/// <para>
/// ABP's resource-permission checker answers one question: is this permission granted on this one resource. It is
/// asked here per request, not per item: once for the module-wide permissions, once for the container, and once for
/// each restricted directory that has to be told apart - the scope of the place, and the restricted directories of a
/// level being listed. Every file and every unrestricted directory of a place takes the place's answer. That is what
/// the restriction being a column is for: whether a directory has grants would itself be a question per directory.
/// </para>
/// <para>
/// Only users get access: a principal without a user id - a client-credentials token - is allowed nothing, as the file
/// API serves users only.
/// </para>
/// </summary>
public class BlobManagementAccessService : ITransientDependency
{
    protected static readonly string[] ModuleWidePermissions =
    {
        BlobManagementPermissions.Containers.Default,
        BlobManagementPermissions.Containers.UploadFiles,
        BlobManagementPermissions.Containers.UpdateFiles,
        BlobManagementPermissions.Containers.DeleteFiles,
        BlobManagementPermissions.Containers.CreateDirectories,
        BlobManagementPermissions.Containers.UpdateDirectories,
        BlobManagementPermissions.Containers.DeleteDirectories
    };

    protected static readonly string[] ContainerPermissions =
    {
        BlobManagementPermissions.Container.View,
        BlobManagementPermissions.Container.UploadFiles,
        BlobManagementPermissions.Container.UpdateFiles,
        BlobManagementPermissions.Container.DeleteFiles,
        BlobManagementPermissions.Container.CreateDirectories,
        BlobManagementPermissions.Container.UpdateDirectories,
        BlobManagementPermissions.Container.DeleteDirectories
    };

    protected static readonly string[] DirectoryPermissions =
    {
        BlobManagementPermissions.Directory.View,
        BlobManagementPermissions.Directory.CreateDirectories,
        BlobManagementPermissions.Directory.UpdateDirectories,
        BlobManagementPermissions.Directory.DeleteDirectories
    };

    protected IPermissionChecker PermissionChecker { get; }

    protected IResourcePermissionChecker ResourcePermissionChecker { get; }

    protected ICurrentPrincipalAccessor PrincipalAccessor { get; }

    protected IDirectoryDescriptorRepository DirectoryRepository { get; }

    public BlobManagementAccessService(
        IPermissionChecker permissionChecker,
        IResourcePermissionChecker resourcePermissionChecker,
        ICurrentPrincipalAccessor principalAccessor,
        IDirectoryDescriptorRepository directoryRepository)
    {
        PermissionChecker = permissionChecker;
        ResourcePermissionChecker = resourcePermissionChecker;
        PrincipalAccessor = principalAccessor;
        DirectoryRepository = directoryRepository;
    }

    /// <summary>
    /// Starts answering for <paramref name="principal"/> (default: the current one): reads the module-wide permissions,
    /// once. The context is then asked about the places of any container.
    /// </summary>
    public virtual async Task<BlobManagementAccessContext> BeginAsync(ClaimsPrincipal? principal = null)
    {
        principal ??= PrincipalAccessor.Principal;
        var context = new BlobManagementAccessContext(principal, principal?.FindUserId());
        if (context.UserId == null)
        {
            return context;
        }

        var result = await PermissionChecker.IsGrantedAsync(principal, ModuleWidePermissions);
        foreach (var permission in result.Result.Where(r => r.Value == PermissionGrantResult.Granted))
        {
            context.ModuleWide.Add(permission.Key);
        }

        return context;
    }

    /// <summary>
    /// Whether the caller has anything to do in <paramref name="containerName"/>: the management permission, or any
    /// grant on the container.
    /// </summary>
    public virtual async Task<bool> HasContainerAsync(BlobManagementAccessContext context, string containerName)
    {
        if (context.UserId == null)
        {
            return false;
        }

        return context.ModuleWide.Contains(BlobManagementPermissions.Containers.Default) ||
               (await GetContainerGrantsAsync(context, containerName)).Count > 0;
    }

    /// <summary>What the caller may do at the top of <paramref name="containerName"/>.</summary>
    public virtual Task<BlobManagementAccess> GetAccessAsync(BlobManagementAccessContext context, string containerName)
    {
        return GetAccessAsync(context, containerName, directory: null);
    }

    /// <summary>
    /// What the caller may do in <paramref name="directory"/> of <paramref name="containerName"/> (<c>null</c>: at the
    /// top of the container).
    /// </summary>
    public virtual async Task<BlobManagementAccess> GetAccessAsync(
        BlobManagementAccessContext context,
        string containerName,
        DirectoryDescriptor? directory)
    {
        if (context.UserId == null)
        {
            return BlobManagementAccess.None;
        }

        return await GetScopeAccessAsync(context, containerName, await FindScopeAsync(context, directory));
    }

    /// <summary>
    /// What the caller may do in each of <paramref name="children"/>, the directories of one level, whose own place has
    /// <paramref name="parentAccess"/>: a restricted child is its own scope, every other shares its parent's.
    /// </summary>
    public virtual async Task<Dictionary<Guid, BlobManagementAccess>> GetChildrenAccessAsync(
        BlobManagementAccessContext context,
        string containerName,
        BlobManagementAccess parentAccess,
        IReadOnlyCollection<DirectoryDescriptor> children)
    {
        var result = new Dictionary<Guid, BlobManagementAccess>();
        foreach (var child in children)
        {
            result[child.Id] = child.IsRestricted && context.UserId != null
                ? await GetScopeAccessAsync(context, containerName, child)
                : parentAccess;
        }

        return result;
    }

    /// <summary>
    /// The scope of <paramref name="directory"/>: itself when it is restricted, else the nearest restricted directory
    /// above it - its ancestors are the ids of its path, read in one query - else <c>null</c>, the container.
    /// </summary>
    protected virtual async Task<DirectoryDescriptor?> FindScopeAsync(
        BlobManagementAccessContext context,
        DirectoryDescriptor? directory)
    {
        if (directory == null)
        {
            return null;
        }

        if (directory.IsRestricted)
        {
            return directory;
        }

        if (context.Scopes.TryGetValue(directory.Id, out var known))
        {
            return known;
        }

        DirectoryDescriptor? scope = null;
        var ancestorIds = directory.GetAncestorIds();
        if (ancestorIds.Count > 0)
        {
            var ancestors = (await DirectoryRepository.GetListByIdsAsync(ancestorIds)).ToDictionary(d => d.Id);
            scope = ancestorIds
                .Where(ancestors.ContainsKey)
                .Select(id => ancestors[id])
                .FirstOrDefault(d => d.IsRestricted);
        }

        context.Scopes[directory.Id] = scope;
        return scope;
    }

    protected virtual async Task<BlobManagementAccess> GetScopeAccessAsync(
        BlobManagementAccessContext context,
        string containerName,
        DirectoryDescriptor? scope)
    {
        var moduleWide = context.ModuleWide;
        var container = await GetContainerGrantsAsync(context, containerName);

        bool view;
        HashSet<string> directories;
        string create;
        string update;
        string delete;
        if (scope == null)
        {
            view = container.Contains(BlobManagementPermissions.Container.View);
            directories = container;
            create = BlobManagementPermissions.Container.CreateDirectories;
            update = BlobManagementPermissions.Container.UpdateDirectories;
            delete = BlobManagementPermissions.Container.DeleteDirectories;
        }
        else
        {
            directories = await GetGrantsAsync(
                context, BlobManagementResourceNames.Directory, scope.Id.ToString(), DirectoryPermissions);
            view = directories.Contains(BlobManagementPermissions.Directory.View);
            create = BlobManagementPermissions.Directory.CreateDirectories;
            update = BlobManagementPermissions.Directory.UpdateDirectories;
            delete = BlobManagementPermissions.Directory.DeleteDirectories;
        }

        return new BlobManagementAccess(
            view: view || moduleWide.Contains(BlobManagementPermissions.Containers.Default),
            uploadFiles: moduleWide.Contains(BlobManagementPermissions.Containers.UploadFiles) ||
                         container.Contains(BlobManagementPermissions.Container.UploadFiles),
            updateFiles: moduleWide.Contains(BlobManagementPermissions.Containers.UpdateFiles) ||
                         container.Contains(BlobManagementPermissions.Container.UpdateFiles),
            deleteFiles: moduleWide.Contains(BlobManagementPermissions.Containers.DeleteFiles) ||
                         container.Contains(BlobManagementPermissions.Container.DeleteFiles),
            createDirectories: moduleWide.Contains(BlobManagementPermissions.Containers.CreateDirectories) ||
                               directories.Contains(create),
            updateDirectories: moduleWide.Contains(BlobManagementPermissions.Containers.UpdateDirectories) ||
                               directories.Contains(update),
            deleteDirectories: moduleWide.Contains(BlobManagementPermissions.Containers.DeleteDirectories) ||
                               directories.Contains(delete));
    }

    protected virtual Task<HashSet<string>> GetContainerGrantsAsync(BlobManagementAccessContext context, string containerName)
    {
        return GetGrantsAsync(context, BlobManagementResourceNames.Container, containerName, ContainerPermissions);
    }

    /// <summary>The permissions of <paramref name="names"/> granted on one resource, asked once per context.</summary>
    protected virtual async Task<HashSet<string>> GetGrantsAsync(
        BlobManagementAccessContext context,
        string resourceName,
        string resourceKey,
        string[] names)
    {
        var cacheKey = resourceName + "|" + resourceKey;
        if (context.Grants.TryGetValue(cacheKey, out var granted))
        {
            return granted;
        }

        var result = await ResourcePermissionChecker.IsGrantedAsync(context.Principal, names, resourceName, resourceKey);
        granted = new HashSet<string>(
            result.Result.Where(r => r.Value == PermissionGrantResult.Granted).Select(r => r.Key),
            StringComparer.Ordinal);
        context.Grants[cacheKey] = granted;
        return granted;
    }
}

/// <summary>What one request has read so far about its caller, so nothing is read twice.</summary>
public class BlobManagementAccessContext
{
    public BlobManagementAccessContext(ClaimsPrincipal? principal, Guid? userId)
    {
        Principal = principal;
        UserId = userId;
    }

    public ClaimsPrincipal? Principal { get; }

    public Guid? UserId { get; }

    /// <summary>The module-wide permissions the caller holds.</summary>
    public HashSet<string> ModuleWide { get; } = new(StringComparer.Ordinal);

    /// <summary>The grants read so far, by resource.</summary>
    public Dictionary<string, HashSet<string>> Grants { get; } = new(StringComparer.Ordinal);

    /// <summary>The scopes found so far for unrestricted directories, by directory id; <c>null</c> is the container.</summary>
    public Dictionary<Guid, DirectoryDescriptor?> Scopes { get; } = new();
}
