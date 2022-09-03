﻿using Dignite.FileExplorer.Files;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Modularity;

namespace Dignite.FileExplorer.EntityFrameworkCore;

[DependsOn(
    typeof(FileExplorerDomainModule),
    typeof(AbpEntityFrameworkCoreModule)
)]
public class FileExplorerEntityFrameworkCoreModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddAbpDbContext<FileExplorerDbContext>(options =>
        {
            options.AddRepository<FileDescriptor, EfCoreFileDescriptorRepository>();
        });
    }
}