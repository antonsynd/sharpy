// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Compiler", "CS0660:Type defines operator == or operator != but does not override Object.Equals(object o)", Justification = "Test helper class - equality semantics are intentionally simplified", Scope = "type", Target = "~T:Sharpy.Core.Tests.IdentityWrapper`1")]
[assembly: SuppressMessage("Compiler", "CS0661:Type defines operator == or operator != but does not override Object.GetHashCode()", Justification = "Test helper class - equality semantics are intentionally simplified", Scope = "type", Target = "~T:Sharpy.Core.Tests.IdentityWrapper`1")]
[assembly: SuppressMessage("Compiler", "CS0660:Type defines operator == or operator != but does not override Object.Equals(object o)", Justification = "Test helper class - equality semantics are intentionally simplified", Scope = "type", Target = "~T:Sharpy.Core.Tests.Wrapper`1")]
[assembly: SuppressMessage("Compiler", "CS0661:Type defines operator == or operator != but does not override Object.GetHashCode()", Justification = "Test helper class - equality semantics are intentionally simplified", Scope = "type", Target = "~T:Sharpy.Core.Tests.Wrapper`1")]
[assembly: SuppressMessage("Design", "CA1067:Override Object.Equals(object) when implementing IEquatable<T>", Justification = "Test helper class - equality semantics are intentionally simplified", Scope = "type", Target = "~T:Sharpy.Core.Tests.Wrapper`1")]
