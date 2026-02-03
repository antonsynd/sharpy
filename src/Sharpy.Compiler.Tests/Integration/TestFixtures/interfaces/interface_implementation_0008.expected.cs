#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.InterfaceImplementation0008
{
    public static class Program
    {
        public static IGreeter G = new FriendlyGreeter(5);
        public static void Main()
        {
#line 18 "interface_implementation_0008.spy"
            global::Sharpy.Core.Exports.Print(G.Greet());
        }
    }

    public interface IGreeter
    {
        int Greet();
    }

    public class FriendlyGreeter : IGreeter
    {
        public int Value;
        public int Greet()
        {
#line 13 "interface_implementation_0008.spy"
            return this.Value + 10;
        }

        public FriendlyGreeter(int v)
        {
#line 10 "interface_implementation_0008.spy"
            this.Value = v;
        }
    }
}
