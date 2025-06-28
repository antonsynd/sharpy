from typing import Mapping, MutableMapping, MutableSet, Optional, Self, Sequence, Set


class _NamespaceBase:
    """
    Abstract base class for namespaces in the C# compiler toolchain. This is
    used to avoid the circular reference in the `Type` class.
    """

    def __init__(self, name: str, parent: Optional[Self] = None) -> None:
        self._name: str = name
        self._parent: Optional[Self] = parent
        self._namespaces: MutableMapping[str, Self] = dict()

    def name(self) -> str:
        return self._name

    def parent(self) -> Optional[Self]:
        return self._parent

    def namespaces(self) -> Mapping[str, Self]:
        return self._namespaces

    def add_namespace(self, namespace: Self) -> None:
        self._namespaces[namespace.name()] = namespace
        namespace._parent = self

    def __eq__(self, value: object) -> bool:
        if not isinstance(value, Namespace):
            return False

        return self._name == value._name and self._parent == value._parent

    def __hash__(self) -> int:
        return hash(self._name) ^ (hash(self._parent) if self._parent else 0)


class Type:
    """
    Represents a C# type in the compiler toolchain. It can have fields
    which are actual members (instance or static) or nested types. C#
    requires that all names under a type are unique, which makes this easier
    to model.
    """

    def __init__(
        self,
        name: str,
        namespace: _NamespaceBase,
        superclass: Optional[Self] = None,
        interfaces: Optional[Set[Self]] = None,
    ) -> None:
        """
        This should not be used directly, but rather through the
        `Namespace` class which will create the type and add it to the
        namespace.
        """
        self._name: str = name
        self._namespace: _NamespaceBase = namespace
        self._fields: MutableMapping[str, Self] = dict()
        self._superclass: Optional[Self] = superclass
        # Note: This requires Python 3.10+ for set() to be ordered
        self._interfaces: MutableSet[Self] = interfaces if interfaces is not None else set()

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

        return self._name == value._name and self._namespace == value._namespace

    def __hash__(self) -> int:
        return hash(self._name)


class Namespace(_NamespaceBase):
    """
    Represents a namespace in C# in the compiler toolchain. A namespace can
    have any number of child namespaces and can contain symbols.
    """

    def __init__(self, name: str, parent: Optional[Self] = None) -> None:
        super().__init__(name, parent)
        self._types: MutableSet[Type] = set()

    def types(self) -> Set[Type]:
        return set(self._types)

    def add_type(
        self, name: str, superclass: Optional[Type] = None, interfaces: Optional[Set[Type]] = None
    ) -> Type:
        type_ = Type(name=name, namespace=self, superclass=superclass, interfaces=interfaces)
        self._types.add(type_)

        return type_


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

    def get_namespace(self, namespace: str) -> Optional[Namespace]:
        """
        Checks if the assembly has a namespace with the given name. If it does,
        it returns the namespace. If it does not, it returns None. The
        namespace should be a dot-separated string, e.g.
        "System.Collections.Generic".
        """
        parts: Sequence[str] = namespace.split(".")
        current_namespace: Optional[Namespace] = self._root

        for part in parts:
            current_namespace = current_namespace.namespaces().get(part)

            if not current_namespace:
                return None

        return current_namespace


class AssemblyBuilder:
    def __init__(self, name: str) -> None:
        self._assembly: Assembly = Assembly(name=name)

    def assembly(self) -> Assembly:
        return self._assembly

    def add_namespace(self, namespace: str) -> Namespace:
        """
        Adds a namespace to the assembly and returns it. The namespace should
        be a dot-separated string, e.g. "System.Collections.Generic". If the
        namespace exists, it will return the existing namespace.
        """
        if not namespace:
            raise ValueError("Namespace cannot be empty.")

        ns: Optional[Namespace] = self._assembly.get_namespace(namespace)

        if ns:
            return ns

        parts: Sequence[str] = namespace.split(".")

        if len(parts) == 1:
            new_namespace: Namespace = Namespace(name=parts[0], parent=None)
            self._assembly.add_namespace(namespace=new_namespace, parents=[])

            return new_namespace

        new_namespace: Namespace = Namespace(name=parts[-1], parent=None)
        parents: Sequence[str] = parts[:-1]

        self._assembly.add_namespace(namespace=new_namespace, parents=parents)

        return new_namespace

    def add_type(
        self,
        qname: str,
        superclass: Optional[Type] = None,
        interfaces: Optional[Set[Type]] = None,
    ) -> Type:
        """
        Adds a type to the assembly in the given namespace and returns it. If
        the namespace does not exist, it will be created.
        """
        if not qname:
            raise ValueError("Qualified type name cannot be empty.")

        parts: Sequence[str] = qname.split(".")

        if len(parts) < 2:
            raise ValueError("Type name must be qualified with a namespace.")

        type_name: str = parts[-1]
        namespace_name: str = ".".join(parts[:-1])

        ns: Namespace = self.add_namespace(namespace=namespace_name)
        type_ = ns.add_type(name=type_name, superclass=superclass, interfaces=interfaces)

        return type_
