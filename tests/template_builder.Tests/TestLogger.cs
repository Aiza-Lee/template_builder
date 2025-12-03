using System.Collections.Concurrent;
using Utils;

namespace template_builder.Tests;

internal sealed class TestLogger : ILogger {
	private readonly ConcurrentQueue<(LogLevel Level, string Message)> _entries = new();

	public IReadOnlyCollection<(LogLevel Level, string Message)> Entries => _entries.ToArray();

	public void Info(string message) => _entries.Enqueue((LogLevel.INFO, message));

	public void Warning(string message) => _entries.Enqueue((LogLevel.WARNING, message));

	public void Error(string message) => _entries.Enqueue((LogLevel.ERROR, message));

	public void Debug(string message) => _entries.Enqueue((LogLevel.DEBUG, message));
}
