namespace Sharpy;

/// <summary>
/// Type conversion functions for int
/// </summary>
public static partial class Exports
{
    /// <summary>
    /// Convert bool to int
    /// </summary>
    public static int Int(bool b)
    {
        return b ? 1 : 0;
    }

    /// <summary>
    /// Convert int to int (identity)
    /// </summary>
    public static int Int(int i)
    {
        return i;
    }

    /// <summary>
    /// Convert long to int
    /// </summary>
    public static int Int(long l)
    {
        if (l < int.MinValue || l > int.MaxValue)
        {
            throw new OverflowException($"Value {l} is out of range for int");
        }
        return (int)l;
    }

    /// <summary>
    /// Convert float to int (truncates)
    /// </summary>
    public static int Int(float f)
    {
        return (int)f;
    }

    /// <summary>
    /// Convert double to int (truncates)
    /// </summary>
    public static int Int(double d)
    {
        return (int)d;
    }

    /// <summary>
    /// Convert decimal to int (truncates)
    /// </summary>
    public static int Int(decimal m)
    {
        return (int)m;
    }

    /// <summary>
    /// Parse string to int
    /// </summary>
    public static int Int(string s)
    {
        if (string.IsNullOrWhiteSpace(s))
        {
            throw new ValueError($"invalid literal for int() with base 10: '{s}'");
        }

        s = s.Trim();
        
        if (!int.TryParse(s, out int result))
        {
            throw new ValueError($"invalid literal for int() with base 10: '{s}'");
        }

        return result;
    }

    /// <summary>
    /// Convert byte to int
    /// </summary>
    public static int Int(byte b)
    {
        return b;
    }

    /// <summary>
    /// Convert sbyte to int
    /// </summary>
    public static int Int(sbyte sb)
    {
        return sb;
    }

    /// <summary>
    /// Convert short to int
    /// </summary>
    public static int Int(short s)
    {
        return s;
    }

    /// <summary>
    /// Convert ushort to int
    /// </summary>
    public static int Int(ushort us)
    {
        return us;
    }

    /// <summary>
    /// Convert uint to int
    /// </summary>
    public static int Int(uint u)
    {
        if (u > int.MaxValue)
        {
            throw new OverflowException($"Value {u} is out of range for int");
        }
        return (int)u;
    }

    /// <summary>
    /// Convert ulong to int
    /// </summary>
    public static int Int(ulong ul)
    {
        if (ul > int.MaxValue)
        {
            throw new OverflowException($"Value {ul} is out of range for int");
        }
        return (int)ul;
    }
}
