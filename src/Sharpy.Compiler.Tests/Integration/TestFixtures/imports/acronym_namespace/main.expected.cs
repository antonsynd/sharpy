// Snapshot: a path segment that is entirely an acronym casts to the same namespace segment in both emission positions (#1173).
// api/__init__.cs
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Sharpy.Test;

namespace Sharpy.Test.API
{
    [global::Sharpy.SharpyModule("api")]
    public static partial class ApiModule
    {
    }
}

// api/ui.cs
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Sharpy.Test;

namespace Sharpy.Test.API.UI
{
    [global::Sharpy.SharpyModule("api.ui")]
    public static partial class UiModule
    {
    }

    [global::Sharpy.SharpyModuleType("api.ui", "Widget")]
    public class Widget
    {
        public string Label;
        public string Render()
#line 7 "ui.spy"
        {
#line (8, 9) - (8, 34) 12 "ui.spy"
            return FormattableString.Invariant($"[{(global::Sharpy.Builtins.Str(this.Label))}]");
#line hidden
        }

        public Widget(string label)
#line 4 "ui.spy"
        {
#line (5, 9) - (5, 27) 12 "ui.spy"
            this.Label = label;
#line hidden
        }
    }
}
#line default


// db.cs
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Sharpy.Test;

namespace Sharpy.Test.DB
{
    [global::Sharpy.SharpyModule("db")]
    public static partial class DbModule
    {
    }

    [global::Sharpy.SharpyModuleType("db", "Record")]
    public class Record
    {
        public string Key;
        public string Describe()
#line 7 "db.spy"
        {
#line (8, 9) - (8, 38) 12 "db.spy"
            return FormattableString.Invariant($"Record({(global::Sharpy.Builtins.Str(this.Key))})");
#line hidden
        }

        public Record(string key)
#line 4 "db.spy"
        {
#line (5, 9) - (5, 23) 12 "db.spy"
            this.Key = key;
#line hidden
        }
    }
}
#line default


// main.cs
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Sharpy.Test;

namespace Sharpy.Test.Main
{
    public static partial class MainModule
    {
        public static string Combine(global::Sharpy.Test.DB.Record r, global::Sharpy.Test.API.UI.Widget w)
        {
#line (12, 5) - (12, 44) 12 "main.spy"
            return r.Describe() + " " + w.Render();
#line hidden
        }

        public static void Main()
        {
#line (16, 5) - (16, 30) 12 "main.spy"
            var madeRecord = new global::Sharpy.Test.DB.Record("k");
#line (17, 5) - (17, 31) 12 "main.spy"
            var madeWidget = new global::Sharpy.Test.API.UI.Widget("ok");
#line (20, 5) - (20, 44) 12 "main.spy"
            global::Sharpy.Test.DB.Record annotatedRecord = madeRecord;
#line (21, 5) - (21, 44) 12 "main.spy"
            global::Sharpy.Test.API.UI.Widget annotatedWidget = madeWidget;
#line (23, 5) - (23, 39) 12 "main.spy"
            global::Sharpy.Builtins.Print(annotatedRecord.Describe());
#line (24, 5) - (24, 37) 12 "main.spy"
            global::Sharpy.Builtins.Print(annotatedWidget.Render());
#line (25, 5) - (25, 55) 12 "main.spy"
            global::Sharpy.Builtins.Print(global::Sharpy.Test.Main.MainModule.Combine(annotatedRecord, annotatedWidget));
#line hidden
        }
    }
}
#line default
