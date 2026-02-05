using Xunit;
using Sharpy.Compiler.Diagnostics;

namespace Sharpy.Compiler.Tests.Diagnostics;

public class DiagnosticExplanationsTests
{
    [Theory]
    [InlineData("SPY0200")]
    [InlineData("SPY0302")]
    [InlineData("SPY0001")]
    [InlineData("SPY0100")]
    [InlineData("SPY0002")]
    [InlineData("SPY0222")]
    [InlineData("SPY0340")]
    [InlineData("SPY0403")]
    [InlineData("SPY0507")]
    [InlineData("SPY0450")]
    public void Get_KnownCode_ReturnsExplanation(string code)
    {
        var explanation = DiagnosticExplanations.Get(code);

        Assert.NotNull(explanation);
        Assert.Equal(code, explanation!.Code);
    }

    [Fact]
    public void Get_UnknownCode_ReturnsNull()
    {
        var explanation = DiagnosticExplanations.Get("SPY9999");

        Assert.Null(explanation);
    }

    [Theory]
    [InlineData("spy0200")]
    [InlineData("Spy0200")]
    [InlineData("SPY0200")]
    public void Get_CaseInsensitive(string code)
    {
        var explanation = DiagnosticExplanations.Get(code);

        Assert.NotNull(explanation);
        Assert.Equal("SPY0200", explanation!.Code);
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
    [InlineData("SPY0200")]
    [InlineData("SPY0220")]
    [InlineData("SPY0001")]
    [InlineData("SPY0100")]
    [InlineData("SPY0302")]
    [InlineData("SPY0263")]
    [InlineData("SPY0281")]
    [InlineData("SPY0403")]
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

        // All diagnostic codes are documented
        Assert.True(all.Count >= 100, $"Expected at least 100 explanations, got {all.Count}");
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
            Assert.Matches(@"^SPY\d{4}$", code);
        }
    }

    [Fact]
    public void AllDiagnosticCodes_HaveExplanations()
    {
        var all = DiagnosticExplanations.GetAll();

        // Collect all codes from DiagnosticCodes via reflection
        var codeFields = new List<string>();
        foreach (var nestedType in typeof(DiagnosticCodes).GetNestedTypes())
        {
            foreach (var field in nestedType.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
            {
                if (field.IsLiteral && field.FieldType == typeof(string))
                {
                    var code = (string)field.GetRawConstantValue()!;
                    codeFields.Add(code);
                }
            }
        }

        var missing = codeFields.Where(c => !all.ContainsKey(c)).ToList();

        Assert.True(missing.Count == 0,
            $"The following diagnostic codes are missing explanations: {string.Join(", ", missing)}");
    }

    [Fact]
    public void UndefinedVariable_HasComprehensiveExplanation()
    {
        var explanation = DiagnosticExplanations.Get(DiagnosticCodes.Semantic.UndefinedVariable);

        Assert.NotNull(explanation);
        Assert.Equal("SPY0200", explanation!.Code);
        Assert.Equal("Undefined variable", explanation.Title);
        Assert.Equal("Semantic", explanation.Category);
        Assert.Contains("not been declared", explanation.Description);
        Assert.NotNull(explanation.Example);
        Assert.NotNull(explanation.Fix);
    }
}
