using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Uow;

namespace Dignite.Abp.AspNetCore.Mcp;

/// <summary>
/// Stands in for a repository write: joins the current unit of work as a database API whose save can be
/// made to fail - the way a unique index or a concurrency conflict only shows up when changes are saved,
/// after the tool itself has returned.
/// </summary>
[McpServerToolType]
public class TestUnitOfWorkTools : ITransientDependency
{
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly TestUnitOfWorkProbe _probe;

    public TestUnitOfWorkTools(IUnitOfWorkManager unitOfWorkManager, TestUnitOfWorkProbe probe)
    {
        _unitOfWorkManager = unitOfWorkManager;
        _probe = probe;
    }

    [McpServerTool(Name = "test_write")]
    public virtual string Write(string marker, bool failOnSave = false, bool failInTool = false)
    {
        var unitOfWork = _unitOfWorkManager.Current ?? throw new InvalidOperationException("No unit of work.");
        _probe.Get(marker).IsTransactional = unitOfWork.Options.IsTransactional;
        _probe.Get(marker).InsideAReservedUnitOfWork = HasReservedOuter(unitOfWork);
        unitOfWork.AddDatabaseApi("test-write-" + marker, new TestDatabaseApi(_probe.Get(marker), failOnSave));
        if (unitOfWork.Options.IsTransactional)
        {
            unitOfWork.AddTransactionApi("test-transaction-" + marker, new TestTransactionApi(_probe.Get(marker)));
        }

        if (failInTool)
        {
            throw new BusinessException("Test:InTool");
        }

        return "written";
    }

    [McpServerTool(Name = "test_fail_unexpected")]
    public virtual string FailUnexpected(string marker)
    {
        throw new InvalidOperationException("Unexpected failure " + marker);
    }

    private static bool HasReservedOuter(IUnitOfWork unitOfWork)
    {
        for (var outer = unitOfWork.Outer; outer != null; outer = outer.Outer)
        {
            // Still named after the reservation once begun (TryBeginReserved clears IsReserved, not the name).
            if (outer.ReservationName == UnitOfWork.UnitOfWorkReservationName)
            {
                return true;
            }
        }

        return false;
    }
}

public class TestUnitOfWorkProbe : ISingletonDependency
{
    private readonly ConcurrentDictionary<string, Record> _records = new();

    public Record Get(string marker) => _records.GetOrAdd(marker, _ => new Record());

    public class Record
    {
        public bool IsTransactional { get; set; }

        public bool Saved { get; set; }

        public bool RolledBack { get; set; }

        public bool Committed { get; set; }

        /// <summary>Whether the call's unit of work sits inside one the unit-of-work middleware reserved.</summary>
        public bool InsideAReservedUnitOfWork { get; set; }
    }
}

/// <summary>
/// Lets a test hold a request open after its MCP response has been written - see the middleware in
/// <see cref="AbpAspNetCoreMcpTestModule"/> - and release it once the client has read the result.
/// </summary>
public class TestResponseHold : ISingletonDependency
{
    public const string Header = "X-Test-Hold";

    private readonly ConcurrentDictionary<string, TaskCompletionSource> _holds = new();

    public Task WaitAsync(string key) => Get(key).Task.WaitAsync(TimeSpan.FromSeconds(10));

    public void Release(string key) => Get(key).TrySetResult();

    private TaskCompletionSource Get(string key) =>
        _holds.GetOrAdd(key, _ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));
}

/// <summary>
/// Stands in for a database transaction: commits only with a live token, as a real one fails when the
/// request it runs for has been aborted.
/// </summary>
public class TestTransactionApi : ITransactionApi, ISupportsRollback
{
    private readonly TestUnitOfWorkProbe.Record _record;

    public TestTransactionApi(TestUnitOfWorkProbe.Record record)
    {
        _record = record;
    }

    public Task CommitAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _record.Committed = true;
        return Task.CompletedTask;
    }

    public Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        _record.RolledBack = true;
        return Task.CompletedTask;
    }

    public void Dispose()
    {
    }
}

public class TestDatabaseApi : IDatabaseApi, ISupportsSavingChanges, ISupportsRollback
{
    public const string SaveFailedCode = "Test:SaveFailed";

    private readonly TestUnitOfWorkProbe.Record _record;
    private readonly bool _failOnSave;

    public TestDatabaseApi(TestUnitOfWorkProbe.Record record, bool failOnSave)
    {
        _record = record;
        _failOnSave = failOnSave;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        if (_failOnSave)
        {
            throw new BusinessException(SaveFailedCode);
        }

        _record.Saved = true;
        return Task.CompletedTask;
    }

    public Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        _record.RolledBack = true;
        return Task.CompletedTask;
    }
}

/// <summary>Keeps every log entry written while the test server runs.</summary>
public sealed class TestLoggerProvider : ILoggerProvider
{
    public ConcurrentQueue<(string Category, LogLevel Level, Exception? Exception)> Entries { get; } = new();

    public ILogger CreateLogger(string categoryName) => new Logger(this, categoryName);

    public void Dispose()
    {
    }

    private sealed class Logger : ILogger
    {
        private readonly TestLoggerProvider _provider;
        private readonly string _category;

        public Logger(TestLoggerProvider provider, string category)
        {
            _provider = provider;
            _category = category;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            _provider.Entries.Enqueue((_category, logLevel, exception));
        }
    }
}
