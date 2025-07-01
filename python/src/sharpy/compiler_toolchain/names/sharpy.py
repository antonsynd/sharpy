from enum import Enum, auto
from typing import MutableSet, Optional, Sequence

import sharpy.compiler_toolchain.names.csharp as csharp


def snake_case_to_pascal_case(s: str) -> str:
    """Converts a snake_case string to PascalCase."""
    if not s:
        return ""

    parts: Sequence[str] = s.split("_")

    if len(parts) == 1:
        return parts[0].title()

    return "".join([x.title() for x in parts])


def snake_case_to_camel_case(s: str) -> str:
    """Converts a snake_case string to camelCase."""
    if not s:
        return ""

    parts: Sequence[str] = s.split("_")

    if len(parts) == 1:
        return parts[0].lower()

    return parts[0].lower() + "".join(part.title() for part in parts[1:])


class IdentifierType(Enum):
    """
    Enum for different types of names in the compiler toolchain. This includes
    things like modules and functions.
    """

    MODULE = auto()
    # Covers methods and free functions
    FUNCTION = auto()
    DECORATOR = auto()
    # For enums
    FIELD = auto()

    STORAGE = auto()
    COLLECTION = auto()


class StorageType(Enum):
    """
    Enum for different types of storage in the compiler toolchain. This covers
    variables, members, constants, and parameters.
    """

    VARIABLE = auto()
    MEMBER = auto()
    CONSTANT = auto()
    PARAMETER = auto()


class CollectionType(Enum):
    """
    Enum for different types of collections in the compiler toolchain.
    Collections are structs, protocols, classes, or enums.
    """

    STRUCT = auto()
    PROTOCOL = auto()
    CLASS = auto()
    ENUM = auto()


class NameType:
    def __init__(
        self,
        identifier_type: IdentifierType,
        storage_type: Optional[StorageType] = None,
        collection_type: Optional[CollectionType] = None,
    ) -> None:
        self._identifier_type: IdentifierType = identifier_type
        self._storage_type: Optional[StorageType] = storage_type
        self._collection_type: Optional[CollectionType] = collection_type

    def identifier_type(self) -> IdentifierType:
        return self._identifier_type

    def storage_type(self) -> Optional[StorageType]:
        return self._storage_type

    def set_storage_type(self, value: Optional[StorageType]) -> None:
        self._storage_type = value

    def collection_type(self) -> Optional[CollectionType]:
        return self._collection_type

    def set_collection_type(self, value: Optional[CollectionType]) -> None:
        self._collection_type = value


class Type:
    """Represents a Sharpy type in the compiler toolchain."""

    def __init__(self, name: str) -> None:
        self._name: str = name

    def name(self) -> str:
        return self._name

    def __eq__(self, value: object) -> bool:
        if not isinstance(value, Type):
            return False

        return self._name == value._name

    def __hash__(self) -> int:
        return hash(self._name)


class TypedName:
    """Represents a typed name in Sharpy in the compiler toolchain."""

    def __init__(self, name: str, type_: Type) -> None:
        self._name: str = name
        self._type: Type = type_

    def name(self) -> str:
        return self._name

    def type(self) -> Type:
        return self._type

    def __eq__(self, value: object) -> bool:
        if not isinstance(value, TypedName):
            return False

        return self._name == value._name and self._type == value._type

    def __hash__(self) -> int:
        return hash((self._name, self._type))


class Module:
    """Represents a Sharpy module in the compiler toolchain."""

    def __init__(self, name: str) -> None:
        self._name: str = name
        self._modules: MutableSet[str] = set()
        self._functions: MutableSet[TypedName] = set()
        self._classes: MutableSet[TypedName] = set()
        self._structs: MutableSet[TypedName] = set()
        self._protocols: MutableSet[TypedName] = set()
        self._enums: MutableSet[TypedName] = set()
        self._constants: MutableSet[TypedName] = set()
        self._variables: MutableSet[TypedName] = set()

    def name(self) -> str:
        return self._name

    def add_module(self, name: str) -> None:
        # Note: To avoid complexity, we store nested modules as strings and
        # maintain the hierarchy in a separate structure.
        self._modules.add(name)

    def add_function(self, name: TypedName) -> None:
        self._functions.add(name)

    def add_class(self, name: TypedName) -> None:
        self._classes.add(name)

    def add_struct(self, name: TypedName) -> None:
        self._structs.add(name)

    def add_protocol(self, name: TypedName) -> None:
        self._protocols.add(name)

    def add_enum(self, name: TypedName) -> None:
        self._enums.add(name)

    def add_constant(self, name: TypedName) -> None:
        self._constants.add(name)

    def add_variable(self, name: TypedName) -> None:
        self._variables.add(name)

    def modules(self) -> MutableSet[str]:
        return self._modules

    def functions(self) -> MutableSet[TypedName]:
        return self._functions

    def classes(self) -> MutableSet[TypedName]:
        return self._classes

    def structs(self) -> MutableSet[TypedName]:
        return self._structs

    def protocols(self) -> MutableSet[TypedName]:
        return self._protocols

    def enums(self) -> MutableSet[TypedName]:
        return self._enums

    def constants(self) -> MutableSet[TypedName]:
        return self._constants

    def variables(self) -> MutableSet[TypedName]:
        return self._variables


