using System;
using System.Reflection;

namespace Dignite.Abp.AspNetCore.Mcp;

public enum AbpMcpPrimitiveKind
{
    Tool,
    Resource,
    Prompt
}

/// <summary>
/// One tool, resource or prompt method a module registered - enough to name its owner in an error.
/// </summary>
public class AbpMcpPrimitiveRegistration
{
    public AbpMcpPrimitiveRegistration(AbpMcpModule module, AbpMcpPrimitiveKind kind, Type declaringType, MethodInfo method)
    {
        Module = module;
        Kind = kind;
        DeclaringType = declaringType;
        Method = method;
    }

    public AbpMcpModule Module { get; }

    public AbpMcpPrimitiveKind Kind { get; }

    public Type DeclaringType { get; }

    public MethodInfo Method { get; }

    public override string ToString()
    {
        return $"{DeclaringType.FullName}.{Method.Name} (MCP module '{Module.Name}')";
    }
}
