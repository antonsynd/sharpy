#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.BoolAccessControl
{
    public static class Program
    {
        public static bool IsLocked = true;
        public static bool IsOpen = !IsLocked;
        public static void Main()
        {
#line 31 "bool_access_control.spy"
            var adminUser = new AccessControl(true, true, true);
#line 32 "bool_access_control.spy"
            global::Sharpy.Core.Exports.Print(adminUser.CanAccessResource());
#line 33 "bool_access_control.spy"
            global::Sharpy.Core.Exports.Print(adminUser.GetAccessLevel());
#line 36 "bool_access_control.spy"
            var regularUser = new AccessControl(false, true, true);
#line 37 "bool_access_control.spy"
            global::Sharpy.Core.Exports.Print(regularUser.CanAccessResource());
#line 38 "bool_access_control.spy"
            global::Sharpy.Core.Exports.Print(regularUser.GetAccessLevel());
#line 41 "bool_access_control.spy"
            var limitedUser = new AccessControl(false, true, false);
#line 42 "bool_access_control.spy"
            global::Sharpy.Core.Exports.Print(limitedUser.CanAccessResource());
#line 43 "bool_access_control.spy"
            global::Sharpy.Core.Exports.Print(limitedUser.GetAccessLevel());
#line 46 "bool_access_control.spy"
            var guestUser = new AccessControl(false, false, false);
#line 47 "bool_access_control.spy"
            global::Sharpy.Core.Exports.Print(guestUser.CanAccessResource());
#line 48 "bool_access_control.spy"
            global::Sharpy.Core.Exports.Print(guestUser.GetAccessLevel());
#line 51 "bool_access_control.spy"
            global::Sharpy.Core.Exports.Print(IsOpen);
        }
    }

    public class AccessControl
    {
        public bool IsAdmin;
        public bool IsAuthenticated;
        public bool HasPermission;
        public bool CanAccessResource()
        {
#line 14 "bool_access_control.spy"
            return this.IsAuthenticated && (this.IsAdmin || this.HasPermission);
        }

        public int GetAccessLevel()
        {
#line 17 "bool_access_control.spy"
            int level = 0;
#line 18 "bool_access_control.spy"
            if (this.IsAuthenticated)
            {
#line 19 "bool_access_control.spy"
                level = level + 1;
            }

#line 20 "bool_access_control.spy"
            if (this.HasPermission)
            {
#line 21 "bool_access_control.spy"
                level = level + 1;
            }

#line 22 "bool_access_control.spy"
            if (this.IsAdmin)
            {
#line 23 "bool_access_control.spy"
                level = level + 2;
            }

#line 24 "bool_access_control.spy"
            return level;
        }

        public AccessControl(bool admin, bool authenticated, bool permission)
        {
#line 9 "bool_access_control.spy"
            this.IsAdmin = admin;
#line 10 "bool_access_control.spy"
            this.IsAuthenticated = authenticated;
#line 11 "bool_access_control.spy"
            this.HasPermission = permission;
        }
    }
}
