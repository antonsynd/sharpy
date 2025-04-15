namespace Sharpy;

public class TypeError(string message) : Exception(message)
{
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
}
