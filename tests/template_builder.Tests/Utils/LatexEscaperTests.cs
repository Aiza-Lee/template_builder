using Utils;
using Xunit;

namespace template_builder.Tests;

public class LatexEscaperTests {
	[Fact]
	public void Escape_Underscore_OutputsBackslashUnderscore() {
		Assert.Equal(@"a\_b", LatexEscaper.Escape("a_b"));
	}

	[Fact]
	public void Escape_Percent_OutputsBackslashPercent() {
		Assert.Equal(@"50\%", LatexEscaper.Escape("50%"));
	}

	[Fact]
	public void Escape_Ampersand_OutputsBackslashAmpersand() {
		Assert.Equal(@"a\&b", LatexEscaper.Escape("a&b"));
	}

	[Fact]
	public void Escape_Backslash_OutputsTextbackslash() {
		Assert.Equal(@"\textbackslash{}", LatexEscaper.Escape(@"\"));
	}

	[Fact]
	public void Escape_Dollar_OutputsBackslashDollar() {
		Assert.Equal(@"\$5", LatexEscaper.Escape("$5"));
	}

	[Fact]
	public void Escape_Hash_OutputsBackslashHash() {
		Assert.Equal(@"\#1", LatexEscaper.Escape("#1"));
	}

	[Fact]
	public void Escape_NoSpecials_PassesThrough() {
		Assert.Equal("hello world", LatexEscaper.Escape("hello world"));
	}

	[Fact]
	public void Escape_NullOrEmpty_ReturnsInput() {
		Assert.Equal(string.Empty, LatexEscaper.Escape(""));
		Assert.Equal(string.Empty, LatexEscaper.Escape(null));
	}

	[Theory]
	[InlineData("{", @"\{")]
	[InlineData("}", @"\}")]
	[InlineData("^", @"\^{}")]
	[InlineData("~", @"\textasciitilde{}")]
	public void Escape_BracesAndHatTilde_EscapesCorrectly(string input, string expected) {
		Assert.Equal(expected, LatexEscaper.Escape(input));
	}
}
