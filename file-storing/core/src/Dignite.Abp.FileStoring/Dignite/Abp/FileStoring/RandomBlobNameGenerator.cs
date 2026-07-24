using System;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;

namespace Dignite.Abp.FileStoring;

public class RandomBlobNameGenerator : IBlobNameGenerator, ITransientDependency
{
    public static RandomBlobNameGenerator Instance { get; } = new RandomBlobNameGenerator();

    public virtual Task<string> Create()
    {
        return Task.FromResult(Guid.NewGuid().ToString("N"));
    }
}
