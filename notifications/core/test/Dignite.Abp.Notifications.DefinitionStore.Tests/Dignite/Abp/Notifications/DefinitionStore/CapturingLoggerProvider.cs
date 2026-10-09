using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace Dignite.Abp.Notifications.DefinitionStore;

/// <summary>Keeps every warning (and worse) a test application logs.</summary>
public sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<(string Category, string Message)> _entries = new();

    public IReadOnlyList<string> WarningsOf<TCategory>()
    {
        var category = typeof(TCategory).FullName!;
        return _entries.Where(entry => entry.Category == category).Select(entry => entry.Message).ToList();
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new CapturingLogger(categoryName, _entries);
    }

    public void Dispose()
    {
    }

    private sealed class CapturingLogger : ILogger
    {
        private readonly string _category;
        private readonly ConcurrentQueue<(string Category, string Message)> _entries;

        public CapturingLogger(string category, ConcurrentQueue<(string Category, string Message)> entries)
        {
            _category = category;
            _entries = entries;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel >= LogLevel.Warning;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel))
            {
                _entries.Enqueue((_category, formatter(state, exception)));
            }
        }
    }
}
