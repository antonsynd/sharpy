import re
from typing import AbstractSet, Mapping, Optional, Sequence, Tuple

# Map of top-level Sharpy types to C# internal types.
TYPE_MAP: Mapping[str, str] = {
    "bool": "bool",
    "byte": "byte",
    "bytearray": "Sharpy.ByteArray",
    "bytes": "Sharpy.Bytes",
    "decimal": "decimal",
    "dict": "Sharpy.Dict",
    "double": "double",
    "int": "int",
    "float": "float",
    "frozenset": "Sharpy.FrozenSet",
    "list": "Sharpy.List",
    "long": "long",
    "None": "void",
    "object": "object",
    "sbyte": "sbyte",
    "set": "Sharpy.Set",
    "short": "short",
    "slice": "Sharpy.Slice",
    "str": "Sharpy.Str",
    "uint": "uint",
    "ulong": "ulong",
    "ushort": "ushort",
}

# Set of top-level Sharpy interfaces
TOP_LEVEL_INTERFACE_SET: AbstractSet[str] = {
    "Equatable",
    "Addable",
    "Multipliable",
    "InplaceMultipliable",
    "RightMultipliable",
    "InplaceAddable",
    "RightAddable",
    "GreaterThanComparable",
    "GreaterThanOrEquatable",
    "LessThanComparable",
    "LessThanOrEquatable",
    "Inequatable",
    "Representable",
    "Hashable",
    "StrConvertible",
    "BoolConvertible",
    "Identifiable",
}

NAMESPACED_INTERFACES: Mapping[str, AbstractSet[str]] = {
    "collections.interfaces": {
        "Collection",
        "Container",
        "ItemsView",
        "Iterable",
        "KeysView",
        "Mapping",
        "MappingView",
        "MutableMapping",
        "MutableSequence",
        "MutableSet",
        "Reversible",
        "Sequence",
        "Set",
        "Sized",
        "ValuesView",
    }
}


# Map of Sharpy types to the number of generic arguments expected
GENERIC_TYPE_VALENCE_MAP: Mapping[str, int] = {
    "dict": 2,
    "list": 1,
    "set": 1,
    "frozenset": 1,
    "Optional": 1,
}

template_pattern = re.compile(r"(array|list|dict|tuple|set|Optional)\[(.+)\]")

# Map of Sharpy dunder methods (without the double underscores) to either
# None if not supported in Sharpy, or a tuple of the C# internal dunder method
# name (without the double underscores) and optionally the C# operator it maps
# to.
DUNDER_NAMES: Mapping[str, Optional[Tuple[str, Optional[str]]]] = {
    "add": ("Add", "+"),
    "mul": ("Mul", "*"),
    "truediv": ("TrueDiv", None),
    "floordiv": ("FloorDiv", None),
    "sub": ("Sub", "-"),
    "mod": ("Mod", "%"),
    "pow": ("Pow", None),
    "iadd": ("IAdd", None),
    "imul": ("IMul", None),
    "itruediv": ("ITrueDiv", None),
    "ifloordiv": ("IFloorDiv", None),
    "isub": ("ISub", None),
    "imod": ("IMod", None),
    "ipow": ("IPOw", None),
    "radd": ("RAdd", None),
    "rmul": ("RMul", None),
    "rsub": ("RSub", None),
    "rtruediv": ("RTrueDiv", None),
    "rfloordiv": ("RFloorDiv", None),
    "rmod": ("RMod", None),
    "rpow": ("RPow", None),
    "contains": ("Contains", None),
    "eq": ("Eq", "=="),
    "ne": ("Ne", "!="),
    "lt": ("Lt", "<"),
    "le": ("Le", "<="),
    "gt": ("Gt", ">"),
    "ge": ("Ge", ">="),
    "id": ("Id", None),
    "hash": ("Hash", None),
    "iter": ("Iter", None),
    "next": ("Next", None),
    "str": ("Str", None),
    "bool": ("Bool", None),
    "repr": ("Repr", None),
    "len": ("Len", None),
    "call": ("Call", None),
    "and": ("And", "&&"),
    "or": ("Or", "||"),
    "xor": ("XOr", "^"),
    "lshift": ("LShift", "<<"),
    "rshift": ("RShift", ">>"),
    "invert": ("Invert", "~"),
    "iand": ("IAnd", None),
    "ior": ("IOr", None),
    "ixor": ("IXOr", None),
    "ilshift": ("ILShift", None),
    "irshift": ("IRShift", None),
    "dir": None,
    "hasattr": None,
    "instancecheck": None,
    "subclasscheck": None,
    "getattribute": None,
    "getattr": None,
    "setattr": None,
    "delattr": None,
    "get": None,
    "set": None,
    "delete": None,
    "set_name": None,
    "getitem": ("GetItem", None),
    "setitem": ("SetItem", None),
    "delitem": ("DelItem", None),
    "reversed": ("Reversed", None),
    "enter": ("Enter", None),
    "exit": ("Exit", None),
    "int": ("Int", None),
    "index": ("Index", None),
    "abs": ("Abs", None),
    "aiter": ("AIter", None),
    "anext": ("ANext", None),
    "float": ("Float", None),
    "complex": ("Complex", None),
    "trunc": ("Trunc", None),
    "round": ("Round", None),
    "dict": None,
    "init": None,
    "init_subclass": None,
    "new": None,
}

# Exceptions for converting snake case (Sharpy) to pascal case (C#) logic
NAME_SNAKE_CASE_TO_PASCAL_CASE_EXCEPTIONS_MAP: Mapping[str, str] = {}


def resolve_type_component(s: str, namespace: Optional[str] = None) -> str:
    maybe_type_name = TYPE_MAP.get(s)

    if maybe_type_name is not None:
        return maybe_type_name

    # Namespaced interfaces have "I" prepended
    if namespace is not None:
        maybe_namespace_interfaces: Optional[AbstractSet[str]] = NAMESPACED_INTERFACES.get(
            namespace
        )

        if maybe_namespace_interfaces is not None:
            return f"I{s}"

    # Top-level interfaces have "I" prepended
    if s in TOP_LEVEL_INTERFACE_SET:
        return f"I{s}"

    return "Sharpy.Object"


dunder_name_pattern = re.compile(r"__(\S+)__")


def resolve_name_component(s: str) -> str:
    if not s:
        return ""

    # @-prefixed names are treated literally
    if s[0] == "@":
        return s[1:]

    # Handle exceptions first
    maybe_name_exception: Optional[str] = NAME_SNAKE_CASE_TO_PASCAL_CASE_EXCEPTIONS_MAP.get(s)

    if maybe_name_exception is not None:
        return maybe_name_exception

    # Dunder names typically have exceptional pascal case names, so
    # they are all handled in this way
    match: Optional[re.Match[str]] = dunder_name_pattern.match(s)

    if match:
        maybe_dunder_name: str = match.group(1)
        dunder_name: Optional[Tuple[str, Optional[str]]] = DUNDER_NAMES.get(maybe_dunder_name)

        if dunder_name is not None:
            return dunder_name[0]

    # By default, use generic logic to convert snake case to pascal case
    return snake_case_to_pascal_case(s)


def snake_case_to_pascal_case(s: str) -> str:
    return "".join([x.title() for x in s.split("_")])
