using System.Collections.Frozen;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Categorizes protocol dunders by their semantic purpose.
/// </summary>
public enum ProtocolKind
{
    Lifecycle,      // __init__, __del__, __new__
    Container,      // __len__, __contains__, __getitem__, __setitem__, __delitem__
    Iterator,       // __iter__, __next__
    Representation, // __str__, __repr__, __format__
    Hashing,        // __hash__
    Conversion      // __bool__, __int__, __float__, __complex__
}

/// <summary>
/// Describes a protocol dunder method and its mappings.
/// </summary>
/// <param name="DunderName">Lowercase Sharpy source name (e.g., "__len__")</param>
/// <param name="Kind">The protocol category</param>
/// <param name="SharpyCoreInterface">The Sharpy.Core interface name (e.g., "ISized"), or null if no interface</param>
/// <param name="InterfaceMethodName">Sharpy.Core method name preserving dunder format with capitalized inner portion (e.g., "__Len__", "__GetItem__"), or null if no interface method</param>
/// <param name="ClrMethodName">The .NET method/property name (e.g., "get_Count"), or null if no direct mapping</param>
/// <param name="ExpectedParamCount">Expected parameter count including 'self'</param>
/// <param name="ExpectedReturnType">Expected return type name (e.g., "int", "bool", "str"), or null for any</param>
public record ProtocolInfo(
    string DunderName,
    ProtocolKind Kind,
    string? SharpyCoreInterface,
    string? InterfaceMethodName,
    string? ClrMethodName,
    int ExpectedParamCount,
    string? ExpectedReturnType
);

/// <summary>
/// Central registry of protocol dunder methods and their mappings to Sharpy.Core interfaces.
/// Excludes operator dunders which are handled by <see cref="OperatorRegistry"/>.
/// </summary>
public static class ProtocolRegistry
{
    private static readonly FrozenDictionary<string, ProtocolInfo> _protocols;

    static ProtocolRegistry()
    {
        var protocols = new Dictionary<string, ProtocolInfo>();
        RegisterAllProtocols(protocols);
        _protocols = protocols.ToFrozenDictionary();
    }

    private static void Register(Dictionary<string, ProtocolInfo> protocols, ProtocolInfo info)
    {
        protocols[info.DunderName] = info;
    }

