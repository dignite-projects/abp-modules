using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Threading;
using Volo.Abp.Uow;

namespace Dignite.FileExplorer.Directories;

public class DirectoryDescriptorAppService : FileExplorerAppService, IDirectoryDescriptorAppService
{
    private readonly DirectoryManager _directoryManager;
    private readonly IDirectoryDescriptorRepository _directoryRepository;

    private CancellationToken RequestCancellationToken =>
        LazyServiceProvider.LazyGetService<ICancellationTokenProvider>()?.Token ?? CancellationToken.None;

    public DirectoryDescriptorAppService(DirectoryManager directoryManager, IDirectoryDescriptorRepository directoryRepository)
    {
        _directoryManager = directoryManager;
        _directoryRepository = directoryRepository;
    }

    [Authorize]
    public async Task<DirectoryDescriptorDto> CreateAsync(CreateDirectoryInput input)
    {
        var cancellationToken = RequestCancellationToken;
        var userId = GetCurrentUserId();
        var resource = new DirectoryDescriptor(
            GuidGenerator.Create(),
            input.ContainerName,
            input.Name,
            input.ParentId,
            0,
            CurrentTenant.Id)
        {
            CreatorId = userId
        };
        await AuthorizationService.CheckAsync(resource, CommonOperations.Create);

        var entity = await _directoryManager.CreateAsync(
            userId,
            input.ContainerName,
            input.Name,
            input.ParentId,
            cancellationToken);
        return ObjectMapper.Map<DirectoryDescriptor, DirectoryDescriptorDto>(entity);
    }

    [Authorize]
    [UnitOfWork]
    public async Task DeleteAsync(Guid id)
    {
        var cancellationToken = RequestCancellationToken;
        var entity = await _directoryRepository.GetAsync(id, cancellationToken: cancellationToken);
        await AuthorizationService.CheckAsync(entity, CommonOperations.Delete);
        await _directoryManager.DeleteAsync(entity, cancellationToken);
    }

    [Authorize]
    public async Task<DirectoryDescriptorDto> GetAsync(Guid id)
    {
        var entity = await _directoryRepository.GetAsync(
            id,
            cancellationToken: RequestCancellationToken);
        await AuthorizationService.CheckAsync(entity, CommonOperations.Get);
        return
            ObjectMapper.Map<DirectoryDescriptor, DirectoryDescriptorDto>(entity);
    }

    [Authorize]
    public async Task<PagedResultDto<DirectoryDescriptorInfoDto>> GetListAsync(GetDirectoriesInput input)
    {
        var result = await _directoryRepository.GetAllByUserAsync(
            GetCurrentUserId(),
            input.ContainerName,
            RequestCancellationToken);
        var dtoList = ObjectMapper.Map<List<DirectoryDescriptor>, List<DirectoryDescriptorInfoDto>>(result);
        return new PagedResultDto<DirectoryDescriptorInfoDto>(dtoList.Count, dtoList.BuildTree());
    }

    [Authorize]
    public async Task<DirectoryDescriptorDto> MoveAsync(Guid id, MoveDirectoryInput input)
    {
        var cancellationToken = RequestCancellationToken;
        var entity = await _directoryRepository.GetAsync(id, false, cancellationToken);
        await AuthorizationService.CheckAsync(entity, CommonOperations.Update);
        if (input.ParentId.HasValue)
        {
            var parent = await _directoryRepository.GetAsync(input.ParentId.Value, false, cancellationToken);
            await AuthorizationService.CheckAsync(parent, CommonOperations.Update);
        }
        entity = await _directoryManager.MoveAsync(entity, input.ParentId, input.Order, cancellationToken);
        return ObjectMapper.Map<DirectoryDescriptor, DirectoryDescriptorDto>(entity);
    }

    /// <summary>
    /// Directories are per user. A caller authenticated without one - a client-credentials token - has no
    /// directories to list or own, and is refused with that reason instead of a null dereference.
    /// </summary>
    private Guid GetCurrentUserId()
    {
        return CurrentUser.Id ?? throw new AbpAuthorizationException(code: FileExplorerErrorCodes.Directories.UserRequired);
    }

    [Authorize]
    public async Task<DirectoryDescriptorDto> UpdateAsync(Guid id, UpdateDirectoryInput input)
    {
        var cancellationToken = RequestCancellationToken;
        var entity = await _directoryRepository.GetAsync(id, false, cancellationToken);
        await AuthorizationService.CheckAsync(entity, CommonOperations.Update);
        await _directoryManager.UpdateAsync(entity, input.Name, cancellationToken);
        return
            ObjectMapper.Map<DirectoryDescriptor, DirectoryDescriptorDto>(entity);
    }
}
