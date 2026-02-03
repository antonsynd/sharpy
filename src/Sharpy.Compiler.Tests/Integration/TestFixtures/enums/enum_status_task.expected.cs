#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.EnumStatusTask
{
    public static class Program
    {
        public static void Main()
        {
#line 19 "enum_status_task.spy"
            var task = new Task(Status.Pending);
#line 20 "enum_status_task.spy"
            global::Sharpy.Core.Exports.Print(task.GetStatus());
#line 22 "enum_status_task.spy"
            task.Status = Status.Active;
#line 23 "enum_status_task.spy"
            global::Sharpy.Core.Exports.Print(task.GetStatus());
#line 25 "enum_status_task.spy"
            task.Status = Status.Completed;
#line 26 "enum_status_task.spy"
            global::Sharpy.Core.Exports.Print(task.GetStatus());
        }
    }

    public enum Status
    {
        Pending = 0,
        Active = 1,
        Completed = 2,
        Failed = 3
    }

    public class Task
    {
        public Status Status;
        public Status GetStatus()
        {
#line 16 "enum_status_task.spy"
            return this.Status;
        }

        public Task(Status initialStatus)
        {
#line 13 "enum_status_task.spy"
            this.Status = initialStatus;
        }
    }
}
