import io
from typing import Mapping, Sequence

from sharpy.compiler_toolchain.logging import logger

## Base classes


class NodeSource:
    """
    Represents the source location of a node in the code, including line
    numbers and column offsets.
    """

    def __init__(self, lineno: int, col_offset: int, end_lineno: int, end_col_offset: int):
        self._line_num: int = lineno
        self._col_offset: int = col_offset
        self._end_line_num: int = end_lineno
        self._end_col_offset: int = end_col_offset

    def lineno(self) -> int:
        return self._line_num

    def col_offset(self) -> int:
        return self._col_offset

    def end_lineno(self) -> int:
        return self._end_line_num

    def end_col_offset(self) -> int:
        return self._end_col_offset

    def __repr__(self) -> str:
        return f"NodeSource({self._line_num}, {self._col_offset}, {self._end_line_num}, {self._end_col_offset})"


class Node:
    """
    The base class for all AST nodes in Sharpy.
    """

    def __init__(self, source: NodeSource | None = None):
        self._source: NodeSource | None = source

    def source(self) -> NodeSource | None:
        return self._source

    def set_source(self, source: NodeSource) -> None:
        self._source = source

    def __repr__(self) -> str:
        return f"{self.__class__.__name__}(source={self._source})"


## Categories
# These are meant to be used to limit the composition of certain nodes,
# but in practice, there might be more categories than these and these
# categories might not even be used.


class Root(Node):
    """
    In Sharpy, this is only the Module node.
    """

    def __init__(self, source: NodeSource | None = None):
        super().__init__(source)


class TypeComponent(Node):
    """
    A type component abstracts the differences between module names, and type
    names (e.g. pathlib.Path). The simplest type component is either a module
    name or a simple type (`int`), while the most complex is one that is
    parameterized.
    """

    def __init__(
        self,
        name: str,
        parameters: Sequence["TypeParameter"] | None = None,
        source: NodeSource | None = None,
    ):
        super().__init__(source)
        self._name: str = name
        self._parameters: Sequence["TypeParameter"] = parameters if parameters is not None else []

    def name(self) -> str:
        return self._name

    def parameters(self) -> Sequence["TypeParameter"]:
        """
        Returns the parameters of this type component, if any.
        """
        return self._parameters

    def __repr__(self) -> str:
        return f"TypeComponent(name={self._name}, parameters={self._parameters})"


class Type(Node):
    def __init__(self, components: Sequence[TypeComponent], source: NodeSource | None = None):
        super().__init__(source)
        self._components: Sequence[TypeComponent] = components

    def components(self) -> Sequence[TypeComponent]:
        return self._components

    def __repr__(self) -> str:
        return f"Type(components={self._components})"


class Literal(Node):
    """
    Things like constants, strings, and collection literals like lists.
    """

    def __init__(self, type_: Type | None = None, source: NodeSource | None = None):
        super().__init__(source)
        self._type: Type | None = type_

    def type(self) -> Type | None:
        return self._type

    def set_type(self, type_: Type) -> None:
        """
        Set the type of the literal node.
        """
        self._type = type_


class Statement(Node):
    def __init__(self, source: NodeSource | None = None):
        super().__init__(source)


class Variable(Node):
    def __init__(self, source: NodeSource | None = None):
        super().__init__(source)


class Expression(Node):
    def __init__(self, source: NodeSource | None = None):
        super().__init__(source)


class UnaryOperatorToken(Node):
    def __init__(self, source: NodeSource | None = None):
        super().__init__(source)


class BinaryOperatorToken(Node):
    def __init__(self, source: NodeSource | None = None):
        super().__init__(source)


class ComparisonOperatorToken(Node):
    def __init__(self, source: NodeSource | None = None):
        super().__init__(source)


class Context(Node):
    """
    A context for a variable, indicating whether it is being read, written,
    or deleted.
    """

    def __init__(self, source: NodeSource | None = None):
        super().__init__(source)


## Concrete nodes
# These nodes represent the actual nodes in alphabetical order.


class Add(BinaryOperatorToken):
    """
    The addition operator +.
    """

    def __init__(self, source: NodeSource | None = None):
        super().__init__(source)

    def __repr__(self) -> str:
        return "Add()"


class alias(Node):
    def __init__(self, name: str, asname: str | None, source: NodeSource | None = None):
        super().__init__(source)
        self._name: str = name
        self._asname: str | None = asname

    def name(self) -> str:
        return self._name

    def asname(self) -> str | None:
        return self._asname

    def __repr__(self) -> str:
        return f"alias(name={self._name}, asname={self._asname})"


class And(Node):
    """
    The `and` keyword, used as the logical AND operator.
    """

    def __init__(self, source: NodeSource | None = None):
        super().__init__(source)

    def __repr__(self) -> str:
        return "And()"


class AnnAssign(Statement):
    """
    An annotated assignment expression of the form:
        x: y = z
    """

    def __init__(self, source: NodeSource | None = None):
        super().__init__(source)

    def __repr__(self) -> str:
        return "AnnAssign()"


class param(Node):
    """
    A parameter in a function call.
    """

    def __init__(self, name: str, type_: Node, default: Node, source: NodeSource | None = None):
        super().__init__(source)
        self._name: str = name
        self._type: Node = type_
        self._default: Node = default

    def name(self) -> str:
        return self._name

    def type(self) -> Node:
        return self._type

    def default(self) -> Node:
        return self._default

    def __repr__(self) -> str:
        return f"param(name={self._name}, type={self._type}, default={self._default})"


class parameters(Node):
    """
    An parameter list in a function call.
    """

    def __init__(self, params: Mapping[str, param]):
        super().__init__()
        self._params: Mapping[str, param] = params

    def params(self) -> Mapping[str, param]:
        return self._params

    def __repr__(self) -> str:
        return f"parameters(params={self._params})"


class Assert(Statement):
    """
    An assert statement.
    """

    def __init__(self, test: Node, msg: Node | None = None, source: NodeSource | None = None):
        super().__init__(source)
        self._test: Node = test
        self._msg: Node | None = msg

    def test(self) -> Node:
        return self._test

    def msg(self) -> Node | None:
        return self._msg

    def __repr__(self) -> str:
        return f"Assert(test={self._test}, msg={self._msg})"


class Assign(Statement):
    """
    An assignment statement of the form:
        x = y
    or
        x, y = z
    """

    def __init__(self, targets: Sequence[Node], value: Node, source: NodeSource | None = None):
        super().__init__(source)
        self._targets: Sequence[Node] = targets
        self._value: Node = value

    def targets(self) -> Sequence[Node]:
        return self._targets

    def value(self) -> Node:
        return self._value

    def __repr__(self) -> str:
        return f"Assign(targets={self._targets}, value={self._value})"


class AsyncFor(Node):
    """
    An asynchronous for loop.
    """

    def __init__(self, target: Node, iter: Node, source: NodeSource | None = None):
        super().__init__(source)
        self._target: Node = target
        self._iter: Node = iter

    def target(self) -> Node:
        return self._target

    def iter(self) -> Node:
        return self._iter

    def __repr__(self) -> str:
        return f"AsyncFor(target={self._target}, iter={self._iter})"


