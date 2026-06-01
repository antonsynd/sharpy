#nullable enable

namespace Sharpy
{
    public static partial class PprintModule
    {
        public static void Pprint(object? obj, int indent = 1, int width = 80, int? depth = null, bool compact = false, bool sortDicts = true)
        {
            var printer = new PrettyPrinter(indent, width, depth, compact, sortDicts);
            printer.Pprint(obj);
        }

        public static string Pformat(object? obj, int indent = 1, int width = 80, int? depth = null, bool compact = false, bool sortDicts = true)
        {
            var printer = new PrettyPrinter(indent, width, depth, compact, sortDicts);
            return printer.Pformat(obj);
        }

        public static bool Isreadable(object? obj)
        {
            var printer = new PrettyPrinter();
            return printer.Isreadable(obj);
        }

        public static bool Isrecursive(object? obj)
        {
            var printer = new PrettyPrinter();
            return printer.Isrecursive(obj);
        }
    }
}
