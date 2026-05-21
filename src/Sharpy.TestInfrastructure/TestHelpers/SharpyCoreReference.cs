extern alias SharpyRT;
using System.Reflection;

namespace Sharpy.TestInfrastructure;

public static class SharpyCoreReference
{
    public static Assembly Assembly => typeof(SharpyRT::Sharpy.Builtins).Assembly;
    public static string Location => Assembly.Location;
}
