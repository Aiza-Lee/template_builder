namespace template_builder.Tests.Fixtures;

/// <summary>
/// 创建一个唯一的临时目录，并在 Dispose 时递归删除。用于替换测试中重复的
/// <c>try { ... } finally { Directory.Delete(tmp, recursive: true); }</c> 样板。
/// </summary>
/// <remarks>
/// 用法：
/// <code>
/// using var tmp = TempDir.Create();
/// File.WriteAllText(Path.Combine(tmp.Path, "x.txt"), "...");
///
/// // 测试结束时 tmp 自动被清理；异常路径也覆盖
/// </code>
/// 故意不实现 <c>IDisposable</c> 在测试类上的 xUnit class-fixture 语义冲突：
/// 本类仅作 using-var 局部资源。
/// </remarks>
internal sealed class TempDir : IDisposable {
    private bool _disposed;

    public string Path { get; }

    private TempDir(string path) {
        Path = path;
    }

    public static TempDir Create() {
        var path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            Guid.NewGuid().ToString("N"));
        return new TempDir(Directory.CreateDirectory(path).FullName);
    }

    public void Dispose() {
        if (_disposed) {
            return;
        }
        _disposed = true;
        // 容忍删除失败（部分测试可能已手动清理或留下锁定文件）
        try {
            Directory.Delete(Path, recursive: true);
        } catch {
            // 静默：测试结束后残留 tmp 目录是 OS 临时目录清理机制的责任
        }
    }
}
