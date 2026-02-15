#if NETSTANDARD2_0 || NETSTANDARD2_1
// Licensed to the .NET Foundation under one or more agreements.
// Polyfill for init-only properties on netstandard2.0/2.1
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}
#endif
