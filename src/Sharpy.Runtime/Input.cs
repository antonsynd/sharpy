namespace Sharpy;

public static partial class Exports
{
    /// <summary>
    /// Read a line from standard input.
    /// </summary>
    /// <returns>The input string (without trailing newline)</returns>
    public static string Input()
    {
        var line = Console.ReadLine();
        return line ?? string.Empty;
    }

    /// <summary>
    /// Read a line from standard input after printing a prompt.
    /// </summary>
    /// <param name="prompt">The prompt to display</param>
    /// <returns>The input string (without trailing newline)</returns>
    public static string Input(string prompt)
    {
        if (prompt != null)
        {
            Console.Write(prompt);
        }
        var line = Console.ReadLine();
        return line ?? string.Empty;
    }
}
