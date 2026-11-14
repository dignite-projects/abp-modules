using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dignite.Abp.BlobManagement.Localization;
using Microsoft.Extensions.Localization;
using Shouldly;
using Volo.Abp.Localization;
using Volo.Abp.Modularity;
using Volo.Abp.VirtualFileSystem;
using Xunit;

namespace Dignite.Abp.BlobManagement;

/// <summary>
/// Every text of <c>BlobManagementResource</c> - the management page's (<c>@dignite/ng.blob-management</c>), the errors,
/// the permission and feature names - is in every culture of it, with the English text's placeholders, and ABP
/// resolves them.
/// </summary>
public abstract class BlobManagementLocalization_Tests<TStartupModule> : BlobManagementDomainTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private const string Folder = "/Dignite/Abp/BlobManagement/Localization/BlobManagement";

    [Fact]
    public void Every_culture_has_every_English_text_with_its_placeholders()
    {
        var files = ReadTexts();
        files.Count.ShouldBeGreaterThan(1);

        var english = files["en"];
        foreach (var (culture, texts) in files)
        {
            texts.Keys.ShouldBe(english.Keys, ignoreOrder: true, $"{culture} has other texts than English.");
            foreach (var (key, text) in english)
            {
                texts[key].ShouldNotBeNullOrWhiteSpace($"{culture}: {key}");
                Placeholders(texts[key]).ShouldBe(Placeholders(text), $"{culture}: {key}");
            }
        }
    }

    [Theory]
    [InlineData("de", "Usage:Stored", "Gespeichert")]
    [InlineData("zh-Hant", "Usage:NoUser", "無使用者")]
    [InlineData("zh-Hans", "Usage", "用量")]
    [InlineData("fr", "Permission:ManagePermissions", "Gérer les autorisations sur les conteneurs")]
    [InlineData("ko", "Usage:StoredOfAllocation", "{1} 중 {0} 저장됨")]
    [InlineData("es", "Feature:BlobManagementStorageSize", "Almacenamiento (MB)")]
    [InlineData("zh-Hans", "Permission:CreateDirectories", "创建目录")]
    [InlineData("de", "Usage:Containers", "Container")]
    public void ABP_resolves_a_text_in_its_culture(string culture, string key, string expected)
    {
        using (CultureHelper.Use(culture))
        {
            GetRequiredService<IStringLocalizer<BlobManagementResource>>()[key].Value.ShouldBe(expected);
        }
    }

    /// <summary>Blob Management's localization files as ABP reads them: from the virtual file system, by culture.</summary>
    private Dictionary<string, Dictionary<string, string>> ReadTexts()
    {
        var result = new Dictionary<string, Dictionary<string, string>>();
        foreach (var file in GetRequiredService<IVirtualFileProvider>().GetDirectoryContents(Folder)
                     .Where(file => file.Name.EndsWith(".json")))
        {
            using var stream = file.CreateReadStream();
            using var document = JsonDocument.Parse(stream);
            var culture = document.RootElement.GetProperty("culture").GetString()!;
            Path.GetFileNameWithoutExtension(file.Name).ShouldBe(culture);
            result[culture] = document.RootElement.GetProperty("texts").EnumerateObject()
                .ToDictionary(text => text.Name, text => text.Value.GetString()!);
        }

        return result;
    }

    private static string[] Placeholders(string text)
    {
        return Regex.Matches(text, @"\{\w+\}").Select(match => match.Value).OrderBy(value => value).ToArray();
    }
}
