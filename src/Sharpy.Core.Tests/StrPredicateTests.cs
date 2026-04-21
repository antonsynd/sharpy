using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

/// <summary>
/// Edge-case tests for string boolean predicate methods.
/// Basic "happy path" cases are already covered in StringExtensionTests.cs.
/// This file focuses on: empty strings, Unicode chars, and mixed-content scenarios.
/// </summary>
public class StrPredicateTests
{
    #region Isalpha edge cases

    [Fact]
    public void Isalpha_EmptyString_ReturnsFalse()
    {
        "".Isalpha().Should().BeFalse();
    }

    [Fact]
    public void Isalpha_UnicodeGreekLetter_ReturnsTrue()
    {
        // Python: "α".isalpha() == True
        "α".Isalpha().Should().BeTrue();
    }

    [Fact]
    public void Isalpha_MixedAlphaAndDigit_ReturnsFalse()
    {
        "abc1".Isalpha().Should().BeFalse();
    }

    [Fact]
    public void Isalpha_WithSpace_ReturnsFalse()
    {
        "hello world".Isalpha().Should().BeFalse();
    }

    #endregion

    #region Isalnum edge cases

    [Fact]
    public void Isalnum_EmptyString_ReturnsFalse()
    {
        "".Isalnum().Should().BeFalse();
    }

    [Fact]
    public void Isalnum_WithSpecialChar_ReturnsFalse()
    {
        "abc!".Isalnum().Should().BeFalse();
    }

    [Fact]
    public void Isalnum_WithSpace_ReturnsFalse()
    {
        "abc 123".Isalnum().Should().BeFalse();
    }

    #endregion

    #region Isdigit edge cases

    [Fact]
    public void Isdigit_EmptyString_ReturnsFalse()
    {
        "".Isdigit().Should().BeFalse();
    }

    [Fact]
    public void Isdigit_SuperscriptDigit_ReturnsTrue()
    {
        // Python: "²".isdigit() == True (superscript 2, Unicode category 'No', is a digit in Python)
        // .NET: char.IsDigit('²') == False — implementation bug tracked in #576
        "²".Isdigit().Should().BeTrue();
    }

    #endregion

    #region Isdecimal edge cases

    [Fact]
    public void Isdecimal_EmptyString_ReturnsFalse()
    {
        "".Isdecimal().Should().BeFalse();
    }

    [Fact]
    public void Isdecimal_SuperscriptDigit_ReturnsFalse()
    {
        // Python: "²".isdecimal() == False (superscript is not a decimal digit)
        "²".Isdecimal().Should().BeFalse();
    }

    [Fact]
    public void Isdecimal_RegularDigits_ReturnsTrue()
    {
        "0123456789".Isdecimal().Should().BeTrue();
    }

    #endregion

    #region Isnumeric edge cases

    [Fact]
    public void Isnumeric_EmptyString_ReturnsFalse()
    {
        "".Isnumeric().Should().BeFalse();
    }

    [Fact]
    public void Isnumeric_Fraction_ReturnsTrue()
    {
        // Python: "½".isnumeric() == True (vulgar fraction is numeric)
        "½".Isnumeric().Should().BeTrue();
    }

    [Fact]
    public void Isnumeric_SuperscriptDigit_ReturnsTrue()
    {
        // Python: "²".isnumeric() == True
        "²".Isnumeric().Should().BeTrue();
    }

    [Fact]
    public void Isnumeric_Letters_ReturnsFalse()
    {
        "abc".Isnumeric().Should().BeFalse();
    }

    #endregion

    #region Isascii edge cases

    [Fact]
    public void Isascii_EmptyString_ReturnsTrue()
    {
        // Python: "".isascii() == True (vacuously true)
        "".Isascii().Should().BeTrue();
    }

    [Fact]
    public void Isascii_ControlCharacter_ReturnsTrue()
    {
        // Control chars are still ASCII (U+0000 to U+001F are ASCII)
        "\x00".Isascii().Should().BeTrue();
        "\x1F".Isascii().Should().BeTrue();
    }

    [Fact]
    public void Isascii_Del_ReturnsTrue()
    {
        // DEL (127/0x7F) is still ASCII
        "\x7F".Isascii().Should().BeTrue();
    }