class AsyncFunctionDef(Node):
    """
    An asynchronous function.
    """

    def __init__(
        self,
        name: str,
        args: parameters,
        body: Sequence[Node],
        decorator_list: Sequence[Node] | None = None,
        returns: Node | None = None,
        source: NodeSource | None = None,
    ):
        super().__init__(source)
        self._name: str = name
        self._args: parameters = args
        self._body: Sequence[Node] = body
        self._decorator_list: Sequence[Node] = decorator_list if decorator_list is not None else []
        self._returns: Node | None = returns

    def name(self) -> str:
        return self._name

    def args(self) -> parameters:
        return self._args

    def body(self) -> Sequence[Node]:
        return self._body

    def decorator_list(self) -> Sequence[Node]:
        return self._decorator_list

    def returns(self) -> Node | None:
        return self._returns

    def __repr__(self) -> str:
        return f"AsyncFunctionDef(name={self._name}, args={self._args}, body={self._body}, decorator_list={self._decorator_list}, returns={self._returns})"


class AsyncWith(Node):
    """
    An asynchronous with block.
    """

    def __init__(
        self, items: Sequence[Node], body: Sequence[Node], source: NodeSource | None = None
    ):
        super().__init__(source)
        self._items: Sequence[Node] = items
        self._body: Sequence[Node] = body

    def items(self) -> Sequence[Node]:
        return self._items

    def body(self) -> Sequence[Node]:
        return self._body

    def __repr__(self) -> str:
        return f"AsyncWith(items={self._items}, body={self._body})"


class Attribute(Expression):
    """
    An attribute access expression of the form:
        obj.attr
    where `obj` is any valid expression and `attr` is the attribute name.
    """

    def __init__(self, value: Node, attr: str, ctx: Context, source: NodeSource | None = None):
        super().__init__(source)
        self._value: Node = value
        self._attr: str = attr
        self._ctx: Context = ctx

    def value(self) -> Node:
        return self._value

    def attr(self) -> str:
        return self._attr

    def ctx(self) -> Context:
        return self._ctx

    def __repr__(self) -> str:
        return f"Attribute(value={self._value}, attr={self._attr}, ctx={self._ctx})"


class AugmentedAssignment(Statement):
    """
    An augmented assignment statement of the form:
        x += y
    where + is any valid binary operator.
    """

    def __init__(
        self,
        target: Node,
        operator: BinaryOperatorToken,
        value: Node,
        source: NodeSource | None = None,
    ):
        super().__init__(source)
        self._target: Node = target
        self._operator: Node = operator
        self._value: Node = value

    def target(self) -> Node:
        return self._target

    def operator(self) -> Node:
        return self._operator

    def value(self) -> Node:
        return self._value

    def __repr__(self) -> str:
        return f"AugmentedAssignment(target={self._target}, operator={self._operator}, value={self._value})"


class Await(Node):
    """
    An await expression of the form:
        await x
    """

    def __init__(self, value: Node, source: NodeSource | None = None):
        super().__init__(source)
        self._value: Node = value

    def value(self) -> Node:
        return self._value

    def __repr__(self) -> str:
        return f"Await(value={self._value})"


class BinaryOperation(Node):
    """
    A binary operation of the form:
        x + y
    where + is any valid binary operator.
    """

    def __init__(
        self,
        left: Node,
        operator: BinaryOperatorToken,
        right: Node,
        source: NodeSource | None = None,
    ):
        super().__init__(source)
        self._left: Node = left
        self._operator: BinaryOperatorToken = operator
        self._right: Node = right

    def left(self) -> Node:
        return self._left

    def operator(self) -> BinaryOperatorToken:
        return self._operator

    def right(self) -> Node:
        return self._right

    def __repr__(self) -> str:
        return f"BinOp(left={self._left}, operator={self._operator}, right={self._right})"


class BitwiseAnd(BinaryOperatorToken):
    """
    The bitwise AND operator &.
    """

    def __init__(self, source: NodeSource | None = None):
        super().__init__(source)

    def __repr__(self) -> str:
        return "BitAnd()"


class BitwiseOr(BinaryOperatorToken):
    """
    The bitwise OR operator |.
    """

    def __init__(self, source: NodeSource | None = None):
        super().__init__(source)

    def __repr__(self) -> str:
        return "BitOr()"


class BitwiseXor(BinaryOperatorToken):
    """
    The bitwise XOR operator ^.
    """

    def __init__(self, source: NodeSource | None = None):
        super().__init__(source)

    def __repr__(self) -> str:
        return "BitXor()"


class BoolOperation(Expression):
    """
    A boolean operation of the form:
        x and y and z
    """

    def __init__(
        self, operator: "Or | And", values: Sequence[Node], source: NodeSource | None = None
    ):
        super().__init__(source)
        self._operator: Or | And = operator
        self._values: Sequence[Node] = values

    def operator(self) -> "Or | And":
        return self._operator

    def values(self) -> Sequence[Node]:
        return self._values

    def __repr__(self) -> str:
        return f"BoolOperation(operator={self._operator}, values={self._values})"


class Break(Node):
    """
    A break statement.
    """

    def __init__(self, source: NodeSource | None = None):
        super().__init__(source)

    def __repr__(self) -> str:
        return "Break()"


class Bytes(Node):
    """
    A bytes literal.
    """

    def __init__(self, value: str, source: NodeSource | None = None):
        super().__init__(source)
        self._value: str = value

    def value(self) -> str:
        return self._value

    def __repr__(self) -> str:
        return f"Bytes(value={self._value})"


class Call(Expression):
    """
    A function call expression of the form:
        func(x, y)
    """

    def __init__(
        self,
        func: Node,
        args: Sequence[Node],
        keywords: Sequence["keyword"] | None = None,
        source: NodeSource | None = None,
    ):
        super().__init__(source)
        self._func: Node = func
        self._args: Sequence[Node] = args
        self._keywords: Sequence[keyword] = keywords if keywords is not None else []

    def func(self) -> Node:
        return self._func

    def args(self) -> Sequence[Node]:
        return self._args

    def keywords(self) -> Sequence["keyword"]:
        return self._keywords

    def __repr__(self) -> str:
        return f"Call(func={self._func}, args={self._args}, keywords={self._keywords})"


class ClassDefinition(Node):
    """
    A class definition.
    """

    def __init__(
        self,
        name: str,
        base: Node | None,
        protocols: Sequence[Node],
        keywords: Sequence[Node],
        body: Sequence[Node],
        source: NodeSource | None = None,
    ):
        super().__init__(source)
        self._name: str = name
        self._base: Node | None = base
        self._protocols: Sequence[Node] = protocols
        self._keywords: Sequence[Node] = keywords
        self._body: Sequence[Node] = body

    def name(self) -> str:
        return self._name

    def base(self) -> Node | None:
        return self._base

    def protocols(self) -> Sequence[Node]:
        return self._protocols

    def keywords(self) -> Sequence[Node]:
        return self._keywords

    def body(self) -> Sequence[Node]:
        return self._body

    def __repr__(self) -> str:
        return f"ClassDef(name={self._name}, base={self._base}, protocols={self._protocols}, keywords={self._keywords}, body={self._body})"


