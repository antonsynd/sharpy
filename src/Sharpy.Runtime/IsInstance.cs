namespace Sharpy;

public static partial class Exports
{
    /// <summary>
    /// Return True if the object argument is an instance of the classinfo argument.
    /// </summary>
    /// <typeparam name="T">The type to check against</typeparam>
    /// <param name="obj">The object to check</param>
    /// <returns>True if obj is an instance of T, False otherwise</returns>
    public static bool IsInstance<T>(object? obj)
    {
        return obj is T;
    }

    /// <summary>
    /// Return True if the object argument is an instance of the classinfo argument.
    /// This overload accepts the type as a parameter for runtime type checking.
    /// </summary>
    /// <param name="obj">The object to check</param>
    /// <param name="classInfo">The type to check against</param>
    /// <returns>True if obj is an instance of classInfo, False otherwise</returns>
    public static bool IsInstance(object? obj, Type classInfo)
    {
        if (classInfo is null)
        {
            throw TypeError.ArgNone("isinstance", "classinfo");
        }

        if (obj is null)
        {
            return false;
        }

        return classInfo.IsInstanceOfType(obj);
    }

    /// <summary>
    /// Return True if the object argument is an instance of any of the types in classInfo.
    /// </summary>
    /// <param name="obj">The object to check</param>
    /// <param name="classInfo">A tuple of types to check against</param>
    /// <returns>True if obj is an instance of any type in classInfo, False otherwise</returns>
    public static bool IsInstance(object? obj, params Type[] classInfo)
    {
        if (classInfo is null || classInfo.Length == 0)
        {
            throw TypeError.ArgNone("isinstance", "classinfo");
        }

        if (obj is null)
        {
            return false;
        }

        foreach (var type in classInfo)
        {
            if (type is not null && type.IsInstanceOfType(obj))
            {
                return true;
            }
        }

        return false;
    }
}