    private static void RegisterAllProtocols(Dictionary<string, ProtocolInfo> protocols)
    {
        // 2.2.1 Lifecycle protocols
        Register(protocols, new ProtocolInfo(
            DunderName: "__init__",
            Kind: ProtocolKind.Lifecycle,
            SharpyCoreInterface: null,  // Special: maps to constructor
            InterfaceMethodName: null,  // No interface method; constructor is special-cased
            ClrMethodName: ".ctor",
            ExpectedParamCount: -1,  // Variable (1+ including self)
            ExpectedReturnType: "None"  // Constructors return void
        ));

        // 2.2.2 Container protocols
        // NOTE: ISized.__Len__() returns uint in Sharpy.Core, but we register "int" here
        // because that's the common Sharpy/Python return type for len(). The code generator
        // handles the uint-to-int conversion when emitting calls to __Len__().
        Register(protocols, new ProtocolInfo(
            DunderName: "__len__",
            Kind: ProtocolKind.Container,
            SharpyCoreInterface: "ISized",
            InterfaceMethodName: "__Len__",
            ClrMethodName: "get_Count",  // Maps to Count property in .NET
            ExpectedParamCount: 1,  // Just self
            ExpectedReturnType: "int"  // Sharpy uses int; codegen handles uint conversion
        ));

        Register(protocols, new ProtocolInfo(
            DunderName: "__contains__",
            Kind: ProtocolKind.Container,
            SharpyCoreInterface: "IContainer",
            InterfaceMethodName: "__Contains__",
            ClrMethodName: "Contains",
            ExpectedParamCount: 2,  // self, item
            ExpectedReturnType: "bool"
        ));

        Register(protocols, new ProtocolInfo(
            DunderName: "__getitem__",
            Kind: ProtocolKind.Container,
            SharpyCoreInterface: "ISequence",
            InterfaceMethodName: "__GetItem__",
            ClrMethodName: "get_Item",  // Maps to indexer property
            ExpectedParamCount: 2,  // self, key/index
            ExpectedReturnType: null  // Returns element type (generic)
        ));

        Register(protocols, new ProtocolInfo(
            DunderName: "__setitem__",
            Kind: ProtocolKind.Container,
            SharpyCoreInterface: "IMutableSequence",
            InterfaceMethodName: "__SetItem__",
            ClrMethodName: "set_Item",  // Maps to indexer property setter
            ExpectedParamCount: 3,  // self, key/index, value
            ExpectedReturnType: "None"
        ));

        Register(protocols, new ProtocolInfo(
            DunderName: "__delitem__",
            Kind: ProtocolKind.Container,
            SharpyCoreInterface: "IMutableSequence",
            InterfaceMethodName: "__DelItem__",
            ClrMethodName: null,  // No direct .NET equivalent
            ExpectedParamCount: 2,  // self, key/index
            ExpectedReturnType: "None"
        ));

        // 2.2.3 Iterator protocols
        Register(protocols, new ProtocolInfo(
            DunderName: "__iter__",
            Kind: ProtocolKind.Iterator,
            SharpyCoreInterface: "IIterable",
            InterfaceMethodName: "__Iter__",
            ClrMethodName: "GetEnumerator",
            ExpectedParamCount: 1,  // Just self
            ExpectedReturnType: null  // Returns Iterator<T> (generic)
        ));

        Register(protocols, new ProtocolInfo(
            DunderName: "__next__",
            Kind: ProtocolKind.Iterator,
            SharpyCoreInterface: null,  // Part of Iterator<T> class, not an interface
            InterfaceMethodName: "__Next__",
            ClrMethodName: null,  // No direct .NET equivalent (MoveNext returns bool, __next__ returns element or raises)
            ExpectedParamCount: 1,  // Just self
            ExpectedReturnType: null  // Returns element type (generic)
        ));

        // 2.2.4 Representation protocols
        Register(protocols, new ProtocolInfo(
            DunderName: "__str__",
            Kind: ProtocolKind.Representation,
            SharpyCoreInterface: "IStrConvertible",
            InterfaceMethodName: "__Str__",
            ClrMethodName: "ToString",
            ExpectedParamCount: 1,  // Just self
            ExpectedReturnType: "str"
        ));

        Register(protocols, new ProtocolInfo(
            DunderName: "__repr__",
            Kind: ProtocolKind.Representation,
            SharpyCoreInterface: "IRepresentable",
            InterfaceMethodName: "__Repr__",
            ClrMethodName: null,  // No direct .NET equivalent; generates __Repr__() method
            ExpectedParamCount: 1,  // Just self
            ExpectedReturnType: "str"
        ));

        // 2.2.5 Hashing protocols
        Register(protocols, new ProtocolInfo(
            DunderName: "__hash__",
            Kind: ProtocolKind.Hashing,
            SharpyCoreInterface: "IHashable",
            InterfaceMethodName: "__Hash__",
            ClrMethodName: "GetHashCode",
            ExpectedParamCount: 1,  // Just self
            ExpectedReturnType: "int"
        ));

        // 2.2.6 Conversion protocols
        Register(protocols, new ProtocolInfo(
            DunderName: "__bool__",
            Kind: ProtocolKind.Conversion,
            SharpyCoreInterface: "IBoolConvertible",
            InterfaceMethodName: "__Bool__",
            ClrMethodName: "op_Explicit",  // Explicit bool conversion operator
            ExpectedParamCount: 1,  // Just self
            ExpectedReturnType: "bool"
        ));

        // Note: __eq__ and __ne__ are handled by OperatorRegistry as they
        // are comparison operators that map to .NET operator overloads.
        // However, they also integrate with Sharpy.Core.Object for equality semantics.
    }

