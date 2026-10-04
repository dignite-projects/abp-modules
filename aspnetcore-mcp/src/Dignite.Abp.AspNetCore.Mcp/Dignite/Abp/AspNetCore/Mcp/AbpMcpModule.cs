using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using Volo.Abp;

namespace Dignite.Abp.AspNetCore.Mcp;

/// <summary>
/// One contributor to the MCP server and the namespace it owns.
/// <para>
/// <b>The namespace is what keeps contributors from colliding.</b> Every tool and prompt this module
/// registers must be named <c>{Name}_…</c>, and every resource URI must use the
/// <see cref="UriScheme"/> scheme. Uniqueness then holds by construction across modules written by
/// different people, instead of being discovered by whoever assembles a host - who could not fix it
/// anyway, since the names are compiled into the module's package and are referenced by its own
/// descriptions and instructions. <see cref="AbpMcpPrimitiveValidator"/> enforces all of this at startup.
/// </para>
/// </summary>
public class AbpMcpModule
{
    // snake_case: what MCP clients and model APIs accept everywhere. A dot is legal in an MCP tool name,
    // but several model APIs reject it in function names, so the prefix never contains one.
    private static readonly Regex NamePattern = new("^[a-z][a-z0-9]*(_[a-z0-9]+)*$", RegexOptions.CultureInvariant);

    // RFC 3986 scheme syntax, lower-cased. No underscore is allowed in a scheme, which is why the default
    // is the name with '_' turned into '-' (file_explorer -> file-explorer).
    private static readonly Regex UriSchemePattern = new("^[a-z][a-z0-9+.-]*$", RegexOptions.CultureInvariant);

    private readonly List<AbpMcpPrimitiveRegistration> _primitives = new();
    private readonly List<string> _instructions = new();
    private readonly List<Type> _resourceListContributors = new();
    private string _uriScheme;

    internal AbpMcpModule(string name)
    {
        Check.NotNullOrWhiteSpace(name, nameof(name));
        if (!NamePattern.IsMatch(name))
        {
            throw new AbpException(
                $"'{name}' is not a valid MCP module name. Use lower-case snake_case (for example " +
                "'file_explorer'): it becomes the prefix of every tool and prompt name the module registers.");
        }

        Name = name;
        _uriScheme = name.Replace('_', '-');
    }

    /// <summary>The module's name, and the prefix (followed by <c>_</c>) of its tool and prompt names.</summary>
    public string Name { get; }

    /// <summary>The prefix every tool and prompt name of this module must start with.</summary>
    public string NamePrefix => Name + "_";

    /// <summary>
    /// The URI scheme every resource of this module must use. Defaults to <see cref="Name"/> with
    /// <c>_</c> replaced by <c>-</c>.
    /// </summary>
    public string UriScheme
    {
        get => _uriScheme;
        internal set
        {
            Check.NotNullOrWhiteSpace(value, nameof(value));
            if (!UriSchemePattern.IsMatch(value))
            {
                throw new AbpException(
                    $"'{value}' is not a valid URI scheme for MCP module '{Name}'. Use lower-case letters, " +
                    "digits, '+', '-' or '.', starting with a letter (RFC 3986 - no underscore).");
            }

            _uriScheme = value;
        }
    }

    /// <summary>The tools, resources and prompts this module registered, in registration order.</summary>
    public IReadOnlyList<AbpMcpPrimitiveRegistration> Primitives => _primitives;

    /// <summary>This module's sections of the server instructions, in the order they were added.</summary>
    public IReadOnlyList<string> Instructions => _instructions;

    /// <summary>
    /// This module's <see cref="IAbpMcpResourceListContributor"/> types, in the order they were added.
    /// </summary>
    public IReadOnlyList<Type> ResourceListContributors => _resourceListContributors;

    internal AbpMcpPrimitiveRegistration AddPrimitive(AbpMcpPrimitiveKind kind, Type declaringType, MethodInfo method)
    {
        var registration = new AbpMcpPrimitiveRegistration(this, kind, declaringType, method);
        _primitives.Add(registration);
        return registration;
    }

    internal void AddInstructions(string instructions)
    {
        _instructions.Add(instructions);
    }

    internal void AddResourceListContributor(Type contributorType)
    {
        if (!_resourceListContributors.Contains(contributorType))
        {
            _resourceListContributors.Add(contributorType);
        }
    }
}
