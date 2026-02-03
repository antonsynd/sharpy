#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.BoolVariables0002
{
    public static class Program
    {
        public static void Main()
        {
#line 28 "bool_variables_0002.spy"
            var validator = new FormValidator();
#line 30 "bool_variables_0002.spy"
            var result1 = validator.ValidateSubmission(true, true, true);
#line 31 "bool_variables_0002.spy"
            global::Sharpy.Core.Exports.Print(result1.IsApproved());
#line 32 "bool_variables_0002.spy"
            global::Sharpy.Core.Exports.Print(result1.NeedsReview());
#line 34 "bool_variables_0002.spy"
            var result2 = validator.ValidateSubmission(true, false, true);
#line 35 "bool_variables_0002.spy"
            global::Sharpy.Core.Exports.Print(result2.IsApproved());
#line 36 "bool_variables_0002.spy"
            global::Sharpy.Core.Exports.Print(result2.NeedsReview());
#line 38 "bool_variables_0002.spy"
            var result3 = validator.ValidateSubmission(true, true, false);
#line 39 "bool_variables_0002.spy"
            global::Sharpy.Core.Exports.Print(result3.IsApproved());
#line 40 "bool_variables_0002.spy"
            global::Sharpy.Core.Exports.Print(result3.NeedsReview());
        }
    }

    public class ValidationResult
    {
        public bool IsValid;
        public bool IsComplete;
        public bool HasErrors;
        public bool IsApproved()
        {
#line 14 "bool_variables_0002.spy"
            return this.IsValid && this.IsComplete && !this.HasErrors;
        }

        public bool NeedsReview()
        {
#line 17 "bool_variables_0002.spy"
            return !this.IsValid || this.HasErrors;
        }

        public ValidationResult(bool valid, bool complete, bool errors)
        {
#line 9 "bool_variables_0002.spy"
            this.IsValid = valid;
#line 10 "bool_variables_0002.spy"
            this.IsComplete = complete;
#line 11 "bool_variables_0002.spy"
            this.HasErrors = errors;
        }
    }

    public class FormValidator
    {
        public ValidationResult ValidateSubmission(bool hasName, bool hasEmail, bool hasConsent)
        {
#line 21 "bool_variables_0002.spy"
            var valid = hasName && hasEmail;
#line 22 "bool_variables_0002.spy"
            var complete = hasName && hasEmail && hasConsent;
#line 23 "bool_variables_0002.spy"
            var errors = !hasName || !hasEmail;
#line 25 "bool_variables_0002.spy"
            return new ValidationResult(valid, complete, errors);
        }
    }
}
