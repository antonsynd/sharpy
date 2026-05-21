using System.Reflection;

namespace Sharpy.Stdlib.Tests;

internal static class SharpyCoreReference
{
    public static Assembly Assembly => typeof(Sharpy.Builtins).Assembly;
    public static string Location => Assembly.Location;
}
