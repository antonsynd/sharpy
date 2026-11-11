using System;

namespace Sharpy
{
    /// <summary>
    /// The stamp that ties one public door of <see cref="PathErrors"/> to the
    /// <see cref="PathRequirement"/> it answers for (plan-638e8b D1 predicate 4, #2476).
    /// </summary>
    /// <remarks>
    /// INTERNAL, like the enum it carries: neither reserves a name in Sharpy programs. Read by
    /// reflection only — G1's R0 (every public door stamped with exactly one requirement, every
    /// requirement with a door) and R2 (the requirement a door reference reaches) in
    /// <c>Sharpy.Stdlib.Tests</c>, which sees Core's internals through <c>InternalsVisibleTo</c>.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    internal sealed class PathRequirementAttribute : Attribute
    {
        /// <summary>The requirement the stamped door answers for.</summary>
        public PathRequirement Requirement { get; }

        /// <summary>Stamp a door with its requirement.</summary>
        public PathRequirementAttribute(PathRequirement requirement)
        {
            Requirement = requirement;
        }
    }
}
