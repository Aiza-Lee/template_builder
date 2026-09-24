using Utils;
using Xunit;

namespace template_builder.Tests;

public class NaturalStringComparerTests {
    [Fact]
    public void NaturalSort_SortsNumbersCorrectly() {
        var items = new[] { "10.py", "2.py", "1.py", "20.py", "3.py" };
        var sorted = items.OrderBy(x => x, NaturalStringComparer.OrdinalIgnoreCase).ToArray();
        Assert.Equal(["1.py", "2.py", "3.py", "10.py", "20.py"], sorted);
    }

    [Fact]
    public void NaturalSort_HandlesMultiSegmentNumbers() {
        var items = new[] { "section_1_part_10.txt", "section_1_part_2.txt", "section_10_part_1.txt", "section_2_part_1.txt" };
        var sorted = items.OrderBy(x => x, NaturalStringComparer.OrdinalIgnoreCase).ToArray();
        Assert.Equal([
            "section_1_part_2.txt",
            "section_1_part_10.txt",
            "section_2_part_1.txt",
            "section_10_part_1.txt"
        ], sorted);
    }

    [Fact]
    public void NaturalSort_HandlesArbitraryLargeNumbersWithoutOverflow() {
        var small = "100000000000000000000000000000";
        var large = "100000000000000000000000000001";
        var veryLarge = "2000000000000000000000000000000";

        var items = new[] { veryLarge, large, small };
        var sorted = items.OrderBy(x => x, NaturalStringComparer.OrdinalIgnoreCase).ToArray();
        Assert.Equal([small, large, veryLarge], sorted);
    }

    [Fact]
    public void NaturalSort_IsCaseInsensitive() {
        var items = new[] { "beta", "Alpha", "alpha2", "Alpha10" };
        var sorted = items.OrderBy(x => x, NaturalStringComparer.OrdinalIgnoreCase).ToArray();
        Assert.Equal(["Alpha", "alpha2", "Alpha10", "beta"], sorted);
    }

    [Fact]
    public void NaturalSort_HandlesNullAndEmpty() {
        var cmp = NaturalStringComparer.OrdinalIgnoreCase;
        Assert.Equal(0, cmp.Compare(null, null));
        Assert.True(cmp.Compare(null, "a") < 0);
        Assert.True(cmp.Compare("a", null) > 0);
        Assert.Equal(0, cmp.Compare("", ""));
    }
}
