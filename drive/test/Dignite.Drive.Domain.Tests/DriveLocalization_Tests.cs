using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dignite.Drive.Localization;
using Microsoft.Extensions.Localization;
using Shouldly;
using Volo.Abp.Localization;
using Volo.Abp.Modularity;
using Volo.Abp.VirtualFileSystem;
using Xunit;

namespace Dignite.Drive;

/// <summary>
/// The texts the file input (<c>drive-file-input</c> in <c>@dignite/ng.drive</c>) shows are in every culture of
/// <c>DriveResource</c>, with the English text's placeholders, and ABP resolves them.
/// </summary>
public abstract class DriveLocalization_Tests<TStartupModule> : DriveDomainTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private const string Folder = "/Dignite/Drive/Localization/Drive";

    private static readonly string[] FileInputTexts =
    {
        "Upload", "RemoveFile", "FileTooLarge", "FileTypeNotAllowed", "UploadFailed", "NoFileSelected"
    };

    [Fact]
    public void Every_culture_has_the_file_input_texts_with_the_English_placeholders()
    {
        var files = ReadTexts();
        files.Count.ShouldBeGreaterThan(1);

        var english = files["en"];
        foreach (var (culture, texts) in files)
        {
            foreach (var key in FileInputTexts)
            {
                texts.ShouldContainKey(key, $"{culture} has no text for {key}.");
                texts[key].ShouldNotBeNullOrWhiteSpace($"{culture}: {key}");
                Placeholders(texts[key]).ShouldBe(Placeholders(english[key]), $"{culture}: {key}");
            }
        }
    }

    [Theory]
    [InlineData("de", "Upload", "Hochladen")]
    [InlineData("zh-Hant", "NoFileSelected", "未選擇檔案。")]
    [InlineData("zh-Hans", "Upload", "上传")]
    public void ABP_resolves_a_file_input_text_in_its_culture(string culture, string key, string expected)
    {
        using (CultureHelper.Use(culture))
        {
            GetRequiredService<IStringLocalizer<DriveResource>>()[key].Value.ShouldBe(expected);
        }
    }

    /// <summary>The Drive's localization files as ABP reads them: from the virtual file system, by culture.</summary>
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
