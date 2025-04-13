namespace Sharpy;

public static partial class Exports
{
    public static bool Bool(bool b)
    {
        return b;
    }

    public static bool Bool(decimal d)
    {
        return d != 0;
    }

    public static bool Bool(float f)
    {
        return f != 0;
    }

    public static bool Bool(double d)
    {
        return d != 0;
    }

    public static bool Bool(int i)
    {
        return i != 0;
    }

    public static bool Bool(uint u)
    {
        return u != 0;
    }

    public static bool Bool(short s)
    {
        return s != 0;
    }

    public static bool Bool(ushort u)
    {
        return u != 0;
    }

    public static bool Bool(long l)
    {
        return l != 0;
    }

    public static bool Bool(ulong u)
    {
        return u != 0;
    }

    public static bool Bool(byte b)
    {
        return b != 0;
    }

    public static bool Bool(sbyte s)
    {
        return s != 0;
    }

    public static bool Bool(string s)
    {
        if (s is null)
        {
            return false;
        }

        return s.Length > 0;
    }

    public static bool Bool(Object obj)
    {
        return obj?.__Bool__() ?? false;
    }

    public static bool Bool(IBoolConvertible b)
    {
        return b?.__Bool__() ?? false;
    }

    public static bool Bool(object? obj)
    {
        if (obj is null)
        {
            return false;
        }

        // This is super shitty, but C# doesn't do overload resolution
        // correctly on generic types, treating them as object.
        if (obj is Object o)
        {
            return Bool(o);
        }

        if (obj is IBoolConvertible b)
        {
            return Bool(b);
        }

        if (obj is string @string)
        {
            return Bool(@string);
        }

        if (obj is bool @bool)
        {
            return Bool(@bool);
        }

        if (obj is int @int)
        {
            return Bool(@int);
        }

        if (obj is uint @uint)
        {
            return Bool(@uint);
        }

        if (obj is byte @byte)
        {
            return Bool(@byte);
        }

        if (obj is sbyte @sbyte)
        {
            return Bool(@sbyte);
        }

        if (obj is short @short)
        {
            return Bool(@short);
        }

        if (obj is ushort @ushort)
        {
            return Bool(@ushort);
        }

        if (obj is long @long)
        {
            return Bool(@long);
        }

        if (obj is ulong @ulong)
        {
            return Bool(@ulong);
        }

        if (obj is double @double)
        {
            return Bool(@double);
        }

        if (obj is decimal @decimal)
        {
            return Bool(@decimal);
        }

        if (obj is float @float)
        {
            return Bool(@float);
        }

        throw new TypeError($"{obj.GetType().Name} is not convertible to bool");
    }
}
