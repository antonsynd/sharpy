using System;
using System.Collections.Generic;
using System.Linq;

class Program
{
    static void Main()
    {
        long total = 0;
        for (int i = 0; i < 500; i++)
        {
            // List comprehension with filter
            var evens = Enumerable.Range(0, 1000).Where(x => x % 2 == 0).Select(x => (long)x * x).ToList();
            // Nested comprehension
            var pairs = new List<long>();
            for (int a = 0; a < 50; a++)
                for (int b = 0; b < 50; b++)
                    pairs.Add(a + b);
            total += evens.Count + pairs.Count;
        }
        Console.WriteLine($"total elements: {total}");
    }
}
