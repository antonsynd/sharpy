// Generated from src/Sharpy.Stdlib/spy/functools.spy — do not edit directly.
// To regenerate: sharpyc emit csharp src/Sharpy.Stdlib/spy/functools.spy -t library -n Sharpy
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

namespace Sharpy
{
    public static partial class Functools
    {
        public static T Reduce<T>(System.Func<T, T, T> func, Sharpy.List<T> iterable)
        {
            Sharpy.List<T> items = new global::Sharpy.List<T>(iterable);
            if (global::Sharpy.Builtins.Len(items) == 0)
            {
                throw new global::Sharpy.TypeError("reduce() of empty iterable with no initial value");
            }

            T accumulator = items[0];
            int i = 1;
            while (i < global::Sharpy.Builtins.Len(items))
            {
                accumulator = func(accumulator, items[i]);
                i = i + 1;
            }

            return accumulator;
        }
    }
}
