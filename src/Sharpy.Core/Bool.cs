namespace Sharpy.Core;

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

    public static bool Bool(object? obj)
    {
        if (obj is null)
        {
            return false;
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

        // Collection types - check Count for emptiness
        // Note: ICollection (non-generic) is for arrays and old-style collections
        if (obj is System.Collections.ICollection collection)
        {
            return collection.Count > 0;
        }

        // Check for ICollection<T> or IReadOnlyCollection<T> via interface check
        // This handles List<T>, Set<T>, etc. that use explicit interface implementations
        foreach (var iface in obj.GetType().GetInterfaces())
        {
            if (iface.IsGenericType)
            {
                var genericDef = iface.GetGenericTypeDefinition();
                if (genericDef == typeof(ICollection<>) || genericDef == typeof(IReadOnlyCollection<>))
                {
                    var countProp = iface.GetProperty("Count");
                    if (countProp is not null)
                    {
                        var count = (int)countProp.GetValue(obj)!;
                        return count > 0;
                    }
                }
            }
        }

        // Non-null objects are truthy by default (matching Python's behavior)
        return true;
    }
}
