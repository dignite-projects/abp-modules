using System;
using System.Collections.Generic;
using System.Text.Json;
using ModelContextProtocol.Server;
using Volo.Abp.DependencyInjection;

namespace Dignite.Abp.AspNetCore.Mcp;

/// <summary>
/// Tools whose parameters a call can get wrong in each way the binder fails: a missing argument, a value of
/// the wrong type, a value of the wrong format or outside an enum, and a nested value of the wrong shape -
/// and tools that throw the binder's own exceptions from their bodies, which are not the caller's mistake.
/// </summary>
[McpServerToolType]
public class TestArgumentTools : ITransientDependency
{
    [McpServerTool(Name = "test_sum", ReadOnly = true)]
    public virtual int Sum(int left, int right) => left + right;

    [McpServerTool(Name = "test_find", ReadOnly = true)]
    public virtual string Find(Guid id, TestPageStatus status = TestPageStatus.Draft) => $"{id}:{status}";

    [McpServerTool(Name = "test_book", ReadOnly = true)]
    public virtual int Book(TestBooking booking) => booking.Guests.Count;

    [McpServerTool(Name = "test_fail_own_argument_exception")]
    public virtual string FailWithOwnArgumentException()
    {
        throw new ArgumentException("The widget is not valid.", "widget");
    }

    /// <summary>
    /// The binder's exception, word for word, thrown by the tool itself: nothing about the call's arguments.
    /// </summary>
    [McpServerTool(Name = "test_fail_own_arguments_exception")]
    public virtual string FailWithOwnArgumentsException()
    {
        throw new ArgumentException("The arguments dictionary is missing a value.", "arguments");
    }

    /// <summary>A tool that reads JSON it did not get from its arguments, such as an upstream response.</summary>
    [McpServerTool(Name = "test_fail_own_json_exception")]
    public virtual int FailWithOwnJsonException()
    {
        return JsonSerializer.Deserialize<int>("\"not a number\"");
    }
}

public class TestBooking
{
    public required string Name { get; set; }

    public List<TestGuest> Guests { get; set; } = new();
}

public class TestGuest
{
    public required string Name { get; set; }

    public int Age { get; set; }
}