class Compare(Expression):
    """
    A comparison operation of the form:
        x < y
    where < is any valid comparison operator.
    """

    def __init__(
        self,
        left: Node,
        ops: Sequence[ComparisonOperatorToken],
        comparators: Sequence[Node],
        source: NodeSource | None = None,
    ):
        super().__init__(source)
        self._left: Node = left
        self._ops: Sequence[ComparisonOperatorToken] = ops
        self._comparators: Sequence[Node] = comparators

    def left(self) -> Node:
        return self._left

    def ops(self) -> Sequence[ComparisonOperatorToken]:
        return self._ops

    def comparators(self) -> Sequence[Node]:
        return self._comparators

    def __repr__(self) -> str:
        return f"Compare(left={self._left}, ops={self._ops}, comparators={self._comparators})"


class comprehension(Node):
    """
    A for clause within a comprehension expression.
    """

    def __init__(
        self,
        target: "Name | Tuple",
        iter: Node,
        ifs: Sequence[Node],
        is_async: bool,
        source: NodeSource | None = None,
    ):
        super().__init__(source)
        self._target: "Name | Tuple" = target
        self._iter: Node = iter
        self._ifs: Sequence[Node] = ifs
        self._is_async: bool = is_async

    def target(self) -> "Name | Tuple":
        return self._target

    def iter(self) -> Node:
        return self._iter

    def ifs(self) -> Sequence[Node]:
        return self._ifs

    def is_async(self) -> bool:
        """
        Returns True if this comprehension is asynchronous (e.g., async for).
        """
        return self._is_async

    def __repr__(self) -> str:
        return f"comprehension(target={self._target}, iter={self._iter}, ifs={self._ifs}, is_async={self.is_async()})"


class Constant(Literal):
    """
    A constant value, such as a string, number, or boolean.
    """

    def __init__(
        self,
        value: str | bytes | complex | int | float | bool | None,
        type_: Type | None = None,
        source: NodeSource | None = None,
    ):
        super().__init__(type_=type_, source=source)
        self._value: str | int | float | bool | bytes | complex | None = value

        # TODO: Need to infer other numeric types via the suffix
        if type_ is not None:
            self._type: Type | None = type_
        elif value is None:
            self._type = Type(components=[TypeComponent(name="NoneType")])
        else:
            self._type: Type | None = Type(components=[TypeComponent(name=type(value).__name__)])

        logger.debug(f"Processing constant node with value: {self._value}")

    def value(self) -> str | int | float | complex | bytes | bool | None:
        return self._value

    def type(self) -> Type | None:
        return self._type

    def __repr__(self) -> str:
        return f"Constant(value={self._value}, type={self._type})"


class Continue(Node):
    """
    A continue statement.
    """

    def __init__(self, source: NodeSource | None = None):
        super().__init__(source)

    def __repr__(self) -> str:
        return "Continue()"


class Del(Context):
    """
    Indicates a variable is being deleted.
    """

    def __init__(self, source: NodeSource | None = None):
        super().__init__(source)

    def __repr__(self) -> str:
        return "Del()"


class Delete(Statement):
    """
    A delete statement of the form:
        del x
    or
        del x, y

    Unlike Python, this does not allow the deletion of attributes, only
    subscripts of a dictionary.
    """

    def __init__(self, targets: Sequence["Subscript"], source: NodeSource | None = None):
        super().__init__(source)
        self._targets: Sequence[Subscript] = targets

    def targets(self) -> Sequence["Subscript"]:
        return self._targets

    def __repr__(self) -> str:
        return f"Delete(targets={self._targets})"


class Dict(Literal):
    """
    A dictionary literal of the form:
        {key1: value1, key2: value2}
    """

    def __init__(
        self,
        keys: Sequence[Node],
        values: Sequence[Node],
        type_: Type | None = None,
        source: NodeSource | None = None,
    ):
        super().__init__(type_=type_, source=source)
        self._keys: Sequence[Node] = keys
        self._values: Sequence[Node] = values

    def keys(self) -> Sequence[Node]:
        return self._keys

    def values(self) -> Sequence[Node]:
        return self._values

    def __repr__(self) -> str:
        return f"Dict(keys={self._keys}, values={self._values}, type={self._type})"


class DictComp(Node):
    """
    A dictionary comprehension of the form:
        {key: value for key, value in iterable if condition}
    """

    def __init__(
        self,
        key: Node,
        value: Node,
        generators: Sequence[comprehension],
        source: NodeSource | None = None,
    ):
        super().__init__(source)
        self._key: Node = key
        self._value: Node = value
        self._generators: Sequence[comprehension] = generators

    def key(self) -> Node:
        return self._key

    def value(self) -> Node:
        return self._value

    def generators(self) -> Sequence[comprehension]:
        return self._generators

    def __repr__(self) -> str:
        return f"DictComp(key={self._key}, value={self._value}, generators={self._generators})"


class Div(BinaryOperatorToken):
    """
    The division operator /.
    """

    def __init__(self, source: NodeSource | None = None):
        super().__init__(source)

    def __repr__(self) -> str:
        return "Div()"


class Ellipsis(Node):
    """
    An ellipsis literal, represented by the `...` token.
    """

    def __init__(self, source: NodeSource | None = None):
        super().__init__(source)

    def __repr__(self) -> str:
        return "Ellipsis()"


class Eq(ComparisonOperatorToken):
    """
    The equality operator.
    """

    def __init__(self, source: NodeSource | None = None):
        super().__init__(source)

    def __repr__(self) -> str:
        return "Eq()"


class ExceptHandler(Node):
    """
    An exception handler in a try-except block.
    """

    def __init__(
        self,
        type_: Node | None,
        name: str | None,
        body: Sequence[Node],
        source: NodeSource | None = None,
    ):
        super().__init__(source)
        self._type: Node | None = type_
        self._name: str | None = name
        self._body: Sequence[Node] = body

    def type(self) -> Node | None:
        return self._type

    def name(self) -> str | None:
        return self._name

    def body(self) -> Sequence[Node]:
        return self._body

    def __repr__(self) -> str:
        return f"ExceptHandler(type={self._type}, name={self._name}, body={self._body})"


class Expr(Expression):
    """
    An expression statement, which is an expression that is not part of a
    larger statement like a function call where the result is not used.
    """

    def __init__(
        self,
        value: "Constant | Name | Lambda | Yield | YieldFrom",
        source: NodeSource | None = None,
    ):
        super().__init__(source)
        self._value: Constant | Name | Lambda | Yield | YieldFrom = value

    def value(self) -> "Constant | Name | Lambda | Yield | YieldFrom":
        return self._value

    def __repr__(self) -> str:
        return f"Expr(value={self._value})"


