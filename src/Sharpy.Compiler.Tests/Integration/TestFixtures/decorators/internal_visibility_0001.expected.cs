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
#line (12, 9) - (12, 28) 1 "internal_visibility_0001.spy"
            return "secret-42";
        }

        public string PublicMethod()
#line 18 "internal_visibility_0001.spy"
        {
#line (19, 9) - (19, 34) 1 "internal_visibility_0001.spy"
            return this.GetSecret();
        }

        internal string Label
        {
            get
            {
#line (16, 9) - (16, 27) 1 "internal_visibility_0001.spy"
                return this._Name;
            }
        }

        public Service()
#line 7 "internal_visibility_0001.spy"
        {
#line (8, 9) - (8, 33) 1 "internal_visibility_0001.spy"
            this._Name = "MyService";
        }
    }

    public static void Main()
    {
#line (22, 5) - (22, 28) 1 "internal_visibility_0001.spy"
        Service s = new Service();
#line (23, 5) - (23, 29) 1 "internal_visibility_0001.spy"
        global::Sharpy.Builtins.Print(s.PublicMethod());
#line (24, 5) - (24, 19) 1 "internal_visibility_0001.spy"
        global::Sharpy.Builtins.Print(s.Label);
    }
}
