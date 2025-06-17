from enum import Enum, auto
from typing import Any, MutableSet, Optional, Self, Sequence


def snake_case_to_pascal_case(s: str) -> str:
    """Converts a snake_case string to PascalCase."""
    return "".join([x.title() for x in s.split("_")])


def snake_case_to_camel_case(s: str) -> str:
    """Converts a snake_case string to camelCase."""
    parts: Sequence[str] = s.split("_")

    if not parts:
        return ""

    return parts[0] + "".join(part.title() for part in parts[1:])


class IdentifierType(Enum):
    """Enum for different types of names in the compiler toolchain."""

    MODULE = auto()
    # Covers methods and free functions
    FUNCTION = auto()
    DECORATOR = auto()
    # For enums
    FIELD = auto()

    STORAGE = auto()
    COLLECTION = auto()


class StorageType(Enum):
    """Enum for different types of storage in the compiler toolchain."""

    VARIABLE = auto()
    MEMBER = auto()
    CONSTANT = auto()
    PARAMETER = auto()


class CollectionType(Enum):
    """Enum for different types of collections in the compiler toolchain."""

    STRUCT = auto()
    PROTOCOL = auto()
    CLASS = auto()
    ENUM = auto()


class SharpyNameType:
    def __init__(
        self,
        identifier_type: IdentifierType,
        storage_type: Optional[StorageType] = None,
        collection_type: Optional[CollectionType] = None,
    ) -> None:
        self._identifier_type: IdentifierType = identifier_type
        self._storage_type: Optional[StorageType] = storage_type
        self._collection_type: Optional[CollectionType] = collection_type

    @property
    def identifier_type(self) -> IdentifierType:
        """Returns the identifier type of the name."""
        return self._identifier_type

    @property
    def storage_type(self) -> Optional[StorageType]:
        """Returns the storage type of the name, if applicable."""
        return self._storage_type

    @storage_type.setter
    def storage_type(self, value: Optional[StorageType]) -> None:
        """Sets the storage type of the name."""
        self._storage_type = value

    @property
    def collection_type(self) -> Optional[CollectionType]:
        """Returns the collection type of the name, if applicable."""
        return self._collection_type

    @collection_type.setter
    def collection_type(self, value: Optional[CollectionType]) -> None:
        """Sets the collection type of the name."""
        self._collection_type = value


class SharpyModule:
    """Represents a Sharpy module in the compiler toolchain."""
    def __init__(self, name: str) -> None:
        pass

    def add_function(self, name: str) -> None:
        """Adds a function to the module."""
        pass

    def add_class(self, name: str) -> None:
        """Adds a class to the module."""
        pass

    def add_struct(self, name: str) -> None:
        """Adds a struct to the module."""
        pass

    def add_protocol(self, name: str) -> None:
        """Adds a protocol to the module."""
        pass

    def add_enum(self, name: str) -> None:
        """Adds an enum to the module."""
        pass

    def add_constant(self, name: str) -> None:
        """Adds a constant to the module."""
        pass

    def add_variable(self, name: str) -> None:
        """Adds a variable to the module."""
        pass


class CSharpClass:
    """Represents a C# class in the compiler toolchain."""
    def __init__(self, name: str) -> None:
        self.name: str = name

    def add_static(self, name: str) -> None:
        """Adds a static member to the class."""
        pass

    def add_instance(self, name: str) -> None:
        """Adds a instance member to the class."""
        pass


class CSharpNamespace:
    """
    Represents a C# namespace in the compiler toolchain. C# namespaces can only
    contain classes, interfaces, and enums, so it makes it relatively easy to
    model. The only difficulty is that C# classes can be static and hold
    static members.
    """
    def __init__(self, name: str) -> None:
        self._name: str = name
        self._classes: MutableSet[CSharpClass] = set()
        self._interfaces: MutableSet[str] = set()
        self._enums: MutableSet[str] = set()

    def add_class(self, class_: CSharpClass) -> None:
        """Adds a class to the namespace."""
        self._classes.add(class_)

    def add_interface(self, name: str) -> None:
        """Adds an interface to the namespace."""
        self._interfaces.add(name)

    def add_enum(self, name: str) -> None:
        """Adds an enum to the namespace."""
        self._enums.add(name)

    def name(self) -> str:
        """Returns the name of the namespace."""
        return self._name

    def classes(self) -> MutableSet[CSharpClass]:
        """Returns the classes in the namespace."""
        return self._classes

    def interfaces(self) -> MutableSet[str]:
        """Returns the interfaces in the namespace."""
        return self._interfaces

    def enums(self) -> MutableSet[str]:
        """Returns the enums in the namespace."""
        return self._enums


class Context:
    def __init__(self) -> None:
        self._namespaces: MutableSet[CSharpNamespace] = set()
        self._modules: MutableSet[SharpyModule] = set()

    def add_namespace(self, namespace: CSharpNamespace) -> None:
        """Adds a C# namespace to the context."""
        self._namespaces.add(namespace)

    def add_module(self, module: SharpyModule) -> None:
        """Adds a Sharpy module to the context."""
        self._modules.add(module)

    def namespaces(self) -> MutableSet[CSharpNamespace]:
        """Returns the C# namespaces in the context."""
        return self._namespaces

    def modules(self) -> MutableSet[SharpyModule]:
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


class SharpyName:
    def __init__(self, type_: SharpyNameType, name: str) -> None:
        self._type: SharpyNameType = type_
        self._name: str = name

    def name_type(self) -> SharpyNameType:
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
                return context.resolve_storage(self._name, self._type.storage_type)
            case IdentifierType.COLLECTION:
                return context.resolve_collection(self._name, self._type.collection_type)
            case _:
                raise ValueError(f"Unknown identifier type: {self._type.identifier_type}")


class SharpyQName:
    def __init__(
        self,
        type_: SharpyNameType,
        name: SharpyName,
        qualifiers: Optional[Sequence[SharpyName]] = None,
    ) -> None:
        self._type: SharpyNameType = type_
        self._name: SharpyName = name
        self._qualifiers: Sequence[SharpyName] = qualifiers if qualifiers else list()

    def name_type(self) -> SharpyNameType:
        """Returns the type of the qualified name."""
        return self._type

    def name(self) -> SharpyName:
        """Returns the base name of the qualified name."""
        return self._name

    def qualifiers(self) -> Sequence[SharpyName]:
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