class expr(Node):
    """
    A generic expression node.
    """

    def __init__(self, source: NodeSource | None = None):
        super().__init__(source)

    def __repr__(self) -> str:
        return "expr()"


class FloorDiv(BinaryOperatorToken):
    """
    The floor division operator //.
    """

    def __init__(self, source: NodeSource | None = None):
        super().__init__(source)

    def __repr__(self) -> str:
        return "FloorDiv()"


class For(Node):
    """
    A for loop.
    """

    # TODO: How do we represent typed targets like `for x: int in iterable`?
    def __init__(
        self,
        target: "Name | Tuple | List | Attribute | Subscript",
        iter: Node,
        body: Sequence[Node],
        orelse: Sequence[Node] | None = None,
        source: NodeSource | None = None,
    ):
        super().__init__(source)
        self._target: Name | Tuple | List | Attribute | Subscript = target
        self._iter: Node = iter
        self._body: Sequence[Node] = body
        self._orelse: Sequence[Node] = orelse if orelse is not None else []

    def target(self) -> "Name | Tuple | List | Attribute | Subscript":
        return self._target

    def iter(self) -> Node:
        return self._iter

    def body(self) -> Sequence[Node]:
        return self._body

    def orelse(self) -> Sequence[Node]:
        return self._orelse

    def __repr__(self) -> str:
        return f"For(target={self._target}, iter={self._iter}, body={self._body}, orelse={self._orelse})"


class FormattedValue(Literal):
    """
    A formatted string literal, such as an f-string.
    """

    def __init__(
        self,
        value: Node,
        conversion: int,
        format_spec: Node | None = None,
        source: NodeSource | None = None,
    ):
        super().__init__(type_=Type(components=[TypeComponent(name="str")]), source=source)
        self._value: Node = value
        self._conversion: int = conversion
        self._format_spec: Node | None = format_spec

    def value(self) -> Node:
        return self._value

    def conversion(self) -> int:
        return self._conversion

    def format_spec(self) -> Node | None:
        return self._format_spec

    def __repr__(self) -> str:
        return f"FormattedValue(value={self._value}, conversion={self._conversion}, format_spec={self._format_spec}, type={self._type})"


class FunctionDef(Node):
    """
    A function definition.
    """

    def __init__(
        self,
        name: str,
        args: parameters,
        body: Sequence[Node],
        decorator_list: Sequence[Node] | None = None,
        returns: Node | None = None,
        source: NodeSource | None = None,
    ):
        super().__init__(source)
        self._name: str = name
        self._args: parameters = args
        self._body: Sequence[Node] = body
        self._decorator_list: Sequence[Node] = decorator_list if decorator_list is not None else []
        self._returns: Node | None = returns

    def name(self) -> str:
        return self._name

    def args(self) -> parameters:
        return self._args

    def body(self) -> Sequence[Node]:
        return self._body

    def decorator_list(self) -> Sequence[Node]:
        return self._decorator_list

    def returns(self) -> Node | None:
        return self._returns

    def __repr__(self) -> str:
        return f"FunctionDef(name={self._name}, args={self._args}, body={self._body}, decorator_list={self._decorator_list}, returns={self._returns})"


class GeneratorExp(Node):
    """
    A generator expression, which is a compact way to create a generator.
    """

    def __init__(
        self,
        elt: Node,
        generators: Sequence[comprehension],
        source: NodeSource | None = None,
    ):
        super().__init__(source)
        self._elt: Node = elt
        self._generators: Sequence[comprehension] = generators

    def elt(self) -> Node:
        return self._elt

    def generators(self) -> Sequence[comprehension]:
        return self._generators

    def __repr__(self) -> str:
        return f"GeneratorExp(elt={self._elt}, generators={self._generators})"


class Gt(ComparisonOperatorToken):
    """
    The greater than operator >.
    """

    def __init__(self, source: NodeSource | None = None):
        super().__init__(source)

    def __repr__(self) -> str:
        return "Gt()"


class GtE(ComparisonOperatorToken):
    """
    The greater than or equal to operator >=.
    """

    def __init__(self, source: NodeSource | None = None):
        super().__init__(source)

    def __repr__(self) -> str:
        return "GtE()"


class If(Node):
    """
    An if statement.
    """

    def __init__(
        self,
        test: Node,
        body: Sequence[Node],
        orelse: Sequence[Node],
        source: NodeSource | None = None,
    ):
        super().__init__(source)
        self._test: Node = test
        self._body: Sequence[Node] = body
        self._orelse: Sequence[Node] = orelse

    def test(self) -> Node:
        return self._test

    def body(self) -> Sequence[Node]:
        return self._body

    def orelse(self) -> Sequence[Node]:
        return self._orelse

    def __repr__(self) -> str:
        return f"If(test={self._test}, body={self._body}, orelse={self._orelse})"


class IfExp(Expression):
    """
    An if expression, which is a conditional expression of the form:
        x if condition else y
    """

    def __init__(self, test: Node, body: Node, orelse: Node, source: NodeSource | None = None):
        super().__init__(source)
        self._test: Node = test
        self._body: Node = body
        self._orelse: Node = orelse

    def test(self) -> Node:
        return self._test

    def body(self) -> Node:
        return self._body

    def orelse(self) -> Node:
        return self._orelse

    def __repr__(self) -> str:
        return f"IfExp(test={self._test}, body={self._body}, orelse={self._orelse})"


class Import(Node):
    """
    An import statement, which imports modules or specific names from modules.
    """

    def __init__(self, names: Sequence[alias], source: NodeSource | None = None):
        super().__init__(source)
        self._names: Sequence[alias] = names

    def names(self) -> Sequence[alias]:
        return self._names

    def __repr__(self) -> str:
        return f"Import(names={self._names})"


class ImportFrom(Node):
    """
    An import statement that imports specific names from a module. Level
    indicates the relative import level, where 0 is an absolute import.
    """

    def __init__(
        self,
        module: str | None,
        names: Sequence[alias],
        level: int,
        source: NodeSource | None = None,
    ):
        super().__init__(source)
        self._module: str | None = module
        self._names: Sequence[alias] = names
        self._level: int = level

    def module(self) -> str | None:
        return self._module

    def names(self) -> Sequence[alias]:
        return self._names

    def level(self) -> int:
        return self._level

    def __repr__(self) -> str:
        return f"ImportFrom(module={self._module}, names={self._names}, level={self._level})"


class In(ComparisonOperatorToken):
    """
    The `in` keyword, which is the membership operator, used to check if a
    value is in a sequence.
    """

    def __init__(self, source: NodeSource | None = None):
        super().__init__(source)

    def __repr__(self) -> str:
        return "In()"


class Index(Node):
    """
    An index expression, which is used to access elements in a sequence.
    """

    def __init__(self, value: Node, source: NodeSource | None = None):
        super().__init__(source)
        self._value: Node = value

    def value(self) -> Node:
        return self._value

    def __repr__(self) -> str:
        return f"Index(value={self._value})"


