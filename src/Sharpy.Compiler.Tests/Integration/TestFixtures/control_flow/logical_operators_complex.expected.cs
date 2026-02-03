#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.LogicalOperatorsComplex
{
    public static class Program
    {
        public static void Main()
        {
#line 58 "logical_operators_complex.spy"
            BiometricCredential bio = new BiometricCredential(true, true, false);
#line 59 "logical_operators_complex.spy"
            PinCredential pin = new PinCredential(true, true, 3);
#line 61 "logical_operators_complex.spy"
            global::Sharpy.Core.Exports.Print(bio.CheckPermission());
#line 62 "logical_operators_complex.spy"
            global::Sharpy.Core.Exports.Print(pin.CheckPermission());
#line 64 "logical_operators_complex.spy"
            SecuritySystem system = new SecuritySystem(false, false);
#line 65 "logical_operators_complex.spy"
            global::Sharpy.Core.Exports.Print(system.GrantAccess(bio, false));
#line 66 "logical_operators_complex.spy"
            global::Sharpy.Core.Exports.Print(system.GrantAccess(pin, false));
#line 68 "logical_operators_complex.spy"
            BiometricCredential failedBio = new BiometricCredential(true, false, false);
#line 69 "logical_operators_complex.spy"
            global::Sharpy.Core.Exports.Print(failedBio.CheckPermission());
#line 70 "logical_operators_complex.spy"
            global::Sharpy.Core.Exports.Print(system.GrantAccess(failedBio, true));
#line 72 "logical_operators_complex.spy"
            SecuritySystem armedSystem = new SecuritySystem(true, true);
#line 73 "logical_operators_complex.spy"
            global::Sharpy.Core.Exports.Print(armedSystem.GrantAccess(pin, false));
#line 74 "logical_operators_complex.spy"
            global::Sharpy.Core.Exports.Print(armedSystem.GrantAccess(null, true));
        }
    }

    public abstract class SecurityCredential
    {
        public bool IsValid;
        public abstract bool CheckPermission();
        public SecurityCredential(bool valid)
        {
#line 9 "logical_operators_complex.spy"
            this.IsValid = valid;
        }
    }

    public class BiometricCredential : SecurityCredential
    {
        public bool FingerprintMatch;
        public bool FaceMatch;
        public override bool CheckPermission()
        {
#line 26 "logical_operators_complex.spy"
            return this.IsValid && (this.FingerprintMatch || this.FaceMatch);
        }

        public BiometricCredential(bool valid, bool fingerprint, bool face) : base(valid)
        {
#line 21 "logical_operators_complex.spy"
            this.FingerprintMatch = fingerprint;
#line 22 "logical_operators_complex.spy"
            this.FaceMatch = face;
        }
    }

    public class PinCredential : SecurityCredential
    {
        public bool PinCorrect;
        public int AttemptsLeft;
        public override bool CheckPermission()
        {
#line 39 "logical_operators_complex.spy"
            return this.IsValid && this.PinCorrect && this.AttemptsLeft > 0;
        }

        public PinCredential(bool valid, bool pin, int attempts) : base(valid)
        {
#line 34 "logical_operators_complex.spy"
            this.PinCorrect = pin;
#line 35 "logical_operators_complex.spy"
            this.AttemptsLeft = attempts;
        }
    }

    public class SecuritySystem
    {
        public bool HasEmergencyOverride;
        public bool SystemArmed;
        public bool GrantAccess(SecurityCredential? credential, bool backupCode)
        {
#line 50 "logical_operators_complex.spy"
            if (credential != null && !this.SystemArmed)
            {
#line 51 "logical_operators_complex.spy"
                return credential.CheckPermission() || backupCode;
            }
            else if (this.HasEmergencyOverride && !this.SystemArmed)
            {
#line 53 "logical_operators_complex.spy"
                return true;
            }
            else
            {
#line 55 "logical_operators_complex.spy"
                return false && !(true || false);
            }
        }

        public SecuritySystem(bool emergency, bool armed)
        {
#line 46 "logical_operators_complex.spy"
            this.HasEmergencyOverride = emergency;
#line 47 "logical_operators_complex.spy"
            this.SystemArmed = armed;
        }
    }
}
