// extended_interface.cs
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;
using static Sharpy.Test.BaseInterface.Exports;
using Sharpy.Test.BaseInterface;

namespace Sharpy.Test.ExtendedInterface
{
    public static class Exports
    {
    }

    public interface IEntity : Sharpy.Test.BaseInterface.IIdentity
    {
        string GetName();
        bool IsActive();
    }
}

// main.cs
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;
using static Sharpy.Test.Implementation.Exports;
using Sharpy.Test.Implementation;
using static Sharpy.Test.BaseInterface.Exports;
using Sharpy.Test.BaseInterface;

namespace Sharpy.Test.Main
{
    public static class Program
    {
        public static void Main()
        {
#line 8 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/cross_module_inheritance/interface_inheritance_chain/main.spy"
            Sharpy.Test.Implementation.User user = new Sharpy.Test.Implementation.User(1, "Alice");
#line 11 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/cross_module_inheritance/interface_inheritance_chain/main.spy"
            global::Sharpy.Core.Exports.Print(user.GetId());
#line 12 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/cross_module_inheritance/interface_inheritance_chain/main.spy"
            global::Sharpy.Core.Exports.Print(user.GetName());
#line 13 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/cross_module_inheritance/interface_inheritance_chain/main.spy"
            global::Sharpy.Core.Exports.Print(user.IsActive());
#line 16 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/cross_module_inheritance/interface_inheritance_chain/main.spy"
            Sharpy.Test.BaseInterface.IIdentity identifiable = user;
#line 17 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/cross_module_inheritance/interface_inheritance_chain/main.spy"
            global::Sharpy.Core.Exports.Print(identifiable.GetId());
        }
    }
}

// implementation.cs
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;
using static Sharpy.Test.ExtendedInterface.Exports;
using Sharpy.Test.ExtendedInterface;

namespace Sharpy.Test.Implementation
{
    public static class Exports
    {
    }

    public class User : Sharpy.Test.ExtendedInterface.IEntity
    {
        public int Id;
        public string Name;
        public bool Active;
        public int GetId()
        {
#line 17 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/cross_module_inheritance/interface_inheritance_chain/implementation.spy"
            return this.Id;
        }

        public string GetName()
        {
#line 20 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/cross_module_inheritance/interface_inheritance_chain/implementation.spy"
            return this.Name;
        }

        public bool IsActive()
        {
#line 23 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/cross_module_inheritance/interface_inheritance_chain/implementation.spy"
            return this.Active;
        }

        public User(int id, string name)
        {
#line 12 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/cross_module_inheritance/interface_inheritance_chain/implementation.spy"
            this.Id = id;
#line 13 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/cross_module_inheritance/interface_inheritance_chain/implementation.spy"
            this.Name = name;
#line 14 "/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/cross_module_inheritance/interface_inheritance_chain/implementation.spy"
            this.Active = true;
        }
    }
}

// base_interface.cs
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.Test.BaseInterface
{
    public static class Exports
    {
    }

    public interface IIdentity
    {
        int GetId();
    }
}
