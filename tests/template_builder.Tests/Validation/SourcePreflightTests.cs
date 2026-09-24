using System.IO;
using Core;
using Core.Models;
using Core.Validation;
using template_builder.Tests.Fixtures;
using Xunit;

namespace template_builder.Tests.Validation;

public class SourcePreflightTests {
    [Fact]
    public void Inspect_NonExistentSourceDir_ReturnsInvalidArguments() {
        using var tmp = TempDir.Create();
        var nonExistentDir = new DirectoryInfo(Path.Combine(tmp.Path, "missing_dir"));
        var config = new ProjectConfiguration();

        var result = SourcePreflight.Inspect(nonExistentDir, config);

        Assert.False(result.IsValid);
        Assert.Equal(ExitCodes.InvalidArguments, result.ExitCode);
        Assert.Contains("未找到源目录", result.Message);
    }

    [Fact]
    public void Inspect_EmptySourceDir_ReturnsValidationFailed() {
        using var tmp = TempDir.Create();
        var emptyDir = Directory.CreateDirectory(Path.Combine(tmp.Path, "empty_src"));
        var config = new ProjectConfiguration();

        var result = SourcePreflight.Inspect(emptyDir, config);

        Assert.False(result.IsValid);
        Assert.Equal(ExitCodes.ValidationFailed, result.ExitCode);
        Assert.Equal(0, result.TotalFiles);
        Assert.Equal(0, result.MatchedFiles);
        Assert.Contains("为空", result.Message);
        Assert.NotNull(result.Recommendation);
    }

    [Fact]
    public void Inspect_AllFilesFilteredOut_ReturnsValidationFailed() {
        using var tmp = TempDir.Create();
        var srcDir = Directory.CreateDirectory(Path.Combine(tmp.Path, "src"));
        // 创建仅含 .bin 与 .exe 的文件
        File.WriteAllText(Path.Combine(srcDir.FullName, "binary.bin"), "0101");
        File.WriteAllText(Path.Combine(srcDir.FullName, "run.exe"), "MZ");

        // 默认配置白名单不包含 .bin 和 .exe
        var config = new ProjectConfiguration();

        var result = SourcePreflight.Inspect(srcDir, config);

        Assert.False(result.IsValid);
        Assert.Equal(ExitCodes.ValidationFailed, result.ExitCode);
        Assert.Equal(2, result.TotalFiles);
        Assert.Equal(0, result.MatchedFiles);
        Assert.Contains("未匹配到任何有效源文件", result.Message);
        Assert.Contains("PROGRAM.include_file_types", result.Recommendation);
    }

    [Fact]
    public void Inspect_SixLevelDeepDirectory_ReturnsValidationFailed() {
        using var tmp = TempDir.Create();
        var srcDir = Directory.CreateDirectory(Path.Combine(tmp.Path, "src"));
        // 构造 6 级嵌套（深度 5 >= 5）
        var current = srcDir.FullName;
        for (int i = 0; i < 6; i++) {
            current = Path.Combine(current, $"sub_{i}");
            Directory.CreateDirectory(current);
        }
        File.WriteAllText(Path.Combine(current, "deep_code.cpp"), "// deep");

        var config = new ProjectConfiguration();

        var result = SourcePreflight.Inspect(srcDir, config);

        Assert.False(result.IsValid);
        Assert.Equal(ExitCodes.ValidationFailed, result.ExitCode);
        Assert.True(result.MaxDepth >= 5);
        Assert.Contains("嵌套层级过深", result.Message);
        Assert.Contains("subparagraph", result.Message);
    }

    [Fact]
    public void Inspect_ValidSourceTree_ReturnsOk() {
        using var tmp = TempDir.Create();
        var srcDir = Directory.CreateDirectory(Path.Combine(tmp.Path, "src"));
        File.WriteAllText(Path.Combine(srcDir.FullName, "main.cpp"), "int main() { return 0; }");
        var subDir = Directory.CreateDirectory(Path.Combine(srcDir.FullName, "utils"));
        File.WriteAllText(Path.Combine(subDir.FullName, "helper.py"), "def help(): pass");

        var config = new ProjectConfiguration();

        var result = SourcePreflight.Inspect(srcDir, config);

        Assert.True(result.IsValid);
        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.Equal(2, result.TotalFiles);
        Assert.Equal(2, result.MatchedFiles);
        Assert.Equal(1, result.MaxDepth);
    }
}
