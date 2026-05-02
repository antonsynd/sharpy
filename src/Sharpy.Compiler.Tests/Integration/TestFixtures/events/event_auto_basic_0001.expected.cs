// Snapshot: Auto-event with subscribe, raise, and unsubscribe
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class EventAutoBasic0001
{
    public delegate void MsgHandler(string msg);
    public class Publisher
    {
        public void Notify(string msg)
#line 7 "event_auto_basic_0001.spy"
        {
#line (8, 9) - (8, 33) 1 "event_auto_basic_0001.spy"
            this.OnMsg?.Invoke(msg);
        }

        public event MsgHandler? OnMsg;
    }

    public static void Handler(string msg)
    {
#line (11, 5) - (11, 15) 1 "event_auto_basic_0001.spy"
        global::Sharpy.Builtins.Print(msg);
    }

    public static void Main()
    {
#line (14, 5) - (14, 32) 1 "event_auto_basic_0001.spy"
        Publisher p = new Publisher();
#line (15, 5) - (15, 24) 1 "event_auto_basic_0001.spy"
        p.OnMsg += Handler;
#line (16, 5) - (16, 22) 1 "event_auto_basic_0001.spy"
        p.Notify("hello");
#line (17, 5) - (17, 24) 1 "event_auto_basic_0001.spy"
        p.OnMsg -= Handler;
#line (18, 5) - (18, 33) 1 "event_auto_basic_0001.spy"
        p.Notify("should not print");
#line (19, 5) - (19, 18) 1 "event_auto_basic_0001.spy"
        global::Sharpy.Builtins.Print("done");
    }
}
