using System.Diagnostics;
using Core.Pipeline;
using Xunit;

namespace template_builder.Tests.Fixtures {
    /// <summary>
    /// 仅在环境具备完整真实 PDF 编译能力（包括 xelatex、pygmentize 以及 ctexart 宏包）时才执行的 Fact。
    /// CI 环境通过完整安装依赖闭包确保执行；在外部非完整环境则提供明确的缺失提示并跳过。
    /// </summary>
    public sealed class RequiresRealPdfCompilationFactAttribute : FactAttribute {
        public RequiresRealPdfCompilationFactAttribute() {
            if (!ValidationRunner.CheckXelatexOnPath()) {
                Skip = "未检测到 xelatex（TeX 发行版未安装），跳过依赖真实 LaTeX 编译的测试。";
                return;
            }
            if (!ValidationRunner.CheckPygmentizeOnPath()) {
                Skip = "未检测到 pygmentize（代码语法高亮引擎未安装），跳过依赖真实 LaTeX 编译的测试。";
                return;
            }
            if (!CheckLatexPackage("ctexart.cls")) {
                Skip = "未检测到 ctexart.cls（缺少 texlive-lang-chinese 中文宏包），跳过依赖真实中文 LaTeX 编译的测试。";
            }
        }

        private static bool CheckLatexPackage(string packageFile) {
            try {
                var psi = new ProcessStartInfo("kpsewhich", packageFile) {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var proc = Process.Start(psi);
                if (proc == null) return false;
                var output = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit();
                return proc.ExitCode == 0 && !string.IsNullOrWhiteSpace(output);
            } catch {
                return false;
            }
        }
    }
}
