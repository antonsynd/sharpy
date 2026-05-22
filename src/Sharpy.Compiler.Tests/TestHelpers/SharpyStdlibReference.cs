extern alias SharpyStdlib;
using System.Reflection;

namespace Sharpy.Compiler.Tests;

internal static class SharpyStdlibReference
{
    public static Assembly Assembly => typeof(SharpyStdlib::Sharpy.Json).Assembly;
    public static string Location => Assembly.Location;
}
