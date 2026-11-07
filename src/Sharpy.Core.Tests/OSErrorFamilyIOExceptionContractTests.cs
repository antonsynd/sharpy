using System.IO;
using Xunit;

namespace Sharpy.Core.Tests;

/// <summary>
/// The .NET contract the OSError re-root keeps (#2124 D15, cell 11): <c>OSError</c> derives from
/// <see cref="IOException"/>, so C# host code catching <c>IOException</c> still sees every member of
/// Sharpy's OSError family, raised by Sharpy code. The one-way part of the ruling (a host catching
/// <see cref="FileNotFoundException"/> or <see cref="System.UnauthorizedAccessException"/> specifically
/// no longer sees Sharpy's) is asserted too, so the ruling's both halves are measured.
/// </summary>
public class OSErrorFamilyIOExceptionContractTests
{
    public static IEnumerable<object[]> Family() => new[]
    {
        new object[] { new OSError("x") },
        new object[] { new FileNotFoundError("x") },
        new object[] { new FileExistsError("x") },
        new object[] { new IsADirectoryError("x") },
        new object[] { new PermissionError("x") },
    };

    [Theory]
    [MemberData(nameof(Family))]
    public void Host_catch_IOException_sees_the_family(Exception raised)
    {
        Exception? caught = null;
        try
        {
            throw raised;
        }
        catch (IOException e)
        {
            caught = e;
        }
        catch (Exception)
        {
            // falls through: caught stays null
        }

        Assert.Same(raised, caught);
        Assert.IsAssignableFrom<OSError>(raised);
    }

    [Fact]
    public void Specific_dotnet_catches_do_not_see_the_family()
    {
        Assert.False((object)new FileNotFoundError("x") is FileNotFoundException);
        Assert.False((object)new PermissionError("x") is System.UnauthorizedAccessException);
    }
}
