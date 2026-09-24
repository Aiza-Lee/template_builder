using System.IO;
using Core.Diagnostics;
using template_builder.Tests.Fixtures;
using Xunit;

namespace template_builder.Tests.Diagnostics {
    public class LatexDiagnosticParserTests {
        [Fact]
        public void Parse_UndefinedControlSequence_ExtractsReasonLineAndSourceFile() {
            using var tmp = TempDir.Create();
            var texPath = Path.Combine(tmp.Path, "mid-output.tex");
            File.WriteAllLines(texPath, new[] {
                @"\documentclass{article}",
                @"\begin{document}",
                @"% [TB_SOURCE_FILE: src/algorithms/tree.py]",
                @"\section{tree.py}",
                @"\begin{minted}{python}",
                @"def traverse():",
                @"    pass",
                @"\end{minted}",
                @"\unknownmacro"
            });

            var stdout = @"This is XeTeX, Version 3.141592653-2.6-0.999995
(./mid-output.tex
! Undefined control sequence.
l.9 \unknownmacro
                 
No pages of output.
Transcript written on mid-output.log.";

            var result = LatexDiagnosticParser.Parse(stdout, "", null, texPath);

            Assert.Equal("! Undefined control sequence.", result.FailureReason);
            Assert.Equal(9, result.TexLineNumber);
            Assert.Equal("src/algorithms/tree.py", result.CulpritSourceFile);
            Assert.NotNull(result.Recommendation);
            Assert.Contains("未定义", result.Recommendation);
        }

        [Fact]
        public void Parse_MintedShellEscape_RecommendsPygmentsAndShellEscape() {
            var stdout = @"! Package minted Error: You must invoke LaTeX with the -shell-escape flag.
See the minted package documentation for explanation.
l.15 \begin{minted}";

            var result = LatexDiagnosticParser.Parse(stdout, "");

            Assert.Contains("Package minted Error", result.FailureReason);
            Assert.Equal(15, result.TexLineNumber);
            Assert.Contains("shell-escape", result.Recommendation);
            Assert.Contains("pygments", result.Recommendation, System.StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Parse_FontNotFound_RecommendsFontDirectory() {
            var stderr = @"kpathsea: Running mktexmf SimSun
! Package fontspec Error: The font ""SimSun"" cannot be found in the current font path.
l.8 \setCJKmainfont{SimSun}";

            var result = LatexDiagnosticParser.Parse("", stderr);

            Assert.Contains("fontspec Error", result.FailureReason);
            Assert.Equal(8, result.TexLineNumber);
            Assert.Contains("fonts/", result.Recommendation);
        }

        [Fact]
        public void Parse_PreambleError_IdentifiesPreambleAndStyMissing() {
            using var tmp = TempDir.Create();
            var texPath = Path.Combine(tmp.Path, "mid-output.tex");
            File.WriteAllLines(texPath, new[] {
                @"\documentclass{article}",
                @"\usepackage{missing_package}",
                @"\begin{document}",
                @"Hello World",
                @"\end{document}"
            });

            var stdout = @"! LaTeX Error: File `missing_package.sty' not found.
Type X to quit or <RETURN> to proceed.
l.2 \usepackage{missing_package}";

            var result = LatexDiagnosticParser.Parse(stdout, "", null, texPath);

            Assert.Contains("File `missing_package.sty' not found", result.FailureReason);
            Assert.Equal(2, result.TexLineNumber);
            Assert.Contains("导言区", result.CulpritSourceFile);
            Assert.Contains("宏包", result.Recommendation);
        }

        [Fact]
        public void Parse_FromLogFile_WhenStdoutEmpty() {
            using var tmp = TempDir.Create();
            var logPath = Path.Combine(tmp.Path, "mid-output.log");
            File.WriteAllLines(logPath, new[] {
                "This is XeTeX, Version 3.141592653",
                "! Missing $ inserted.",
                "<inserted text>",
                "                $",
                "l.12 math_variable = x _ 1"
            });

            var result = LatexDiagnosticParser.Parse("", "", logPath, null);

            Assert.Equal("! Missing $ inserted.", result.FailureReason);
            Assert.Equal(12, result.TexLineNumber);
            Assert.Contains("数学符号", result.Recommendation);
        }
    }
}
