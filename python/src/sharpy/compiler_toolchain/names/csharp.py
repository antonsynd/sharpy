from typing import MutableSet


class Class:
    """Represents a C# class in the compiler toolchain."""

    def __init__(self, name: str) -> None:
        self.name: str = name

    def add_static(self, name: str) -> None:
        """Adds a static member to the class."""
        pass

    def add_instance(self, name: str) -> None:
        """Adds a instance member to the class."""
        pass


class Namespace:
    """
    Represents a C# namespace in the compiler toolchain. C# namespaces can only
    contain classes, interfaces, and enums, so it makes it relatively easy to
    model. The only difficulty is that C# classes can be static and hold
    static members.
    """

    def __init__(self, name: str) -> None:
        self._name: str = name
        self._classes: MutableSet[Class] = set()
        self._interfaces: MutableSet[str] = set()
        self._enums: MutableSet[str] = set()

    def add_class(self, class_: Class) -> None:
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

    def classes(self) -> MutableSet[Class]:
        """Returns the classes in the namespace."""
        return self._classes

    def interfaces(self) -> MutableSet[str]:
        """Returns the interfaces in the namespace."""
        return self._interfaces

    def enums(self) -> MutableSet[str]:
        """Returns the enums in the namespace."""
        return self._enums
