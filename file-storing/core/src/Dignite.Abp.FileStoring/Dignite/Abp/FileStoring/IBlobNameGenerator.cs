using System.Threading.Tasks;

namespace Dignite.Abp.FileStoring;

public interface IBlobNameGenerator
{
    Task<string> Create();
}
