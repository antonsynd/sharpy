// Snapshot: @internal visibility decorator on method and property
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class InternalVisibility0001
{
    public class Service
    {
        protected string _Name;
        internal string GetSecret()
#line 11 "internal_visibility_0001.spy"
        {
#line (12, 9) - (12, 28) 12 "internal_visibility_0001.spy"
            return "secret-42";
#line hidden
        }

        public string PublicMethod()
#line 18 "internal_visibility_0001.spy"
        {
#line (19, 9) - (19, 34) 12 "internal_visibility_0001.spy"
            return this.GetSecret();
#line hidden
        }

        internal string Label
        {
            get
            {
#line (16, 9) - (16, 27) 16 "internal_visibility_0001.spy"
                return this._Name;
#line hidden
            }
        }

        public Service()
#line 7 "internal_visibility_0001.spy"
        {
#line (8, 9) - (8, 33) 12 "internal_visibility_0001.spy"
            this._Name = "MyService";
#line hidden
        }
    }

    public static void Main()
    {
#line (22, 5) - (22, 28) 8 "internal_visibility_0001.spy"
        Service s = new Service();
#line (23, 5) - (23, 29) 8 "internal_visibility_0001.spy"
        global::Sharpy.Builtins.Print(s.PublicMethod());
#line (24, 5) - (24, 19) 8 "internal_visibility_0001.spy"
        global::Sharpy.Builtins.Print(s.Label);
#line hidden
    }
}
#line default
