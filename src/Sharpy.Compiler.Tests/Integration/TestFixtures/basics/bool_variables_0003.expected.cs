#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.BoolVariables0003
{
    public static class Program
    {
        public static void RunAccessTests()
        {
#line 60 "bool_variables_0003.spy"
            StrictPolicy strict = new StrictPolicy(true, true);
#line 61 "bool_variables_0003.spy"
            PermissivePolicy permissive = new PermissivePolicy(true);
#line 63 "bool_variables_0003.spy"
            AccessManager manager1 = new AccessManager(strict, false, true);
#line 64 "bool_variables_0003.spy"
            AccessManager manager2 = new AccessManager(permissive, true, false);
#line 66 "bool_variables_0003.spy"
            global::Sharpy.Core.Exports.Print(manager1.CheckAccess(true, true, false));
#line 67 "bool_variables_0003.spy"
            global::Sharpy.Core.Exports.Print(manager1.CheckAccess(true, false, false));
#line 68 "bool_variables_0003.spy"
            global::Sharpy.Core.Exports.Print(manager1.CheckAccess(false, false, true));
#line 70 "bool_variables_0003.spy"
            global::Sharpy.Core.Exports.Print(manager2.CheckAccess(true, false, false));
#line 72 "bool_variables_0003.spy"
            manager2.ToggleLock();
#line 73 "bool_variables_0003.spy"
            global::Sharpy.Core.Exports.Print(manager2.CheckAccess(true, false, false));
#line 74 "bool_variables_0003.spy"
            global::Sharpy.Core.Exports.Print(manager2.CheckAccess(false, true, false));
#line 76 "bool_variables_0003.spy"
            StrictPolicy disabledPolicy = new StrictPolicy(false, true);
#line 77 "bool_variables_0003.spy"
            AccessManager manager3 = new AccessManager(disabledPolicy, false, false);
#line 78 "bool_variables_0003.spy"
            global::Sharpy.Core.Exports.Print(manager3.CheckAccess(false, false, false));
        }

        public static void Main()
        {
#line 81 "bool_variables_0003.spy"
            RunAccessTests();
        }
    }

    public abstract class SecurityPolicy
    {
        public bool Enabled;
        public abstract bool Evaluate(bool isAuthenticated, bool hasRole);
        public SecurityPolicy(bool enabled)
        {
#line 8 "bool_variables_0003.spy"
            this.Enabled = enabled;
        }
    }

    public class StrictPolicy : SecurityPolicy
    {
        public bool RequireBoth;
        public override bool Evaluate(bool isAuthenticated, bool hasRole)
        {
#line 23 "bool_variables_0003.spy"
            if (!this.Enabled)
            {
#line 24 "bool_variables_0003.spy"
                return true;
            }

#line 25 "bool_variables_0003.spy"
            if (this.RequireBoth)
            {
#line 26 "bool_variables_0003.spy"
                return isAuthenticated && hasRole;
            }

#line 27 "bool_variables_0003.spy"
            return isAuthenticated || hasRole;
        }

        public StrictPolicy(bool enabled, bool requireBoth) : base(enabled)
        {
#line 19 "bool_variables_0003.spy"
            this.RequireBoth = requireBoth;
        }
    }

    public class PermissivePolicy : SecurityPolicy
    {
        public override bool Evaluate(bool isAuthenticated, bool hasRole)
        {
#line 35 "bool_variables_0003.spy"
            if (!this.Enabled)
            {
#line 36 "bool_variables_0003.spy"
                return true;
            }

#line 37 "bool_variables_0003.spy"
            return isAuthenticated || hasRole;
        }

        public PermissivePolicy(bool enabled) : base(enabled)
        {
        }
    }

    public class AccessManager
    {
        public SecurityPolicy Policy;
        public bool Locked;
        public bool AdminOverride;
        public bool CheckAccess(bool isAuthenticated, bool hasRole, bool isAdmin)
        {
#line 50 "bool_variables_0003.spy"
            if (this.Locked && !this.AdminOverride)
            {
#line 51 "bool_variables_0003.spy"
                return false;
            }

#line 52 "bool_variables_0003.spy"
            if (this.AdminOverride && isAdmin)
            {
#line 53 "bool_variables_0003.spy"
                return true;
            }

#line 54 "bool_variables_0003.spy"
            return this.Policy.Evaluate(isAuthenticated, hasRole);
        }

        public void ToggleLock()
        {
#line 57 "bool_variables_0003.spy"
            this.Locked = !this.Locked;
        }

        public AccessManager(SecurityPolicy policy, bool locked, bool adminOverride)
        {
#line 45 "bool_variables_0003.spy"
            this.Policy = policy;
#line 46 "bool_variables_0003.spy"
            this.Locked = locked;
#line 47 "bool_variables_0003.spy"
            this.AdminOverride = adminOverride;
        }
    }
}
