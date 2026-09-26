// Snapshot: Auto-event with subscribe, raise, and unsubscribe
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

namespace EventAutoBasic0001
{
    public static partial class EventAutoBasic0001Module
    {
        public static void Handler(string msg)
        {
#line (11, 5) - (11, 15) 12 "event_auto_basic_0001.spy"
            global::Sharpy.Builtins.Print(msg);
#line hidden
        }

        public static void Main()
        {
#line (14, 5) - (14, 32) 12 "event_auto_basic_0001.spy"
            global::EventAutoBasic0001.Publisher p = new global::EventAutoBasic0001.Publisher();
#line (15, 5) - (15, 24) 12 "event_auto_basic_0001.spy"
            p.OnMsg += global::EventAutoBasic0001.EventAutoBasic0001Module.Handler;
#line (16, 5) - (16, 22) 12 "event_auto_basic_0001.spy"
            p.Notify("hello");
#line (17, 5) - (17, 24) 12 "event_auto_basic_0001.spy"
            p.OnMsg -= global::EventAutoBasic0001.EventAutoBasic0001Module.Handler;
#line (18, 5) - (18, 33) 12 "event_auto_basic_0001.spy"
            p.Notify("should not print");
#line (19, 5) - (19, 18) 12 "event_auto_basic_0001.spy"
            global::Sharpy.Builtins.Print("done");
#line hidden
        }
    }

    [global::Sharpy.SharpyModuleType("__main__", "MsgHandler")]
    public delegate void MsgHandler(string msg);
    [global::Sharpy.SharpyModuleType("__main__", "Publisher")]
    public class Publisher
    {
        public void Notify(string msg)
#line 7 "event_auto_basic_0001.spy"
        {
#line (8, 9) - (8, 33) 12 "event_auto_basic_0001.spy"
            this.OnMsg?.Invoke(msg);
#line hidden
        }

        public event global::EventAutoBasic0001.MsgHandler? OnMsg;
    }
}
#line default
