using System;
using System.Collections.Generic;
using System.Linq;

class Program
{
    static List<long> Quicksort(List<long> arr)
    {
        if (arr.Count <= 1) return arr;
        long pivot = arr[arr.Count / 2];
        var left = arr.Where(x => x < pivot).ToList();
        var middle = arr.Where(x => x == pivot).ToList();
        var right = arr.Where(x => x > pivot).ToList();
        var result = Quicksort(left);
        result.AddRange(middle);
        result.AddRange(Quicksort(right));
        return result;
    }

    static void Main()
    {
        var rng = new Random(42);
        for (int i = 0; i < 100; i++)
        {
            var data = Enumerable.Range(0, 1000).Select(_ => (long)rng.Next(10001)).ToList();
            var sorted = Quicksort(data);
        }
        Console.WriteLine("sorted 100 arrays");
    }
}