class Is(ComparisonOperatorToken):
    """
    The `is` keyword, which is the identity operator, used to check if two
    values are the same object.
    """

    def __init__(self, source: NodeSource | None = None):
        super().__init__(source)

    def __repr__(self) -> str:
        return "Is()"


class IsNot(ComparisonOperatorToken):
    """
    The `is not` keyword, which is the negative identity operator, used to
    check if two values are not the same object.
    """

    def __init__(self, source: NodeSource | None = None):
        super().__init__(source)

    def __repr__(self) -> str:
        return "IsNot()"


class Invert(UnaryOperatorToken):
    """
    The bitwise NOT operator ~.
    """

    def __init__(self, source: NodeSource | None = None):
        super().__init__(source)

    def __repr__(self) -> str:
        return "Invert()"


class JoinedStr(Literal):
    """
    A joined string literal, which is a sequence of string literals concatenated together.
    """

    def __init__(
        self, values: Sequence[FormattedValue | Constant], source: NodeSource | None = None
    ):
        super().__init__(type_=Type(components=[TypeComponent(name="str")]), source=source)
        self._values: Sequence[FormattedValue | Constant] = values

    def values(self) -> Sequence[FormattedValue | Constant]:
        return self._values

    def __repr__(self) -> str:
        return f"JoinedStr(values={self._values}, type={self._type})"


class keyword(Node):
    """
    A keyword argument in a function call.
    """

    def __init__(self, arg: str | None, value: Node, source: NodeSource | None = None):
        super().__init__(source)
        self._arg: str | None = arg
        self._value: Node = value

    def arg(self) -> str | None:
        return self._arg

    def value(self) -> Node:
        return self._value

    def __repr__(self) -> str:
        return f"keyword(arg={self._arg}, value={self._value})"


class Lambda(Node):
    """
    A lambda function, which is an anonymous function defined with the `lambda` keyword.
    """

    def __init__(self, args: parameters, body: Node, source: NodeSource | None = None):
        super().__init__(source)
        self._args: parameters = args
        self._body: Node = body
        # TODO: return type

    def args(self) -> parameters:
        return self._args

    def body(self) -> Node:
        return self._body

    def __repr__(self) -> str:
        return f"Lambda(args={self._args}, body={self._body})"


class List(Literal):
    """
    A list literal, which is a sequence of values enclosed in square brackets.
    """

    def __init__(
        self,
        elts: Sequence[Node],
        ctx: Context,
        type_: Type | None = None,
        source: NodeSource | None = None,
    ):
        super().__init__(type_=type_, source=source)
        self._elts: Sequence[Node] = elts
        self._ctx: Context = ctx

    def elts(self) -> Sequence[Node]:
        return self._elts

    def ctx(self) -> Context:
        return self._ctx

    def __repr__(self) -> str:
        return f"List(elts={self._elts}, ctx={self._ctx}, type={self._type})"


class ListComp(Node):
    """
    A list comprehension, which is a compact way to create a list.
    """

    def __init__(
        self, elt: Node, generators: Sequence[comprehension], source: NodeSource | None = None
    ):
        super().__init__(source)
        self._elt: Node = elt
        self._generators: Sequence[comprehension] = generators

    def elt(self) -> Node:
        return self._elt

    def generators(self) -> Sequence[comprehension]:
        return self._generators

    def __repr__(self) -> str:
        return f"ListComp(elt={self._elt}, generators={self._generators})"


class Load(Context):
    """
    A load context, which indicates that a value is being read.
    """

    def __init__(self, source: NodeSource | None = None):
        super().__init__(source)

    def __repr__(self) -> str:
        return "Load()"


class LShift(BinaryOperatorToken):
    """
    The left shift operator <<.
    """

    def __init__(self, source: NodeSource | None = None):
        super().__init__(source)

    def __repr__(self) -> str:
        return "LShift()"


class Lt(ComparisonOperatorToken):
    """
    The less than operator <.
    """

    def __init__(self, source: NodeSource | None = None):
        super().__init__(source)

    def __repr__(self) -> str:
        return "Lt()"


class LtE(ComparisonOperatorToken):
    """
    The less than or equal to operator <=.
    """

    def __init__(self, source: NodeSource | None = None):
        super().__init__(source)

    def __repr__(self) -> str:
        return "LtE()"


class Match(Node):
    """
    A match statement, which is used for pattern matching.
    """

    def __init__(self, subject: Node, cases: Sequence[Node], source: NodeSource | None = None):
        super().__init__(source)
        self._subject: Node = subject
        # TODO: should this be a Sequence[match_case]?
        self._cases: Sequence[Node] = cases

    def subject(self) -> Node:
        return self._subject

    def cases(self) -> Sequence[Node]:
        return self._cases

    def __repr__(self) -> str:
        return f"Match(subject={self._subject}, cases={self._cases})"


class match_case(Node):
    """
    A case in a match statement.
    """

    def __init__(
        self,
        pattern: Node,
        guard: Node | None,
        body: Sequence[Node],
        source: NodeSource | None = None,
    ):
        super().__init__(source)
        self._pattern: Node = pattern
        self._guard: Node | None = guard
        self._body: Sequence[Node] = body

    def pattern(self) -> Node:
        return self._pattern

    def guard(self) -> Node | None:
        return self._guard

    def body(self) -> Sequence[Node]:
        return self._body

    def __repr__(self) -> str:
        return f"match_case(pattern={self._pattern}, guard={self._guard}, body={self._body})"


class MatchAs(Node):
    """
    A pattern that matches a value and binds it to a name.
    """

    def __init__(self, name: str | None, pattern: Node, source: NodeSource | None = None):
        super().__init__(source)
        self._name: str | None = name
        self._pattern: Node = pattern

    def name(self) -> str | None:
        return self._name

    def pattern(self) -> Node:
        return self._pattern

    def __repr__(self) -> str:
        return f"MatchAs(name={self._name}, pattern={self._pattern})"


class MatchClass(Node):
    """
    A pattern that matches a class instance.
    """

    def __init__(
        self,
        cls: Node,
        patterns: Sequence[Node],
        kwd_patterns: Sequence[Node],
        source: NodeSource | None = None,
    ):
        super().__init__(source)
        self._cls: Node = cls
        self._patterns: Sequence[Node] = patterns
        self._kwd_patterns: Sequence[Node] = kwd_patterns

    def cls(self) -> Node:
        return self._cls

    def patterns(self) -> Sequence[Node]:
        return self._patterns

    def kwd_patterns(self) -> Sequence[Node]:
        return self._kwd_patterns

    def __repr__(self) -> str:
        return f"MatchClass(cls={self._cls}, patterns={self._patterns}, kwd_patterns={self._kwd_patterns})"


