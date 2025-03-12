using static Sharpy.Sys.Builtins;

namespace Sharpy {
    public static partial class Builtins {
        public static void Print(Object? obj, uint file = Stdout, bool flush = false) {
            var result = obj?.__Str__() ?? "None";

            _Print(result, file, flush);
        }

        public static void Print(object? obj, uint file = Stdout, bool flush = false) {
            var result = obj?.ToString() ?? "None";

            _Print(result, file, flush);
        }

        private static void _Print(string s, uint file = Stdout, bool flush = false) {
            if (file == Stddev) {
                return;
            }

            var textWriter = file == Stdout ? Console.Out : Console.Error;

            textWriter.WriteLine(s);

            if (flush) {
                textWriter.Flush();
            }
        }
    }
}
