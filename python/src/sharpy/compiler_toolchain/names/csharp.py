from typing import MutableSet, Self, Sequence


class Type:
    """Represents a C# type in the compiler toolchain."""

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


class Member:
    """Represents a member of a C# class or interface."""

    def __init__(self, name: str, type_: Type) -> None:
        self._name: str = name
        self._type: Type = type_

    def name(self) -> str:
        return self._name

    def type(self) -> Type:
        return self._type

    def __eq__(self, value: object) -> bool:
        if not isinstance(value, Member):
            return False

        return self._name == value._name and self._type == value._type

    def __hash__(self) -> int:
        return hash((self._name, self._type))


class Constructible(Type):
    """
    Represents a constructible type in C# in the compiler toolchain. This
    includes classes and structs.
    """

    def __init__(self, name: str) -> None:
        super().__init__(name)
        self._static: MutableSet[Member] = set()
        self._instance: MutableSet[Member] = set()

    def add_static(self, member: Member) -> None:
        self._static.add(member)

    def add_instance(self, member: Member) -> None:
        self._instance.add(member)

    def static_members(self) -> MutableSet[Member]:
        return self._static

    def instance_members(self) -> MutableSet[Member]:
        return self._instance

    def __eq__(self, value: object) -> bool:
        if not isinstance(value, Constructible):
            return False

        return (
            self._name == value._name
            and self._static == value._static
            and self._instance == value._instance
        )

    def __hash__(self) -> int:
        return hash((self._name, frozenset(self._static), frozenset(self._instance)))


class Interface:
    """Represents a C# interface in the compiler toolchain."""

    def __init__(self, name: str) -> None:
        self._name: str = name
        self._methods: MutableSet[Member] = set()

    def add_method(self, member: Member) -> None:
        self._methods.add(member)

    def methods(self) -> MutableSet[Member]:
        return self._methods

    def __eq__(self, value: object) -> bool:
        if not isinstance(value, Interface):
            return False

        return self._name == value._name and self._methods == value._methods

    def __hash__(self) -> int:
        return hash((self._name, frozenset(self._methods)))


class Enum(Type):
    """Represents a C# enum in the compiler toolchain."""

    def __init__(self, name: str) -> None:
        super().__init__(name)
        self._fields: MutableSet[str] = set()

    def add_field(self, field: str) -> None:
        self._fields.add(field)

    def fields(self) -> MutableSet[str]:
        return self._fields

    def __eq__(self, value: object) -> bool:
        if not isinstance(value, Enum):
            return False

        return self._name == value._name and self._fields == value._fields

    def __hash__(self) -> int:
        return hash((self._name, frozenset(self._fields)))


class Namespace:
    """
    Represents a C# namespace in the compiler toolchain. C# namespaces can only
    contain classes, interfaces, and enums, so it makes it relatively easy to
    model. The only difficulty is that C# classes can be static and hold
    static members.
    """

    def __init__(self, name: str) -> None:
        self._name: str = name
        self._namespaces: MutableSet[str] = set()
        self._constructibles: MutableSet[Constructible] = set()
        self._interfaces: MutableSet[Interface] = set()
        self._enums: MutableSet[Enum] = set()

    def add_namespace(self, namespace: str) -> None:
        # Note: To avoid complexity, we store nested namespaces as strings and
        # maintain the hierarchy in a separate structure. Even though C#
        # namespaces cannot have relative imports like Python modules can,
        # Sharpy does allow this, so we need to have the hierarchy in place.
        self._namespaces.add(namespace)

    def add_constructible(self, constructible: Constructible) -> None:
        self._constructibles.add(constructible)

    def add_interface(self, interface: Interface) -> None:
        self._interfaces.add(interface)

    def add_enum(self, enum: Enum) -> None:
        self._enums.add(enum)

    def name(self) -> str:
        return self._name

    def namespaces(self) -> MutableSet[str]:
        return self._namespaces

    def constructibles(self) -> MutableSet[Constructible]:
        return self._constructibles

    def interfaces(self) -> MutableSet[Interface]:
        return self._interfaces

    def enums(self) -> MutableSet[Enum]:
        return self._enums


class NamespaceNode:
    """Represents a node in a C# namespace hierarchy."""

    def __init__(self, namespace: Namespace) -> None:
        self._namespace: Namespace = namespace
        self._nested_namespaces: MutableSet[Self] = set()

    def add_namespace(self, namespace: Self) -> None:
        self._nested_namespaces.add(namespace)

    def namespace(self) -> Namespace:
        return self._namespace

    def nested_namespaces(self) -> MutableSet[Self]:
        return self._nested_namespaces

    def __hash__(self) -> int:
        return hash(self._namespace.name())


class DynamicLinkLibrary:
    """Represents a C# dynamic link library (DLL) in the compiler toolchain."""

    def __init__(self, name: str) -> None:
        self._name: str = name
        self._namespaces: MutableSet[NamespaceNode] = set()

    def name(self) -> str:
        return self._name

    def namespaces(self) -> MutableSet[NamespaceNode]:
        return self._namespaces

    def add_top_level_namespace(self, namespace: Namespace) -> None:
        self._namespaces.add(NamespaceNode(namespace))

    def add_nested_namespace(self, namespace: Namespace, parents: Sequence[Namespace]) -> None:
        pass