class MatchMapping(Node):
    """
    A pattern that matches a mapping (like a dictionary).
    """

    def __init__(
        self,
        keys: Sequence[Node],
        patterns: Sequence[Node],
        rest: Node | None,
        source: NodeSource | None = None,
    ):
        super().__init__(source)
        self._keys: Sequence[Node] = keys
        self._patterns: Sequence[Node] = patterns
        self._rest: Node | None = rest

    def keys(self) -> Sequence[Node]:
        return self._keys

    def patterns(self) -> Sequence[Node]:
        return self._patterns

    def rest(self) -> Node | None:
        return self._rest

    def __repr__(self) -> str:
        return f"MatchMapping(keys={self._keys}, patterns={self._patterns}, rest={self._rest})"


class MatchOr(Node):
    """
    A pattern that matches any of the provided patterns.
    """

    def __init__(self, patterns: Sequence[Node], source: NodeSource | None = None):
        super().__init__(source)
        self._patterns: Sequence[Node] = patterns

    def patterns(self) -> Sequence[Node]:
        return self._patterns

    def __repr__(self) -> str:
        return f"MatchOr(patterns={self._patterns})"


class MatchSequence(Node):
    """
    A pattern that matches a sequence (like a list or tuple).
    """

    def __init__(
        self, patterns: Sequence[Node], rest: Node | None, source: NodeSource | None = None
    ):
        super().__init__(source)
        self._patterns: Sequence[Node] = patterns
        self._rest: Node | None = rest

    def patterns(self) -> Sequence[Node]:
        return self._patterns

    def rest(self) -> Node | None:
        return self._rest

    def __repr__(self) -> str:
        return f"MatchSequence(patterns={self._patterns}, rest={self._rest})"


class MatchSingleton(Node):
    """
    A pattern that matches a singleton value.
    """

    def __init__(self, value: Node, source: NodeSource | None = None):
        super().__init__(source)
        self._value: Node = value

    def value(self) -> Node:
        return self._value

    def __repr__(self) -> str:
        return f"MatchSingleton(value={self._value})"


class MatchStar(Node):
    """
    A pattern that matches a variable number of elements in a sequence.
    """

    def __init__(self, name: str | None, source: NodeSource | None = None):
        super().__init__(source)
        self._name: str | None = name

    def name(self) -> str | None:
        return self._name

    def __repr__(self) -> str:
        return f"MatchStar(name={self._name})"


class MatchValue(Node):
    """
    A pattern that matches a specific value.
    """

    # TODO: How is this different from MatchSingleton?
    def __init__(self, value: Node, source: NodeSource | None = None):
        super().__init__(source)
        self._value: Node = value

    def value(self) -> Node:
        return self._value

    def __repr__(self) -> str:
        return f"MatchValue(value={self._value})"


class MatMult(Node):
    """
    The matrix multiplication operator *.
    """

    def __init__(self, source: NodeSource | None = None):
        super().__init__(source)

    def __repr__(self) -> str:
        return "MatMult()"


class Mod(BinaryOperatorToken):
    """
    The modulo operator %.
    """

    def __init__(self, source: NodeSource | None = None):
        super().__init__(source)

    def __repr__(self) -> str:
        return "Mod()"


class Module(Root):
    """
    A module, which is a collection of statements and definitions.
    """

    def __init__(self, body: Sequence[Node], source: NodeSource | None = None):
        super().__init__(source)
        self._body: Sequence[Node] = body

    def body(self) -> Sequence[Node]:
        return self._body

    def __repr__(self) -> str:
        return f"Module(body={self._body})"


class Mult(BinaryOperatorToken):
    """
    The multiplication operator *.
    """

    def __init__(self, source: NodeSource | None = None):
        super().__init__(source)

    def __repr__(self) -> str:
        return "Mult()"


class Name(Variable):
    """
    A name, which is an identifier in the code.
    """

    def __init__(
        self, id: str, ctx: Context, type_: str | None = None, source: NodeSource | None = None
    ):
        super().__init__(source)
        self._id: str = id
        self._ctx: Context = ctx
        self._type: str | None = type_

    def id(self) -> str:
        return self._id

    def ctx(self) -> Context:
        return self._ctx

    def type(self) -> str | None:
        return self._type

    def __repr__(self) -> str:
        return f"Name(id={self._id}, ctx={self._ctx}, type={self._type})"


class NamedExpr(Node):
    """
    A named expression, which is an assignment expression of the form:
        x := y

    a.k.a. the "walrus operator".
    """

    def __init__(self, target: Node, value: Node, source: NodeSource | None = None):
        super().__init__(source)
        self._target: Node = target
        self._value: Node = value

    def target(self) -> Node:
        return self._target

    def value(self) -> Node:
        return self._value

    def __repr__(self) -> str:
        return f"NamedExpr(target={self._target}, value={self._value})"


class Not(UnaryOperatorToken):
    """
    The `not` keyword, used for logical negation.
    """

    def __init__(self, source: NodeSource | None = None):
        super().__init__(source)

    def __repr__(self) -> str:
        return "Not()"


class NotEq(ComparisonOperatorToken):
    """
    The not equal operator.
    """

    def __init__(self, source: NodeSource | None = None):
        super().__init__(source)

    def __repr__(self) -> str:
        return "NotEq()"


class NotIn(Node):
    """
    The `not in` keyword, which is the negative membership operator, used to
    check if a value is not in a sequence.
    """

    def __init__(self, source: NodeSource | None = None):
        super().__init__(source)

    def __repr__(self) -> str:
        return "NotIn()"


class Or(Node):
    """
    The `or` keyword, which is the logical OR operator.
    """

    def __init__(self, source: NodeSource | None = None):
        super().__init__(source)

    def __repr__(self) -> str:
        return "Or()"


class Parameter(Node):
    """
    A parameter in a function definition.
    """

    def __init__(
        self,
        name: str,
        annotation: Node | None = None,
        default: Node | None = None,
        source: NodeSource | None = None,
    ):
        super().__init__(source)
        self._name: str = name
        self._annotation: Node | None = annotation
        self._default: Node | None = default

    def name(self) -> str:
        return self._name

    def annotation(self) -> Node | None:
        return self._annotation

    def default(self) -> Node | None:
        return self._default

    def __repr__(self) -> str:
        return (
            f"Parameter(name={self._name}, annotation={self._annotation}, default={self._default})"
        )


class ParameterSpecification(Node):
    """
    A parameter specification, which is used to define the parameters of a
    function.
    """

    def __init__(self, name: str, type_: Type | None = None, source: NodeSource | None = None):
        """
        `self` cannot have a type.
        """

        super().__init__(source)
        self._name: str = name
        self._type: Type | None = type_

    def name(self) -> str:
        return self._name

    def type(self) -> Type | None:
        return self._type

    def __repr__(self) -> str:
        return f"ParameterSpecification(name={self._name}, type={self._type})"


class Pass(Statement):
    """
    A pass statement, which does nothing and is used as a placeholder.
    """

    def __init__(self, source: NodeSource | None = None):
        super().__init__(source)
        # No additional attributes needed for a pass statement

    def __repr__(self) -> str:
        return "Pass()"


class Pow(BinaryOperatorToken):
    """
    The power operator **, used for exponentiation.
    """

    def __init__(self, source: NodeSource | None = None):
        super().__init__(source)

    def __repr__(self) -> str:
        return "Pow()"


