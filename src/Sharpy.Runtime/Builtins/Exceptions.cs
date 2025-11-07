namespace Sharpy;

/// <summary>
/// Type error exception
/// </summary>
public class TypeError : Exception
{
    public TypeError(string message) : base(message)
    {
    }

    internal static TypeError OpNotSupported(string op, string type)
    {
        return new TypeError($"'{op}' not supported for instances of '{type}'");
    }

    internal static TypeError IsNotInterface(string type, string @interface)
    {
        return new TypeError($"'{type}' object is not {@interface}");
    }

    internal static TypeError ArgNone(string method, string arg)
    {
        return new TypeError($"{method}() {arg} argument cannot be None");
    }

    internal static TypeError CanOnlyNot(string verb, string typeA, string notType, string preposition, string typeB)
    {
        return new TypeError($"can only {verb} {typeA} (not \"{notType}\") {preposition} {typeB}");
    }
}

/// <summary>
/// Value error exception
/// </summary>
public class ValueError : Exception
{
    public ValueError(string message) : base(message)
    {
    }
}
