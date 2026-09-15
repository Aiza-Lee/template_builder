using Core.Pipeline;
using Xunit;

namespace template_builder.Tests.Fixtures {
    /// <summary>
    /// 仅在 PATH 上存在真实 <c>xelatex</c> 时才执行的 Fact。
    /// CI（GitHub Actions <c>ubuntu-latest</c> 默认镜像）不预装 TeX 发行版，
    /// 依赖真实 LaTeX 编译的测试在那种环境下会被标记为 <c>Skipped</c> 而不是失败，
    /// 避免把"缺少外部工具"误报成"代码缺陷"。本机装有 TeX Live 时照常执行。
    /// </summary>
    public sealed class RequiresXelatexFactAttribute : FactAttribute {
        public RequiresXelatexFactAttribute() {
            if (!ValidationRunner.CheckXelatexOnPath()) {
                Skip = "未检测到 xelatex（TeX 发行版未安装），跳过依赖真实 LaTeX 编译的测试。";
            }
        }
    }
}
