using Core.Pipeline;
using Xunit;

namespace template_builder.Tests.Fixtures {
    /// <summary>
    /// 仅在 PATH 上存在真实 <c>xelatex</c> 时才执行的 Fact。
    /// Linux CI 安装 TeX 发行版并要求全部测试执行；其他平台与本地缺少
    /// 外部工具时明确标记为 <c>Skipped</c>。本机装有 TeX Live 时照常执行。
    /// </summary>
    public sealed class RequiresXelatexFactAttribute : FactAttribute {
        public RequiresXelatexFactAttribute() {
            if (!ValidationRunner.CheckXelatexOnPath()) {
                Skip = "未检测到 xelatex（TeX 发行版未安装），跳过依赖真实 LaTeX 编译的测试。";
            }
        }
    }
}
