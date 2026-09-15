using Core.Tui;
using Xunit;

namespace template_builder.Tests.Tui;

/// <summary>
/// <see cref="TuiEnvironment"/> 的判定逻辑测试。
/// <para>
/// 这些用例锁住的是一个真实发生过的崩溃：在非交互环境执行 <c>--tui</c> 时，
/// Spectre.Console 读按键会抛 <c>InvalidOperationException</c> 并打印整段堆栈。
/// 只有在进入 TUI 前拦住，用户才会看到可读的中文提示。
/// </para>
/// <para>
/// 测试的是纯函数 <c>Evaluate</c> 而非 <c>SupportsInteractiveUi</c>：后者依赖宿主进程的
/// 真实重定向状态，在测试运行器下不稳定，断言会随环境漂移。
/// </para>
/// </summary>
public class TuiEnvironmentTests {
    [Fact]
    public void Evaluate_AllClear_SupportsTui() {
        Assert.True(TuiEnvironment.Evaluate(
            inputRedirected: false,
            outputRedirected: false,
            capabilitiesInteractive: true
        ));
    }

    [Fact]
    public void Evaluate_InputRedirected_DoesNotSupportTui() {
        // 管道 / `--tui < /dev/null` / CI 脚本的典型情形
        Assert.False(TuiEnvironment.Evaluate(
            inputRedirected: true,
            outputRedirected: false,
            capabilitiesInteractive: true
        ));
    }

    [Fact]
    public void Evaluate_OutputRedirected_DoesNotSupportTui() {
        // `--tui > log.txt`：Live 渲染无法工作
        Assert.False(TuiEnvironment.Evaluate(
            inputRedirected: false,
            outputRedirected: true,
            capabilitiesInteractive: true
        ));
    }

    [Fact]
    public void Evaluate_CapabilitiesReportNonInteractive_DoesNotSupportTui() {
        Assert.False(TuiEnvironment.Evaluate(
            inputRedirected: false,
            outputRedirected: false,
            capabilitiesInteractive: false
        ));
    }

    [Fact]
    public void Evaluate_BothRedirected_DoesNotSupportTui() {
        Assert.False(TuiEnvironment.Evaluate(
            inputRedirected: true,
            outputRedirected: true,
            capabilitiesInteractive: false
        ));
    }
}
