using Volo.Abp;
using Volo.Abp.MongoDB;

namespace Dignite.Drive.MongoDB;

public static class DriveMongoDbContextExtensions
{
    public static void ConfigureDrive(
        this IMongoModelBuilder builder)
    {
        Check.NotNull(builder, nameof(builder));
    }
}
