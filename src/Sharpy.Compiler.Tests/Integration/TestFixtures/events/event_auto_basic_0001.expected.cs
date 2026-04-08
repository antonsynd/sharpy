// Snapshot: Auto-event with subscribe, raise, and unsubscribe
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class EventAutoBasic0001
{
    public delegate void MsgHandler(Sharpy.Str msg);
    public class Publisher
    {
        public void Notify(Sharpy.Str msg)
        {
#line 8 "event_auto_basic_0001.spy"
            this.OnMsg?.Invoke(msg);
        }

        public event MsgHandler? OnMsg;
    }

    public static void Handler(Sharpy.Str msg)
    {
#line 11 "event_auto_basic_0001.spy"
        global::Sharpy.Builtins.Print(msg);
    }

    public static void Main()
    {
#line 14 "event_auto_basic_0001.spy"
        Publisher p = new Publisher();
#line 15 "event_auto_basic_0001.spy"
        p.OnMsg += Handler;
#line 16 "event_auto_basic_0001.spy"
        p.Notify(((Sharpy.Str)"hello"));
#line 17 "event_auto_basic_0001.spy"
        p.OnMsg -= Handler;
#line 18 "event_auto_basic_0001.spy"
        p.Notify(((Sharpy.Str)"should not print"));
#line 19 "event_auto_basic_0001.spy"
        global::Sharpy.Builtins.Print(((Sharpy.Str)"done"));
    }
}
