using System.Reflection;

namespace Sharpy.Stdlib.Tests;

internal static class SharpyStdlibReference
{
    public static Assembly Assembly => typeof(Sharpy.Json).Assembly;
    public static string Location => Assembly.Location;
}
