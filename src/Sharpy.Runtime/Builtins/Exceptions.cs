namespace Sharpy;

/// <summary>
/// Type error exception
/// </summary>
public class TypeError : Exception
{
    public TypeError(string message) : base(message)
    {
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
