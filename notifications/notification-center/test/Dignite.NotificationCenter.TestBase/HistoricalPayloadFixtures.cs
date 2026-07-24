using System;
using System.IO;

namespace Dignite.NotificationCenter;

internal static class HistoricalPayloadFixtures
{
    public static string Read(string fileName)
    {
        using var stream = typeof(HistoricalPayloadFixtures).Assembly.GetManifestResourceStream(
            $"Fixtures.{fileName}")
            ?? throw new InvalidOperationException(
                $"Embedded historical notification fixture '{fileName}' was not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Trim();
    }
}
