using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Users.Anton.Documents.Github.Sharpy.DogfoodOutput.Issues._20260113162727CompilationFailed0004.Source
{
    public static class Exports
    {
        public static void Main()
        {
            int a = 15;
            int b = 4;
            int sumResult = a + b;
            int diffResult = a - b;
            int prodResult = a * b;
            int divResult = (int)Math.Floor((double)(a) / b);
            int modResult = a % b;
            global::Sharpy.Core.Exports.Print(sumResult);
            global::Sharpy.Core.Exports.Print(diffResult);
            global::Sharpy.Core.Exports.Print(prodResult);
            global::Sharpy.Core.Exports.Print(divResult);
            global::Sharpy.Core.Exports.Print(modResult);
        }
    }
}