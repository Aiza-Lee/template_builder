using Core.Tui;
using Utils;
using Xunit;

namespace template_builder.Tests.Tui;

/// <summary>
/// <see cref="TuiLogSink"/> 单元测试。它是 TUI 构建日志的唯一数据源，
/// 因此「有界」与「级别过滤」两条性质必须被测试锁住：前者防 xelatex 输出撑爆内存，
/// 后者保证 <c>--verbose</c> 的语义与命令行路径一致。
/// </summary>
public class TuiLogSinkTests {
    [Fact]
    public void Append_WithinCapacity_KeepsAllEntriesInOrder() {
        var sink = new TuiLogSink(capacity: 10);

        sink.Info("第一");
        sink.Info("第二");
        sink.Info("第三");

        var snapshot = sink.Snapshot();
        Assert.Equal(3, snapshot.Count);
        Assert.Equal(["第一", "第二", "第三"], snapshot.Select(e => e.Message));
    }

    [Fact]
    public void Append_BeyondCapacity_DropsOldestEntries() {
        var sink = new TuiLogSink(capacity: 3);

        for (var i = 1; i <= 5; i++) {
            sink.Info($"第{i}条");
        }

        var snapshot = sink.Snapshot();
        Assert.Equal(3, snapshot.Count);
        Assert.Equal("第3条", snapshot[0].Message);
        Assert.Equal("第5条", snapshot[^1].Message);
    }

    [Fact]
    public void Debug_IsSuppressedAtDefaultLevel_AndEnabledAfterSetLevel() {
        var sink = new TuiLogSink();
        sink.SetLevel(LogLevel.INFO);

        sink.Debug("调试信息");
        Assert.Equal(0, sink.Count);

        sink.SetLevel(LogLevel.DEBUG);
        sink.Debug("调试信息");
        Assert.Equal(1, sink.Count);
    }

    [Fact]
    public void WarningAndError_CarryPrefixAndLevel() {
        var sink = new TuiLogSink();

        sink.Warning("注意");
        sink.Error("出错");

        var snapshot = sink.Snapshot();
        Assert.Equal(LogLevel.WARNING, snapshot[0].Level);
        Assert.Equal("[Warning] 注意", snapshot[0].Message);
        Assert.Equal(LogLevel.ERROR, snapshot[1].Level);
        Assert.Equal("[Error] 出错", snapshot[1].Message);
    }

    [Fact]
    public void Snapshot_ReturnsCopy_NotLiveView() {
        var sink = new TuiLogSink();
        sink.Info("第一");

        var snapshot = sink.Snapshot();
        sink.Info("第二");

        Assert.Single(snapshot);
        Assert.Equal(2, sink.Count);
    }

    [Fact]
    public void Constructor_RejectsNonPositiveCapacity() {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TuiLogSink(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TuiLogSink(-1));
    }

    [Fact]
    public void Append_IsSafeUnderConcurrentWriters() {
        // 构建线程写日志、渲染线程读快照是本类的正常用法，二者并发不得丢失记录
        var sink = new TuiLogSink(capacity: 10_000);

        Parallel.For(0, 500, i => sink.Info($"并发{i}"));

        Assert.Equal(500, sink.Count);
    }
}
