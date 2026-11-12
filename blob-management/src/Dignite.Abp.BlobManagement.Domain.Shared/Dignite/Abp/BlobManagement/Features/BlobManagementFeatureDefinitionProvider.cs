using Dignite.Abp.BlobManagement.Localization;
using Volo.Abp.Features;
using Volo.Abp.Localization;
using Volo.Abp.Validation.StringValues;

namespace Dignite.Abp.BlobManagement.Features;

public class BlobManagementFeatureDefinitionProvider : FeatureDefinitionProvider
{
    public override void Define(IFeatureDefinitionContext context)
    {
        var group = context.AddGroup(BlobManagementFeatures.GroupName, L("Feature:BlobManagementGroup"));

        var enable = group.AddFeature(
            BlobManagementFeatures.Enable,
            "true",
            L("Feature:BlobManagementEnable"),
            L("Feature:BlobManagementEnableDescription"),
            new ToggleStringValueType());

        enable.CreateChild(
            BlobManagementFeatures.StorageSize,
            "0",
            L("Feature:BlobManagementStorageSize"),
            L("Feature:BlobManagementStorageSizeDescription"),
            new FreeTextStringValueType(new NumericValueValidator(0, int.MaxValue)));
    }

    private static LocalizableString L(string name)
    {
        return LocalizableString.Create<BlobManagementResource>(name);
    }
}
