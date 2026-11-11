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
    /// <summary>
    /// The family by reflection: <c>OSError</c> and every public Core type derived from it, each built
    /// through its message constructor — so a subclass added to Core (<c>NotADirectoryError</c>, P33 D2)
    /// joins the theory without a hand row.
    /// </summary>
    public static IEnumerable<object[]> Family() => FamilyTypes()
        .Select(t => new object[] { (Exception)Activator.CreateInstance(t, "x")! });

    internal static IEnumerable<Type> FamilyTypes() => typeof(OSError).Assembly.GetExportedTypes()
        .Where(t => typeof(OSError).IsAssignableFrom(t) && !t.IsAbstract)
        .OrderBy(t => t.FullName, StringComparer.Ordinal);

    /// <summary>Floor and positive control: the scan finds the family the hand list named, and D2's addition.</summary>
    [Fact]
    public void Generated_family_holds_the_known_members()
    {
        var names = FamilyTypes().Select(t => t.Name).ToHashSet(StringComparer.Ordinal);
        foreach (var expected in new[] { "OSError", "FileNotFoundError", "FileExistsError", "IsADirectoryError", "NotADirectoryError", "PermissionError" })
            Assert.Contains(expected, names);
    }

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
