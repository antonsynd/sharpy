// Snapshot: Interface declaring event, class implementing it
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class EventInterface0001
{
    public delegate void SimpleHandler();
    public interface INotifiable
    {
        event SimpleHandler OnNotify;
    }

    public class Publisher : INotifiable
    {
        public void Notify()
#line 10 "event_interface_0001.spy"
        {
#line (11, 9) - (11, 33) 1 "event_interface_0001.spy"
            this.OnNotify?.Invoke();
        }

        public event SimpleHandler? OnNotify;
    }

    public static void Handler()
    {
#line (14, 5) - (14, 36) 1 "event_interface_0001.spy"
        global::Sharpy.Builtins.Print("notified via interface");
    }

    public static void Main()
    {
#line (17, 5) - (17, 32) 1 "event_interface_0001.spy"
        Publisher p = new Publisher();
#line (18, 5) - (18, 27) 1 "event_interface_0001.spy"
        p.OnNotify += Handler;
#line (19, 5) - (19, 15) 1 "event_interface_0001.spy"
        p.Notify();
    }
}
