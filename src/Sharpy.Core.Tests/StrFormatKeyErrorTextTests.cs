using System.Collections.Generic;
using FluentAssertions;
using Xunit;

namespace Sharpy.Core.Tests;

/// <summary>
/// #2008 (R-CE): a field <c>str.format</c> cannot bind by name raises CPython's KeyError, whose text
/// is the key's repr. python3 3.12: <c>'{name}'.format('a')</c> → <c>KeyError: 'name'</c>;
/// <c>'{x}'.format_map({})</c> → <c>KeyError: 'x'</c>.
/// </summary>
public class StrFormatKeyErrorTextTests
{
    [Fact]
    public void KeywordField_InFormat_IsKeyErrorWithTheQuotedName()
    {
        FluentActions.Invoking(() => "{name}".Format("a"))
            .Should().Throw<KeyError>().Which.Message.Should().Be("'name'");
    }

    [Fact]
    public void MissingKey_InFormatMap_IsKeyErrorWithTheQuotedKey()
    {
        FluentActions.Invoking(() => "{x}".FormatMap(new Dict<string, object>()))
            .Should().Throw<KeyError>().Which.Message.Should().Be("'x'");
    }
}
