using Xunit;
using Sharpy.Compiler.Diagnostics;

namespace Sharpy.Compiler.Tests.Diagnostics;

public class DiagnosticExplanationsTests
{
    [Theory]
    [InlineData("SHP0200")]
    [InlineData("SHP0265")]
    [InlineData("SHP0302")]
    [InlineData("SHP0001")]
    [InlineData("SHP0100")]
    public void Get_KnownCode_ReturnsExplanation(string code)
    {
        var explanation = DiagnosticExplanations.Get(code);

        Assert.NotNull(explanation);
        Assert.Equal(code, explanation!.Code);
    }

    [Fact]
    public void Get_UnknownCode_ReturnsNull()
    {
        var explanation = DiagnosticExplanations.Get("SHP9999");

        Assert.Null(explanation);
    }

    [Theory]
    [InlineData("shp0200")]
    [InlineData("Shp0200")]
    [InlineData("SHP0200")]
    public void Get_CaseInsensitive(string code)
    {
        var explanation = DiagnosticExplanations.Get(code);

        Assert.NotNull(explanation);
        Assert.Equal("SHP0200", explanation!.Code);
    }

    [Fact]
    public void AllExplanations_HaveRequiredFields()
    {
        var all = DiagnosticExplanations.GetAll();

        Assert.NotEmpty(all);

        foreach (var (code, explanation) in all)
        {
            Assert.False(string.IsNullOrWhiteSpace(explanation.Code), $"{code}: Code is empty");
            Assert.False(string.IsNullOrWhiteSpace(explanation.Title), $"{code}: Title is empty");
            Assert.False(string.IsNullOrWhiteSpace(explanation.Description), $"{code}: Description is empty");
            Assert.False(string.IsNullOrWhiteSpace(explanation.Category), $"{code}: Category is empty");
        }
    }

    [Theory]
    [InlineData("SHP0200")]
    [InlineData("SHP0220")]
    [InlineData("SHP0265")]
    [InlineData("SHP0001")]
    [InlineData("SHP0100")]
    [InlineData("SHP0302")]
    public void CommonCodes_HaveExamplesAndFixes(string code)
    {
        var explanation = DiagnosticExplanations.Get(code);

        Assert.NotNull(explanation);
        Assert.False(string.IsNullOrWhiteSpace(explanation!.Example), $"{code}: missing Example");
        Assert.False(string.IsNullOrWhiteSpace(explanation.Fix), $"{code}: missing Fix");
    }

    [Fact]
    public void GetAll_ReturnsMultipleEntries()
    {
        var all = DiagnosticExplanations.GetAll();

        // We documented ~30 codes
        Assert.True(all.Count >= 25, $"Expected at least 25 explanations, got {all.Count}");
    }

    [Fact]
    public void GetAll_CoversAllCategories()
    {
        var all = DiagnosticExplanations.GetAll();
        var categories = all.Values.Select(e => e.Category).Distinct().ToList();

        Assert.Contains("Lexer", categories);
        Assert.Contains("Parser", categories);
        Assert.Contains("Semantic", categories);
        Assert.Contains("Validation", categories);
        Assert.Contains("CodeGen", categories);
        Assert.Contains("Infrastructure", categories);
    }

    [Fact]
    public void AllCodes_AreValidFormat()
    {
        var all = DiagnosticExplanations.GetAll();

        foreach (var (code, _) in all)
        {
            Assert.Matches(@"^SHP\d{4}$", code);
        }
    }

    [Fact]
    public void UndefinedVariable_HasComprehensiveExplanation()
    {
        var explanation = DiagnosticExplanations.Get(DiagnosticCodes.Semantic.UndefinedVariable);

        Assert.NotNull(explanation);
        Assert.Equal("SHP0200", explanation!.Code);
        Assert.Equal("Undefined variable", explanation.Title);
        Assert.Equal("Semantic", explanation.Category);
        Assert.Contains("not been declared", explanation.Description);
        Assert.NotNull(explanation.Example);
        Assert.NotNull(explanation.Fix);
    }
}