    [Fact]
    public void Isascii_Latin1Extended_ReturnsFalse()
    {
        // U+0080 is beyond ASCII
        "".Isascii().Should().BeFalse();
    }

    #endregion

    #region Islower edge cases

    [Fact]
    public void Islower_EmptyString_ReturnsFalse()
    {
        "".Islower().Should().BeFalse();
    }

    [Fact]
    public void Islower_LowercaseWithDigits_ReturnsTrue()
    {
        // Python: "abc1".islower() == True — digits don't count as cased
        "abc1".Islower().Should().BeTrue();
    }

    [Fact]
    public void Islower_MixedCase_ReturnsFalse()
    {
        "Abc".Islower().Should().BeFalse();
    }

    [Fact]
    public void Islower_OnlyDigits_ReturnsFalse()
    {
        // No cased characters at all — returns False
        "123".Islower().Should().BeFalse();
    }

    #endregion

    #region Isupper edge cases

    [Fact]
    public void Isupper_EmptyString_ReturnsFalse()
    {
        "".Isupper().Should().BeFalse();
    }

    [Fact]
    public void Isupper_UppercaseWithDigits_ReturnsTrue()
    {
        // Python: "ABC1".isupper() == True — digits don't count as cased
        "ABC1".Isupper().Should().BeTrue();
    }

    [Fact]
    public void Isupper_MixedCase_ReturnsFalse()
    {
        "ABc".Isupper().Should().BeFalse();
    }

    [Fact]
    public void Isupper_OnlyDigits_ReturnsFalse()
    {
        // No cased characters at all — returns False
        "123".Isupper().Should().BeFalse();
    }

    #endregion

    #region Istitle edge cases

    [Fact]
    public void Istitle_EmptyString_ReturnsFalse()
    {
        "".Istitle().Should().BeFalse();
    }

    [Fact]
    public void Istitle_AllUppercase_ReturnsFalse()
    {
        // Python: "HELLO".istitle() == False — all uppercase is not title case
        "HELLO".Istitle().Should().BeFalse();
    }

    [Fact]
    public void Istitle_SecondWordLowercase_ReturnsFalse()
    {
        // Python: "Hello world".istitle() == False — 'w' should be uppercase
        "Hello world".Istitle().Should().BeFalse();
    }

    [Fact]
    public void Istitle_WithDigitsInWord_ReturnsTrue()
    {
        // Python: "Hello2World".istitle() == True — digits are non-cased separators
        "Hello2World".Istitle().Should().BeTrue();
    }

    #endregion

    #region Isspace edge cases

    [Fact]
    public void Isspace_EmptyString_ReturnsFalse()
    {
        "".Isspace().Should().BeFalse();
    }

    [Fact]
    public void Isspace_TabAndNewline_ReturnsTrue()
    {
        "\t\n".Isspace().Should().BeTrue();
    }

    [Fact]
    public void Isspace_StringWithNonSpace_ReturnsFalse()
    {
        " a ".Isspace().Should().BeFalse();
    }

    #endregion

    #region Isprintable edge cases

    [Fact]
    public void Isprintable_EmptyString_ReturnsTrue()
    {
        // Python: "".isprintable() == True (vacuously true)
        "".Isprintable().Should().BeTrue();
    }

    [Fact]
    public void Isprintable_Newline_ReturnsFalse()
    {
        "\n".Isprintable().Should().BeFalse();
    }

    [Fact]
    public void Isprintable_Space_ReturnsTrue()
    {
        " ".Isprintable().Should().BeTrue();
    }

    [Fact]
    public void Isprintable_Tab_ReturnsFalse()
    {
        "\t".Isprintable().Should().BeFalse();
    }

    #endregion

    #region Isidentifier edge cases

    [Fact]
    public void Isidentifier_EmptyString_ReturnsFalse()
    {
        "".Isidentifier().Should().BeFalse();
    }

    [Fact]
    public void Isidentifier_UnderscorePrefix_ReturnsTrue()
    {
        "_x".Isidentifier().Should().BeTrue();
    }

    [Fact]
    public void Isidentifier_UnderscoreOnly_ReturnsTrue()
    {
        "_".Isidentifier().Should().BeTrue();
    }

    [Fact]
    public void Isidentifier_WithHyphen_ReturnsFalse()
    {
        "my-var".Isidentifier().Should().BeFalse();
    }

    #endregion
}
