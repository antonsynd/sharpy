using System;
using System.Collections.Generic;
using System.Linq;
using Sharpy.Core;

namespace Users.Anton.Documents.Github.Sharpy.Snippets.Hello;
public static class __Module__
{
    public static void Greet(object name)
    {
        Sharpy.Core.Exports.Print("Hello,", name, "!");
    }

    public static void Main()
    {
        Sharpy.Core.Exports.Print("=== Sharpy Demo ===");
        Greet("World");
        Greet("from Sharpy");
        Sharpy.Core.Exports.Print("=== Complete ===");
    }
}