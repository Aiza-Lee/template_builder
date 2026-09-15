using Utils;

namespace Core.Tui {
    /// <summary>一条 TUI 日志记录。</summary>
    internal sealed record TuiLogEntry(DateTime Timestamp, LogLevel Level, string Message);

    /// <summary>
    /// 面向 TUI 的 <see cref="ILogger"/> 实现：把日志累积到有界环形缓冲区，供全屏界面按帧读取渲染。
    /// <para>
    /// 与 <see cref="Logger"/> 的关键区别是不直接写 <c>Console</c>——那会与 Spectre.Console 的
    /// Live 渲染争抢光标，把界面打散。改为缓冲区后，渲染线程只需 <see cref="Snapshot"/> 取走副本。
    /// </para>
    /// <para>
    /// 缓冲区有界：xelatex 单次编译即可产生数千行 DEBUG 输出，无界累积会撑爆内存。
    /// 超出容量时丢弃最旧记录（保留最近上下文，与终端滚屏行为一致）。
    /// </para>
    /// </summary>
    internal sealed class TuiLogSink : ILogger {
        private readonly object _gate = new();
        private readonly Queue<TuiLogEntry> _entries = new();
        private readonly int _capacity;

        // 日志级别过滤状态：Info/Warning/Error/Debug 全部经过它，故需跨线程可见
        private volatile LogLevel _level = LogLevel.INFO;

        public TuiLogSink(int capacity = 400) {
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity), "容量必须为正整数。");
            _capacity = capacity;
        }

        /// <summary>环形缓冲区容量（超出后丢弃最旧记录）。</summary>
        public int Capacity => _capacity;

        /// <summary>当前缓冲区中的记录条数。</summary>
        public int Count {
            get { lock (_gate) { return _entries.Count; } }
        }

        /// <summary>
        /// 按时间顺序返回缓冲区快照。返回的是副本，渲染线程可在锁外安全遍历。
        /// </summary>
        public IReadOnlyList<TuiLogEntry> Snapshot() {
            lock (_gate) {
                return _entries.ToArray();
            }
        }

        public void Info(string message) => Append(LogLevel.INFO, message);
        public void Warning(string message) => Append(LogLevel.WARNING, $"[Warning] {message}");
        public void Error(string message) => Append(LogLevel.ERROR, $"[Error] {message}");
        public void Debug(string message) => Append(LogLevel.DEBUG, $"[Debug] {message}");

        public void SetLevel(LogLevel level) => _level = level;

        private void Append(LogLevel level, string message) {
            lock (_gate) {
                // 语义与 Logger 一致：级别数值越大越啰嗦，超过当前阈值即丢弃
                if (level > _level) return;
                _entries.Enqueue(new TuiLogEntry(DateTime.Now, level, message));
                while (_entries.Count > _capacity) {
                    _entries.Dequeue();
                }
            }
        }
    }
}
