using Sharpy.Collections.Interfaces;

namespace Sharpy
{
    public static partial class Builtins
    {
        public static T Sum<T>(Iterable<T> iterable) where T : notnull, Addable<T>
        {
            var iterator = iterable.__Iter__();

            try
            {
                var result = iterator.__Next__();

                // This sentinel is needed to ensure linters don't think this
                // is an infinite loop
                var shouldLoop = true;

                while (shouldLoop)
                {
                    try
                    {
                        var elem = iterator.__Next__();

                        result = result.__Add__(elem);
                    }
                    catch (StopIteration)
                    {
                        shouldLoop = false;

                        break;
                    }
                }

                return result;
            }
            catch (StopIteration)
            {
                throw new ValueError("Sum() iterable argument is empty");
            }
        }
    }
}
