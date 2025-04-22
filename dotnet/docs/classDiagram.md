```mermaid
classDiagram
    %% Classes
    class System.Object
    class Sharpy.Object
    class Sharpy.List~T~

    %% Interfaces
    class Sharpy.IHashable
    class Sharpy.IEquatable
    class Sharpy.IEquatableWith~T~
    class Sharpy.IEquatable~T~
    class System.IEquatable~T~

    %% Inheritance relationships
    Sharpy.Object --|> System.Object
    Sharpy.List~T~ --|> Sharpy.Object
    Sharpy.List~T~ ..|> Sharpy.IEquatable~T~
    Sharpy.IEquatable~T~ --|> Sharpy.IEquatableWith~T~
    Sharpy.IEquatableWith~T~ --|> Sharpy.IEquatable
    Sharpy.IEquatableWith~T~ --|> System.IEquatable~T~
    Sharpy.IEquatable --|> Sharpy.IHashable
```
