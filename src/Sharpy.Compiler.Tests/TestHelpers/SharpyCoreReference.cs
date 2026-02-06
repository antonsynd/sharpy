extern alias SharpyRT;
using System.Reflection;

namespace Sharpy.Compiler.Tests;

internal static class SharpyCoreReference
{
    public static Assembly Assembly => typeof(SharpyRT::Sharpy.Builtins).Assembly;
    public static string Location => Assembly.Location;
}