class Raise(Statement):
    """
    A raise statement, which is used to raise exceptions.
    """

    def __init__(
        self, exc: Node | None = None, cause: Node | None = None, source: NodeSource | None = None
    ):
        super().__init__(source)
        self._exc: Node | None = exc
        self._cause: Node | None = cause

    def exc(self) -> Node | None:
        return self._exc

    def cause(self) -> Node | None:
        return self._cause

    def __repr__(self) -> str:
        return f"Raise(exc={self._exc}, cause={self._cause})"


class Return(Node):
    """
    A return statement, which is used to return a value from a function.
    """

    def __init__(self, value: Node | None = None, source: NodeSource | None = None):
        super().__init__(source)
        self._value: Node | None = value

    def value(self) -> Node | None:
        return self._value

    def __repr__(self) -> str:
        return f"Return(value={self._value})"


class RShift(BinaryOperatorToken):
    """
    The right shift operator >>.
    """

    def __init__(self, source: NodeSource | None = None):
        super().__init__(source)

    def __repr__(self) -> str:
        return "RShift()"


class Set(Literal):
    """
    A set literal, which is a collection of unique values enclosed in curly braces.
    """

    def __init__(
        self, elts: Sequence[Node], type_: Type | None = None, source: NodeSource | None = None
    ):
        super().__init__(type_=type_, source=source)
        self._elts: Sequence[Node] = elts

    def elts(self) -> Sequence[Node]:
        return self._elts

    def __repr__(self) -> str:
        return f"Set(elts={self._elts}, type={self._type})"


class SetComp(Node):
    """
    A set comprehension, which is a compact way to create a set.
    """

    def __init__(
        self, elt: Node, generators: Sequence[comprehension], source: NodeSource | None = None
    ):
        super().__init__(source)
        self._elt: Node = elt
        self._generators: Sequence[comprehension] = generators

    def elt(self) -> Node:
        return self._elt

    def generators(self) -> Sequence[comprehension]:
        return self._generators

    def __repr__(self) -> str:
        return f"SetComp(elt={self._elt}, generators={self._generators})"


class Slice(Node):
    """
    A slice object, which represents a slice of a sequence.
    """

    def __init__(
        self,
        lower: Node | None,
        upper: Node | None,
        step: Node | None,
        source: NodeSource | None = None,
    ):
        super().__init__(source)
        self._lower: Node | None = lower
        self._upper: Node | None = upper
        self._step: Node | None = step

    def lower(self) -> Node | None:
        return self._lower

    def upper(self) -> Node | None:
        return self._upper

    def step(self) -> Node | None:
        return self._step

    def __repr__(self) -> str:
        return f"Slice(lower={self._lower}, upper={self._upper}, step={self._step})"


class Starred(Variable):
    """
    A starred expression, which is used to unpack elements from a sequence.
    This is also used when building a Call node with *args.
    """

    def __init__(self, value: Node, ctx: Context, source: NodeSource | None = None):
        super().__init__(source)
        self._value: Node = value
        self._ctx: Context = ctx

    def value(self) -> Node:
        return self._value

    def ctx(self) -> Context:
        return self._ctx

    def __repr__(self) -> str:
        return f"Starred(value={self._value}, ctx={self._ctx})"


class Store(Context):
    """
    A store context, which indicates that a value is being written to.
    """

    def __init__(self, source: NodeSource | None = None):
        super().__init__(source)

    def __repr__(self) -> str:
        return "Store()"


class Sub(BinaryOperatorToken):
    """
    The subtraction operator -.
    """

    def __init__(self, source: NodeSource | None = None):
        super().__init__(source)

    def __repr__(self) -> str:
        return "Sub()"


class Subscript(Node):
    """
    A subscript expression, which is used to access elements in a sequence
    using an index or a slice.
    """

    def __init__(self, value: Node, slice_: Node, ctx: Context, source: NodeSource | None = None):
        super().__init__(source)
        self._value: Node = value
        self._slice: Node = slice_
        self._ctx: Context = ctx

    def value(self) -> Node:
        return self._value

    def slice(self) -> Node:
        return self._slice

    def ctx(self) -> Context:
        return self._ctx

    def __repr__(self) -> str:
        return f"Subscript(value={self._value}, slice={self._slice}, ctx={self._ctx})"


class Try(Node):
    """
    A try statement, which is used to handle exceptions.
    """

    def __init__(
        self,
        body: Sequence[Node],
        handlers: Sequence[ExceptHandler],
        orelse: Sequence[Node] | None = None,
        finalbody: Sequence[Node] | None = None,
        source: NodeSource | None = None,
    ):
        super().__init__(source)
        self._body: Sequence[Node] = body
        self._handlers: Sequence[ExceptHandler] = handlers
        self._orelse: Sequence[Node] = orelse if orelse is not None else []
        self._finalbody: Sequence[Node] = finalbody if finalbody is not None else []

    def body(self) -> Sequence[Node]:
        return self._body

    def handlers(self) -> Sequence[ExceptHandler]:
        return self._handlers

    def orelse(self) -> Sequence[Node]:
        return self._orelse

    def finalbody(self) -> Sequence[Node]:
        return self._finalbody

    def __repr__(self) -> str:
        return f"Try(body={self._body}, handlers={self._handlers}, orelse={self._orelse}, finalbody={self._finalbody})"


class TryStar(Node):
    """
    A try statement with a star expression, which is used to handle exceptions with a star pattern.
    This is for exception groups:
    try:
        raise ExceptionGroup("group", [ValueError("value error"), TypeError("type error")])
    except* ValueError as e:
        print("Caught ValueError:", e)
    except* TypeError as e:
        print("Caught TypeError:", e)
    except Exception as e:
        print("Caught other exception:", e)

    This allows handling each exception in the group separately, which each
    exception being handled by the first handler that matches its type.
    """

    def __init__(
        self,
        body: Sequence[Node],
        handlers: Sequence[ExceptHandler],
        orelse: Sequence[Node] | None = None,
        finalbody: Sequence[Node] | None = None,
        source: NodeSource | None = None,
    ):
        super().__init__(source)
        self._body: Sequence[Node] = body
        self._handlers: Sequence[ExceptHandler] = handlers
        self._orelse: Sequence[Node] = orelse if orelse is not None else []
        self._finalbody: Sequence[Node] = finalbody if finalbody is not None else []

    def body(self) -> Sequence[Node]:
        return self._body

    def handlers(self) -> Sequence[ExceptHandler]:
        return self._handlers

    def orelse(self) -> Sequence[Node]:
        return self._orelse

    def finalbody(self) -> Sequence[Node]:
        return self._finalbody

    def __repr__(self) -> str:
        return f"TryStar(body={self._body}, handlers={self._handlers}, orelse={self._orelse}, finalbody={self._finalbody})"


