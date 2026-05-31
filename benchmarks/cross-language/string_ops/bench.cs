using System;

class Program
{
    static void Main()
    {
        // String concatenation and manipulation
        string result = "";
        for (int i = 0; i < 10000; i++)
        {
            result = result + i.ToString();
        }

        // String methods
        long count = 0;
        for (int j = 0; j < 1000; j++)
        {
            string upper = result.ToUpper();
            string lower = result.ToLower();
            string[] parts = result.Split('5');
            count += parts.Length;
        }
        Console.WriteLine($"operations: {count}");
    }
}
