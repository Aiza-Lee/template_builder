using template_builder.Tests.Fixtures;
using Xunit;

namespace template_builder.Tests;

public class RequiresRealPdfCompilationFactTests {
    [Fact]
    public void Attribute_InstantiatesSuccessfully_WithMeaningfulSkipOrNull() {
        var attr = new RequiresRealPdfCompilationFactAttribute();
        if (attr.Skip != null) {
            Assert.Contains("未检测到", attr.Skip);
        }
    }
}
