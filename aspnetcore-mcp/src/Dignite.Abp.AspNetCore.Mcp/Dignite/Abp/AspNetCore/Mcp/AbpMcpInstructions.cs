using System.Collections.Generic;
using System.Linq;

namespace Dignite.Abp.AspNetCore.Mcp;

internal static class AbpMcpInstructions
{
    /// <summary>
    /// The host's opening section, then each module's sections in the order given - registration order, which is
    /// module dependency order, so a module's text always follows the text of the modules it builds on.
    /// </summary>
    public static string? Compose(string? opening, IEnumerable<AbpMcpModule> modules)
    {
        var sections = new[] { opening }
            .Concat(modules.SelectMany(module => module.Instructions))
            .Where(section => !string.IsNullOrWhiteSpace(section))
            .Select(section => section!.Trim())
            .ToList();

        return sections.Count == 0 ? null : string.Join("\n\n", sections);
    }
}
