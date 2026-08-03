using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Dignite.Abp.FlexFields.FileExplorer.Web;

/// <summary>
/// Reads the value the Angular picker denormalizes at pick time (see
/// <c>FileExplorerControlComponent.onSelectedFileChange</c>) - an array of file descriptor objects
/// (id/containerName/blobName/name/mimeType/size/url, ...; see <c>FileDescriptorDto</c> in
/// <c>@dignite/ng.file-explorer</c>) - leniently: a fresh in-memory value is a
/// <see cref="IEnumerable"/> of dictionary-like objects, one that has round-tripped through JSON
/// storage is a <see cref="JsonElement"/> array of objects. Property names are read as stored -
/// <c>Dictionary&lt;string, object?&gt;</c> serializes its keys literally, so whatever casing the value
/// was written with (this project reads the same camelCase the Angular <c>FileDescriptorDto</c> uses)
/// round-trips unchanged; there is no reflection-based naming policy in between to second-guess.
/// </summary>
internal static class FileDescriptorValueReader
{
    public static IReadOnlyList<FileDescriptorView> ReadFiles(object? value)
    {
        return value switch
        {
            null => new List<FileDescriptorView>(),
            JsonElement { ValueKind: JsonValueKind.Array } array => array.EnumerateArray().Select(ReadJsonFile).ToList(),
            JsonElement { ValueKind: JsonValueKind.Object } single => new List<FileDescriptorView> { ReadJsonFile(single) },
            JsonElement => new List<FileDescriptorView>(),
            IDictionary<string, object?> singleDictionary => new List<FileDescriptorView> { ReadDictionaryFile(singleDictionary) },
            IEnumerable items => items
                .Cast<object?>()
                .Select(ReadObjectFile)
                .Where(file => file != null)
                .Select(file => file!)
                .ToList(),
            _ => new List<FileDescriptorView>()
        };
    }

    private static FileDescriptorView? ReadObjectFile(object? item)
    {
        return item switch
        {
            JsonElement element => ReadJsonFile(element),
            IDictionary<string, object?> dictionary => ReadDictionaryFile(dictionary),
            _ => null
        };
    }

    private static FileDescriptorView ReadJsonFile(JsonElement element)
    {
        return new FileDescriptorView(
            Name: GetString(element, "name") ?? GetString(element, "blobName") ?? "file",
            Size: GetLong(element, "size"),
            MimeType: GetString(element, "mimeType"),
            Url: GetString(element, "url"));
    }

    private static FileDescriptorView ReadDictionaryFile(IDictionary<string, object?> dictionary)
    {
        return new FileDescriptorView(
            Name: GetString(dictionary, "name") ?? GetString(dictionary, "blobName") ?? "file",
            Size: GetLong(dictionary, "size"),
            MimeType: GetString(dictionary, "mimeType"),
            Url: GetString(dictionary, "url"));
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static long? GetLong(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.Number && property.TryGetInt64(out var value)
            ? value
            : null;
    }

    private static string? GetString(IDictionary<string, object?> dictionary, string propertyName)
    {
        return dictionary.TryGetValue(propertyName, out var value) ? value as string : null;
    }

    private static long? GetLong(IDictionary<string, object?> dictionary, string propertyName)
    {
        if (!dictionary.TryGetValue(propertyName, out var value) || value == null)
        {
            return null;
        }

        return value switch
        {
            long l => l,
            int i => i,
            _ => null
        };
    }
}

/// <summary>Just enough of a file descriptor for the default view to render - name, size, MIME type and
/// a link. Anything richer (thumbnails, upload date, ...) is a reason to override this view, not to
/// grow this shape.</summary>
internal record FileDescriptorView(string Name, long? Size, string? MimeType, string? Url);
