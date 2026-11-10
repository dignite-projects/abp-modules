using Dignite.Drive.Localization;
using Volo.Abp.Features;
using Volo.Abp.Localization;
using Volo.Abp.Validation.StringValues;

namespace Dignite.Drive.Features;

public class DriveFeatureDefinitionProvider : FeatureDefinitionProvider
{
    public override void Define(IFeatureDefinitionContext context)
    {
        var group = context.AddGroup(DriveFeatures.GroupName, L("Feature:DriveGroup"));

        var enable = group.AddFeature(
            DriveFeatures.Enable,
            "true",
            L("Feature:DriveEnable"),
            L("Feature:DriveEnableDescription"),
            new ToggleStringValueType());

        enable.CreateChild(
            DriveFeatures.StorageSize,
            "0",
            L("Feature:DriveStorageSize"),
            L("Feature:DriveStorageSizeDescription"),
            new FreeTextStringValueType(new NumericValueValidator(0, int.MaxValue)));
    }

    private static LocalizableString L(string name)
    {
        return LocalizableString.Create<DriveResource>(name);
    }
}
