// Snapshot: Interface declaring event, class implementing it
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

namespace EventInterface0001
{
    public static partial class EventInterface0001Module
    {
        public static void Handler()
        {
#line (14, 5) - (14, 36) 12 "event_interface_0001.spy"
            global::Sharpy.Builtins.Print("notified via interface");
#line hidden
        }

        public static void Main()
        {
#line (17, 5) - (17, 32) 12 "event_interface_0001.spy"
            global::EventInterface0001.Publisher p = new global::EventInterface0001.Publisher();
#line (18, 5) - (18, 27) 12 "event_interface_0001.spy"
            p.OnNotify += global::EventInterface0001.EventInterface0001Module.Handler;
#line (19, 5) - (19, 15) 12 "event_interface_0001.spy"
            p.Notify();
#line hidden
        }
    }

    [global::Sharpy.SharpyModuleType("__main__", "SimpleHandler")]
    public delegate void SimpleHandler();
    [global::Sharpy.SharpyModuleType("__main__", "INotifiable")]
    public interface INotifiable
    {
        event global::EventInterface0001.SimpleHandler OnNotify;
    }

    [global::Sharpy.SharpyModuleType("__main__", "Publisher")]
    public class Publisher : global::EventInterface0001.INotifiable
    {
        public void Notify()
#line 10 "event_interface_0001.spy"
        {
#line (11, 9) - (11, 33) 12 "event_interface_0001.spy"
            this.OnNotify?.Invoke();
#line hidden
        }

        public event global::EventInterface0001.SimpleHandler? OnNotify;
    }
}
#line default