    // ==================== 2.3 Query Methods ====================

    /// <summary>
    /// Gets the protocol info for a dunder method name, or null if not a protocol dunder.
    /// </summary>
    public static ProtocolInfo? GetProtocol(string dunderName)
        => _protocols.GetValueOrDefault(dunderName);

    /// <summary>
    /// Checks if a method name is a recognized protocol dunder method.
    /// This excludes operator dunders, which are handled by <see cref="OperatorRegistry"/>.
    /// </summary>
    public static bool IsProtocolDunder(string name)
        => _protocols.ContainsKey(name);

    /// <summary>
    /// Returns all registered protocol dunders for iteration.
    /// </summary>
    public static IEnumerable<ProtocolInfo> GetAllProtocols()
        => _protocols.Values;

    /// <summary>
    /// Returns all protocols of a specific kind.
    /// </summary>
    public static IEnumerable<ProtocolInfo> GetProtocolsByKind(ProtocolKind kind)
        => _protocols.Values.Where(p => p.Kind == kind);

    /// <summary>
    /// Gets the Sharpy.Core interface name for a dunder, or null if not applicable.
    /// </summary>
    public static string? GetInterfaceName(string dunderName)
        => GetProtocol(dunderName)?.SharpyCoreInterface;

    /// <summary>
    /// Gets the .NET method/property name for a dunder, or null if no direct mapping.
    /// </summary>
    public static string? GetClrMethodName(string dunderName)
        => GetProtocol(dunderName)?.ClrMethodName;

    /// <summary>
    /// Checks if a dunder method has a specific expected return type.
    /// Returns true if the dunder has any return type constraint, false otherwise.
    /// </summary>
    public static bool HasReturnTypeConstraint(string dunderName)
        => GetProtocol(dunderName)?.ExpectedReturnType != null;

    /// <summary>
    /// Gets the count of registered protocols.
    /// </summary>
    public static int Count => _protocols.Count;

    /// <summary>
    /// Reverse lookup: Gets a dunder name for a given Sharpy.Core interface name.
    /// Returns null if no protocol is associated with the interface.
    /// </summary>
    /// <remarks>
    /// If multiple protocols map to the same interface (e.g., __setitem__ and __delitem__ 
    /// both map to IMutableSequence), this returns the first match found. For exhaustive 
    /// lookup, use <see cref="GetAllProtocols"/> and filter by interface.
    /// </remarks>
    public static string? GetDunderForInterface(string interfaceName)
        => _protocols.Values
            .Where(p => p.SharpyCoreInterface == interfaceName)
            .Select(p => p.DunderName)
            .FirstOrDefault();

    /// <summary>
    /// Checks if the given method name is any registered dunder (protocol or operator).
    /// This combines <see cref="IsProtocolDunder"/> and <see cref="OperatorRegistry.IsOperatorDunder"/>.
    /// </summary>
    public static bool IsAnyDunder(string methodName)
        => IsProtocolDunder(methodName) || OperatorRegistry.IsOperatorDunder(methodName);

    /// <summary>
    /// Gets the expected parameter count and return type for a dunder name.
    /// Returns null if the dunder is not registered.
    /// </summary>
    public static (int ParamCount, string? ReturnType)? GetExpectedSignature(string dunderName)
    {
        var info = GetProtocol(dunderName);
        if (info == null)
            return null;
        return (info.ExpectedParamCount, info.ExpectedReturnType);
    }

    /// <summary>
    /// Checks if a dunder method overrides a System.Object virtual method.
    /// These dunders require the @override decorator in Sharpy source code:
    /// - __str__ → Object.ToString()
    /// - __eq__ → Object.Equals()
    /// - __hash__ → Object.GetHashCode()
    /// </summary>
    public static bool IsObjectOverrideDunder(string methodName)
        => methodName is "__str__" or "__eq__" or "__hash__";
}