class Context:
    def __init__(self) -> None:
        self._namespaces: MutableSet[csharp.Namespace] = set()
        self._modules: MutableSet[Module] = set()

    def add_namespace(self, namespace: csharp.Namespace) -> None:
        """Adds a C# namespace to the context."""
        self._namespaces.add(namespace)

    def add_module(self, module: Module) -> None:
        """Adds a Sharpy module to the context."""
        self._modules.add(module)

    def namespaces(self) -> MutableSet[csharp.Namespace]:
        """Returns the C# namespaces in the context."""
        return self._namespaces

    def modules(self) -> MutableSet[Module]:
        """Returns the Sharpy modules in the context."""
        return self._modules

    def is_literal(self, name: str) -> bool:
        """Checks if the name is a literal beginning with $."""
        return bool(name) and name[0] == "$"

    def resolve_field(self, name: str) -> str:
        # Enum fields are returned as-is
        return name

    def resolve_module(self, name: str) -> str:
        # Modules are PascalCase
        return snake_case_to_pascal_case(name)

    def resolve_function(self, name: str) -> str:
        # Functions are PascalCase
        return snake_case_to_pascal_case(name)

    def resolve_storage(self, name: str, type_: Optional[StorageType]) -> str:
        match type_:
            case StorageType.MEMBER:
                # Members of collections must be PascalCase
                return snake_case_to_pascal_case(name)
            case _:
                return name

    def resolve_collection(self, name: str, type_: Optional[CollectionType]) -> str:
        match type_:
            case CollectionType.PROTOCOL:
                # Protocols are prefixed with "I" matching C# interfaces
                return f"I{name}"
            case _:
                return name


class Name:
    def __init__(self, type_: NameType, name: str) -> None:
        self._type: NameType = type_
        self._name: str = name

    def name_type(self) -> NameType:
        """Returns the type of the name."""
        return self._type

    def serialize(self, context: Context) -> str:
        match self._type.identifier_type:
            case IdentifierType.MODULE:
                return context.resolve_module(self._name)
            case IdentifierType.FUNCTION:
                return context.resolve_function(self._name)
            case IdentifierType.DECORATOR:
                raise NotImplementedError(
                    "Decorators are not yet implemented in the name serialization."
                )
            case IdentifierType.FIELD:
                return context.resolve_field(self._name)
            case IdentifierType.STORAGE:
                return context.resolve_storage(self._name, self._type.storage_type())
            case IdentifierType.COLLECTION:
                return context.resolve_collection(self._name, self._type.collection_type())
            case _:
                raise ValueError(f"Unknown identifier type: {self._type.identifier_type()}")


class QName:
    def __init__(
        self,
        type_: NameType,
        name: Name,
        qualifiers: Optional[Sequence[Name]] = None,
    ) -> None:
        self._type: NameType = type_
        self._name: Name = name
        self._qualifiers: Sequence[Name] = qualifiers if qualifiers else list()

    def name_type(self) -> NameType:
        """Returns the type of the qualified name."""
        return self._type

    def name(self) -> Name:
        """Returns the base name of the qualified name."""
        return self._name

    def qualifiers(self) -> Sequence[Name]:
        """Returns the qualifiers of the qualified name."""
        return self._qualifiers

    def serialize(self, context: Context) -> str:
        base_str: str = self._name.serialize(context=context)

        if not self._qualifiers:
            return base_str

        qualifiers_str: str = ".".join(
            qualifier.serialize(context=context) for qualifier in self._qualifiers
        )

        return f"{base_str}.{qualifiers_str}"
