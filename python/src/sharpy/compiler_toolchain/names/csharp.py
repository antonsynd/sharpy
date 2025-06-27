from typing import Mapping, MutableMapping, MutableSet, Self, Set, Sequence, Optional


class _TypeBase:
    pass


class _NamespaceBase:
    """
    Base class for namespaces in the C# compiler toolchain. This is used to
    provide a common interface for the Namespace class.
    """

    def name(self) -> str:
        raise NotImplementedError

    def parent(self) -> Optional[Self]:
        raise NotImplementedError

    def namespaces(self) -> Mapping[str, Self]:
        raise NotImplementedError

    def symbols(self):
        raise NotImplementedError


class Type(_TypeBase):
    """
    Represents a C# type in the compiler toolchain. It can have fields
    which are actual members (instance or static) or nested types. C#
    requires that all names under a type are unique, which makes this easier
    to model.
    """

    def __init__(
        self, name: str, superclass: Optional[Self] = None, interfaces: Optional[Set[Self]] = None
    ) -> None:
        self._name: str = name
        self._namespace: _NamespaceBase = None  # This will be set by the Namespace class
        self._fields: MutableMapping[str, Self] = dict()
        # Note: This requires Python 3.10+ for set() to be ordered
        self._interfaces: MutableSet[Self] = interfaces if interfaces is not None else set()
        self._superclass: Optional[Self] = superclass

    def name(self) -> str:
        return self._name

    def namespace(self) -> _NamespaceBase:
        return self._namespace

    def superclass(self) -> Optional[Self]:
        return self._superclass

    def interfaces(self) -> Set[Self]:
        return set(self._interfaces)

    def add_interface(self, interface: Self) -> None:
        self._interfaces.add(interface)

    def fields(self) -> MutableMapping[str, Self]:
        return self._fields

    def add_field(self, name: str, type_: Self) -> None:
        self._fields[name] = type_

    def __eq__(self, value: object) -> bool:
        if not isinstance(value, Type):
            return False

        return self._name == value._name

    def __hash__(self) -> int:
        return hash(self._name)


class Symbols:
    """
    Represents the symbols in a C# namespace in the compiler toolchain.
    """

    def __init__(self, namespace: Namespace) -> None:
        self._namespace: Namespace = namespace
        self._types: MutableMapping[str, Type] = dict()

    def add_type(self, type_: Type) -> None:
        self._types[type_.name()] = type_

    def types(self) -> Mapping[str, Type]:
        return self._types


class Namespace:
    """
    Represents a namespace in C# in the compiler toolchain. A namespace can
    have any number of child namespaces and can contain symbols.
    """

    def __init__(self, name: str, parent: Optional[Self] = None) -> None:
        self._name: str = name
        self._parent: Optional[Self] = parent
        self._symbols: Symbols = Symbols(self)
        self._namespaces: MutableMapping[str, Self] = dict()

    def name(self) -> str:
        return self._name

    def parent(self) -> Optional[Self]:
        return self._parent

    def add_namespace(self, namespace: Self) -> None:
        self._namespaces[namespace.name()] = namespace
        namespace._parent = self

    def namespaces(self) -> Mapping[str, Self]:
        return self._namespaces

    def symbols(self) -> Symbols:
        return self._symbols


class Assembly:
    """
    Represents a C# assembly in the compiler toolchain. It contains a
    hierarchy of namespaces.
    """

    def __init__(self, name: str) -> None:
        self._name: str = name
        self._root: Namespace = Namespace(name="<root>", parent=None)

    def name(self) -> str:
        return self._name

    def namespaces(self) -> Mapping[str, Namespace]:
        return self._root.namespaces()

    def add_namespace(self, namespace: Namespace, parents: Sequence[str]) -> None:
        if not parents:
            # If no parents, add the namespace directly
            self._root.add_namespace(namespace)

        else:
            current_parent: Namespace = self._root

            for parent in parents:
                current_parent_opt: Optional[Namespace] = current_parent.namespaces().get(parent)

                if current_parent_opt:
                    current_parent = current_parent_opt
                else:
                    raise ValueError(
                        f'Parent namespace "{parent}" in {'.'.join(parents)} not found.'
                    )

            current_parent.add_namespace(namespace)