class Tuple(Literal):
    """
    A tuple literal, which is a sequence of values enclosed in parentheses.
    """

    def __init__(
        self,
        elts: Sequence[Node],
        ctx: Context,
        type_: Type | None = None,
        source: NodeSource | None = None,
    ):
        super().__init__(type_=type_, source=source)
        self._elts: Sequence[Node] = elts
        self._ctx: Context = ctx

    def elts(self) -> Sequence[Node]:
        return self._elts

    def ctx(self) -> Context:
        return self._ctx

    def __repr__(self) -> str:
        return f"Tuple(elts={self._elts}, ctx={self._ctx}, type={self._type})"


class TypeAlias(Statement):
    """
    A type alias, which is used to define a new name for an existing type.

    A type alias can also be generic, meaning it can take type parameters. The
    corresponding value can also be a parameterized type, e.g.:

    type StringDict[T] = dict[str, T]
    """

    def __init__(self, name: Type, value: Type, source: NodeSource | None = None):
        super().__init__(source)
        self._name: Type = name
        self._value: Type = value

    def name(self) -> Type:
        return self._name

    def value(self) -> Type:
        return self._value

    def __repr__(self) -> str:
        return f"TypeAlias(name={self._name}, value={self._value})"


class TypeParameter(Node):
    """
    A type parameter, which is used to define generic types. It can be
    constrained by a bound type (a superclass or a protocol) and have a
    default, e.g.

    SomeClass[T: Object, U, V = int], where T must be a subclass of Object,
    U can be any type, and V defaults to int if not specified.

    Constraints can also be introduced via `where` as in C#, but this is not
    yet designed.
    """

    def __init__(
        self,
        name: str,
        default_type: Type | None = None,
        constraint: Node | None = None,
        resolved_type: Type | None = None,
        source: NodeSource | None = None,
    ):
        super().__init__(source)
        self._name: str = name
        self._default_type: Type | None = default_type
        # TODO: Constraints need to be modeled
        self._constraint: Node | None = constraint

        # Note: This should be resolved in static type analysis
        self._resolved_type: Type | None = resolved_type

    def name(self) -> str:
        """
        The name of the type parameter, e.g. `T` in `list[T].
        """
        return self._name

    def resolved_type(self) -> Node | None:
        """
        This is the resolved type of a specific instance of a parameterized
        type, e.g. `T` for list[T] = [1, 3, 5] would be likely be `int`.
        """
        return self._resolved_type

    def default_type(self) -> Node | None:
        """
        The default type for this type parameter, which can be used if no
        specific type is provided.
        """
        return self._default_type

    def constraint(self) -> Node | None:
        """
        Constraint on the type parameter, which can be used to restrict the
        types that can be used as arguments for this type parameter.

        This will be a constraint of the sort in C# introduced by the `where`
        keyword, or by the colon syntax indicating a superclass or protocol.
        """
        return self._constraint

    def __repr__(self) -> str:
        return f"TypeVar(name={self._name}, default_type={self._default_type}, constraint={self._constraint}, resolved_type={self._resolved_type})"


class UnaryAddition(UnaryOperatorToken):
    """
    The unary addition operator +.
    """

    def __init__(self, source: NodeSource | None = None):
        super().__init__(source)

    def __repr__(self) -> str:
        return "UnaryAddition()"


class UnaryOperation(Expression):
    """
    A unary operation, which is an operation that takes a single operand.
    """

    def __init__(
        self, operator: UnaryOperatorToken, operand: Expression, source: NodeSource | None = None
    ):
        super().__init__(source)
        self._operator: UnaryOperatorToken = operator
        self._operand: Expression = operand

    def operator(self) -> UnaryOperatorToken:
        return self._operator

    def operand(self) -> Expression:
        return self._operand

    def __repr__(self) -> str:
        return f"UnaryOperation(operator={self._operator}, operand={self._operand})"


class UnarySubtraction(UnaryOperatorToken):
    """
    The unary subtraction operator -.
    """

    def __init__(self, source: NodeSource | None = None):
        super().__init__(source)

    def __repr__(self) -> str:
        return "UnarySubtraction()"


class While(Node):
    """
    A while loop.
    """

    def __init__(
        self,
        test: Node,
        body: Sequence[Node],
        orelse: Sequence[Node] | None = None,
        source: NodeSource | None = None,
    ):
        super().__init__(source)
        self._test: Node = test
        self._body: Sequence[Node] = body
        self._orelse: Sequence[Node] = orelse if orelse is not None else []

    def test(self) -> Node:
        return self._test

    def body(self) -> Sequence[Node]:
        return self._body

    def orelse(self) -> Sequence[Node]:
        return self._orelse

    def __repr__(self) -> str:
        return f"While(test={self._test}, body={self._body}, orelse={self._orelse})"


class withitem(Node):
    """
    An item in a with statement, which typically includes an expression and an optional variable to assign the context manager to.
    """

    def __init__(
        self,
        context_expr: Node,
        optional_vars: Node | None = None,
        source: NodeSource | None = None,
    ):
        super().__init__(source)
        self._context_expr: Node = context_expr
        self._optional_vars: Node | None = optional_vars

    def context_expr(self) -> Node:
        return self._context_expr

    def optional_vars(self) -> Node | None:
        return self._optional_vars

    def __repr__(self) -> str:
        return f"withitem(context_expr={self._context_expr}, optional_vars={self._optional_vars})"


class With(Node):
    """
    A with statement, which is used to wrap the execution of a block with methods defined by a context manager.
    """

    def __init__(
        self, items: Sequence[withitem], body: Sequence[Node], source: NodeSource | None = None
    ):
        super().__init__(source)
        self._items: Sequence[withitem] = items
        self._body: Sequence[Node] = body

    def items(self) -> Sequence[Node]:
        return self._items

    def body(self) -> Sequence[Node]:
        return self._body

    def __repr__(self) -> str:
        return f"With(items={self._items}, body={self._body})"


class Yield(Node):
    """
    A yield statement, which is used to produce a value from a generator function.
    """

    def __init__(self, value: Node | None = None, source: NodeSource | None = None):
        super().__init__(source)
        self._value: Node | None = value

    def value(self) -> Node | None:
        return self._value

    def __repr__(self) -> str:
        return f"Yield(value={self._value})"


class YieldFrom(Node):
    """
    A yield from statement, which is used to yield all values from an iterable.
    """

    def __init__(self, value: Node, source: NodeSource | None = None):
        super().__init__(source)
        self._value: Node = value

    def value(self) -> Node:
        return self._value

    def __repr__(self) -> str:
        return f"YieldFrom(value={self._value})"


## Functions


def parse(source: io.IOBase | str, filename: str = "<unknown>") -> Node | None:
    # TODO
    return Node()


def unparse(node: Node) -> str:
    # TODO
    return ""


def walk(node: Node) -> Node | None:
    """
    Walks through the AST in no particular order.
    """
    # TODO
    return None


class NodeVisitor:
    pass

    def visit(self, node: Node) -> None:
        """
        Visits a node and calls the appropriate visit method based on the
        node type.
        """
        # TODO
        pass
