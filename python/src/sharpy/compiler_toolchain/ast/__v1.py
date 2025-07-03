import io

from typing import Mapping, Sequence

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


class Literal(Node):
    """
    Things like constants, strings, and collection literals like lists.
    """

    def __init__(self, source: NodeSource | None = None):
        super().__init__(source)


class Statement(Node):
    def __init__(self, source: NodeSource | None = None):
        super().__init__(source)


class Variable(Node):
    def __init__(self, source: NodeSource | None = None):
        super().__init__(source)


class Expression(Node):
    def __init__(self, source: NodeSource | None = None):
        super().__init__(source)


class UnaryOpTok(Node):
    def __init__(self, source: NodeSource | None = None):
        super().__init__(source)


class BinaryOpTok(Node):
    def __init__(self, source: NodeSource | None = None):
        super().__init__(source)


class ComparisonOpTok(Node):
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


class Add(BinaryOpTok):
    """
    The addition operator +.
    """

    def __init__(self, source: NodeSource | None = None):
        super().__init__(source)


class alias(Node):
    def __init__(self, name: str, asname: str | None, source: NodeSource | None = None):
        super().__init__(source)
        self._name: str = name
        self._asname: str | None = asname

    def name(self) -> str:
        return self._name

    def asname(self) -> str | None:
        return self._asname


class And(Node):
    """
    The `and` keyword, used as the logical AND operator.
    """

    def __init__(self, source: NodeSource | None = None):
        super().__init__(source)


class AnnAssign(Statement):
    """
    An annotated assignment expression of the form:
        x: y = z
    """

    def __init__(self, source: NodeSource | None = None):
        super().__init__(source)


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


class parameters(Node):
    """
    An parameter list in a function call.
    """

    def __init__(self, params: Mapping[str, param]):
        super().__init__()
        self._params: Mapping[str, param] = params

    def params(self) -> Mapping[str, param]:
        return self._params


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


class AugAssign(Statement):
    """
    An augmented assignment statement of the form:
        x += y
    where + is any valid binary operator.
    """

    def __init__(
        self, target: Node, op: BinaryOpTok, value: Node, source: NodeSource | None = None
    ):
        super().__init__(source)
        self._target: Node = target
        self._op: Node = op
        self._value: Node = value

    def target(self) -> Node:
        return self._target

    def op(self) -> Node:
        return self._op

    def value(self) -> Node:
        return self._value


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


class BinOp(Node):
    """
    A binary operation of the form:
        x + y
    where + is any valid binary operator.
    """

    def __init__(self, left: Node, op: BinaryOpTok, right: Node, source: NodeSource | None = None):
        super().__init__(source)
        self._left: Node = left
        self._op: BinaryOpTok = op
        self._right: Node = right

    def left(self) -> Node:
        return self._left

    def op(self) -> BinaryOpTok:
        return self._op

    def right(self) -> Node:
        return self._right


class BitAnd(BinaryOpTok):
    """
    The bitwise AND operator &.
    """

    def __init__(self, source: NodeSource | None = None):
        super().__init__(source)


class BitOr(BinaryOpTok):
    """
    The bitwise OR operator |.
    """

    def __init__(self, source: NodeSource | None = None):
        super().__init__(source)


class BitXor(BinaryOpTok):
    """
    The bitwise XOR operator ^.
    """

    def __init__(self, source: NodeSource | None = None):
        super().__init__(source)


class BoolOp(Expression):
    """
    A boolean operation of the form:
        x and y and z
    """

    def __init__(self, op: "Or | And", values: Sequence[Node], source: NodeSource | None = None):
        super().__init__(source)
        self._op: Or | And = op
        self._values: Sequence[Node] = values

    def op(self) -> "Or | And":
        return self._op

    def values(self) -> Sequence[Node]:
        return self._values


class Break(Node):
    """
    A break statement.
    """

    def __init__(self, source: NodeSource | None = None):
        super().__init__(source)


class Bytes(Node):
    """
    A bytes literal.
    """

    def __init__(self, value: bytes, source: NodeSource | None = None):
        super().__init__(source)
        self._value: bytes = value

    def value(self) -> bytes:
        return self._value


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


class ClassDef(Node):
    """
    A class definition.
    """

    def __init__(
        self,
        name: str,
        bases: Sequence[Node],
        keywords: Sequence[Node],
        body: Sequence[Node],
        source: NodeSource | None = None,
    ):
        super().__init__(source)
        self._name: str = name
        self._bases: Sequence[Node] = bases
        self._keywords: Sequence[Node] = keywords
        self._body: Sequence[Node] = body

    def name(self) -> str:
        return self._name

    def bases(self) -> Sequence[Node]:
        return self._bases

    def keywords(self) -> Sequence[Node]:
        return self._keywords

    def body(self) -> Sequence[Node]:
        return self._body


class Compare(Expression):
    """
    A comparison operation of the form:
        x < y
    where < is any valid comparison operator.
    """

    def __init__(
        self,
        left: Node,
        ops: Sequence[ComparisonOpTok],
        comparators: Sequence[Node],
        source: NodeSource | None = None,
    ):
        super().__init__(source)
        self._left: Node = left
        self._ops: Sequence[ComparisonOpTok] = ops
        self._comparators: Sequence[Node] = comparators

    def left(self) -> Node:
        return self._left

    def ops(self) -> Sequence[ComparisonOpTok]:
        return self._ops

    def comparators(self) -> Sequence[Node]:
        return self._comparators


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

    def target(self) -> "Name | Tuple":
        return self._target

    def iter(self) -> Node:
        return self._iter

    def ifs(self) -> Sequence[Node]:
        return self._ifs


class Constant(Literal):
    """
    A constant value, such as a string, number, or boolean.
    """

    def __init__(
        self,
        value: str | bytes | complex | int | float | bool | None,
        source: NodeSource | None = None,
    ):
        super().__init__(source)
        self._value: str | int | float | bool | bytes | complex | None = value

    def value(self) -> str | int | float | complex | bytes | bool | None:
        return self._value


class Continue(Node):
    """
    A continue statement.
    """

    def __init__(self, source: NodeSource | None = None):
        super().__init__(source)


class Del(Context):
    """
    Indicates a variable is being deleted.
    """

    def __init__(self, source: NodeSource | None = None):
        super().__init__(source)


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


class Dict(Literal):
    """
    A dictionary literal of the form:
        {key1: value1, key2: value2}
    """

    def __init__(
        self, keys: Sequence[Node], values: Sequence[Node], source: NodeSource | None = None
    ):
        super().__init__(source)
        self._keys: Sequence[Node] = keys
        self._values: Sequence[Node] = values

    def keys(self) -> Sequence[Node]:
        return self._keys

    def values(self) -> Sequence[Node]:
        return self._values


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


class Div(BinaryOpTok):
    """
    The division operator /.
    """

    def __init__(self, source: NodeSource | None = None):
        super().__init__(source)


class Ellipsis(Node):
    """
    An ellipsis literal, represented by the `...` token.
    """

    def __init__(self, source: NodeSource | None = None):
        super().__init__(source)


class Eq(ComparisonOpTok):
    """
    The equality operator.
    """

    def __init__(self, source: NodeSource | None = None):
        super().__init__(source)


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


class expr(Node):
    """
    A generic expression node.
    """

    def __init__(self, source: NodeSource | None = None):
        super().__init__(source)


class FloorDiv(BinaryOpTok):
    """
    The floor division operator //.
    """

    def __init__(self, source: NodeSource | None = None):
        super().__init__(source)


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
        super().__init__(source)
        self._value: Node = value
        self._conversion: int = conversion
        self._format_spec: Node | None = format_spec

    def value(self) -> Node:
        return self._value

    def conversion(self) -> int:
        return self._conversion

    def format_spec(self) -> Node | None:
        return self._format_spec


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


class Global(Node):
    """
    A global statement, which declares global variables.
    """

    def __init__(self, names: Sequence[str], source: NodeSource | None = None):
        super().__init__(source)
        self._names: Sequence[str] = names

    def names(self) -> Sequence[str]:
        return self._names


class Gt(ComparisonOpTok):
    """
    The greater than operator >.
    """

    def __init__(self, source: NodeSource | None = None):
        super().__init__(source)


class GtE(ComparisonOpTok):
    """
    The greater than or equal to operator >=.
    """

    def __init__(self, source: NodeSource | None = None):
        super().__init__(source)


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


class Import(Node):
    """
    An import statement, which imports modules or specific names from modules.
    """

    def __init__(self, names: Sequence[alias], source: NodeSource | None = None):
        super().__init__(source)
        self._names: Sequence[alias] = names

    def names(self) -> Sequence[alias]:
        return self._names


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


class In(ComparisonOpTok):
    """
    The `in` keyword, which is the membership operator, used to check if a
    value is in a sequence.
    """

    def __init__(self, source: NodeSource | None = None):
        super().__init__(source)


class Index(Node):
    """
    An index expression, which is used to access elements in a sequence.
    """

    def __init__(self, value: Node, source: NodeSource | None = None):
        super().__init__(source)
        self._value: Node = value

    def value(self) -> Node:
        return self._value


class Is(ComparisonOpTok):
    """
    The `is` keyword, which is the identity operator, used to check if two
    values are the same object.
    """

    def __init__(self, source: NodeSource | None = None):
        super().__init__(source)


class IsNot(ComparisonOpTok):
    """
    The `is not` keyword, which is the negative identity operator, used to
    check if two values are not the same object.
    """

    def __init__(self, source: NodeSource | None = None):
        super().__init__(source)


class Invert(UnaryOpTok):
    """
    The bitwise NOT operator ~.
    """

    def __init__(self, source: NodeSource | None = None):
        super().__init__(source)


class JoinedStr(Literal):
    """
    A joined string literal, which is a sequence of string literals concatenated together.
    """

    def __init__(
        self, values: Sequence[FormattedValue | Constant], source: NodeSource | None = None
    ):
        super().__init__(source)
        self._values: Sequence[FormattedValue | Constant] = values

    def values(self) -> Sequence[FormattedValue | Constant]:
        return self._values


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


class Lambda(Node):
    """
    A lambda function, which is an anonymous function defined with the `lambda` keyword.
    """

    def __init__(self, args: parameters, body: Node, source: NodeSource | None = None):
        super().__init__(source)
        self._args: parameters = args
        self._body: Node = body

    def args(self) -> parameters:
        return self._args

    def body(self) -> Node:
        return self._body


class List(Literal):
    """
    A list literal, which is a sequence of values enclosed in square brackets.
    """

    def __init__(self, elts: Sequence[Node], ctx: Node, source: NodeSource | None = None):
        super().__init__(source)
        self._elts: Sequence[Node] = elts
        self._ctx: Node = ctx

    def elts(self) -> Sequence[Node]:
        return self._elts

    def ctx(self) -> Node:
        return self._ctx


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


class Load(Context):
    """
    A load context, which indicates that a value is being read.
    """

    def __init__(self, source: NodeSource | None = None):
        super().__init__(source)


class LShift(BinaryOpTok):
    """
    The left shift operator <<.
    """

    def __init__(self, source: NodeSource | None = None):
        super().__init__(source)


class Lt(ComparisonOpTok):
    """
    The less than operator <.
    """

    def __init__(self, source: NodeSource | None = None):
        super().__init__(source)


class LtE(ComparisonOpTok):
    """
    The less than or equal to operator <=.
    """

    def __init__(self, source: NodeSource | None = None):
        super().__init__(source)


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


class MatchOr(Node):
    """
    A pattern that matches any of the provided patterns.
    """

    def __init__(self, patterns: Sequence[Node], source: NodeSource | None = None):
        super().__init__(source)
        self._patterns: Sequence[Node] = patterns

    def patterns(self) -> Sequence[Node]:
        return self._patterns


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


class MatchSingleton(Node):
    """
    A pattern that matches a singleton value.
    """

    def __init__(self, value: Node, source: NodeSource | None = None):
        super().__init__(source)
        self._value: Node = value

    def value(self) -> Node:
        return self._value


class MatchStar(Node):
    """
    A pattern that matches a variable number of elements in a sequence.
    """

    def __init__(self, name: str | None, source: NodeSource | None = None):
        super().__init__(source)
        self._name: str | None = name

    def name(self) -> str | None:
        return self._name


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


class MatMult(Node):
    """
    The matrix multiplication operator *.
    """

    def __init__(self, source: NodeSource | None = None):
        super().__init__(source)


class Mod(BinaryOpTok):
    """
    The modulo operator %.
    """

    def __init__(self, source: NodeSource | None = None):
        super().__init__(source)


class Module(Root):
    """
    A module, which is a collection of statements and definitions.
    """

    def __init__(self, body: Sequence[Node], source: NodeSource | None = None):
        super().__init__(source)
        self._body: Sequence[Node] = body

    def body(self) -> Sequence[Node]:
        return self._body


class Mult(BinaryOpTok):
    """
    The multiplication operator *.
    """

    def __init__(self, source: NodeSource | None = None):
        super().__init__(source)


class Name(Variable):
    """
    A name, which is an identifier in the code.
    """

    def __init__(self, id: str, ctx: Node, source: NodeSource | None = None):
        super().__init__(source)
        self._id: str = id
        self._ctx: Node = ctx

    def id(self) -> str:
        return self._id

    def ctx(self) -> Node:
        return self._ctx


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


class Nonlocal(Node):
    """
    A nonlocal statement, which declares nonlocal variables.
    """

    def __init__(self, names: Sequence[str], source: NodeSource | None = None):
        super().__init__(source)
        self._names: Sequence[str] = names

    def names(self) -> Sequence[str]:
        return self._names


class Not(UnaryOpTok):
    """
    The `not` keyword, used for logical negation.
    """

    def __init__(self, source: NodeSource | None = None):
        super().__init__(source)


class NotEq(ComparisonOpTok):
    """
    The not equal operator.
    """

    def __init__(self, source: NodeSource | None = None):
        super().__init__(source)


class NotIn(Node):
    """
    The `not in` keyword, which is the negative membership operator, used to
    check if a value is not in a sequence.
    """

    def __init__(self, source: NodeSource | None = None):
        super().__init__(source)


class Or(Node):
    """
    The `or` keyword, which is the logical OR operator.
    """

    def __init__(self, source: NodeSource | None = None):
        super().__init__(source)


class Param(Node):
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


class ParamSpec(Node):
    """
    A parameter specification, which is used to define the parameters of a function.
    """

    def __init__(self, name: str, kind: str, source: NodeSource | None = None):
        super().__init__(source)
        self._name: str = name
        self._kind: str = kind  # e.g., 'positional', 'keyword', etc.

    def name(self) -> str:
        return self._name

    def kind(self) -> str:
        return self._kind


class Pass(Statement):
    """
    A pass statement, which does nothing and is used as a placeholder.
    """

    def __init__(self, source: NodeSource | None = None):
        super().__init__(source)
        # No additional attributes needed for a pass statement


class Pow(BinaryOpTok):
    """
    The power operator **, used for exponentiation.
    """

    def __init__(self, source: NodeSource | None = None):
        super().__init__(source)


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


class Return(Node):
    """
    A return statement, which is used to return a value from a function.
    """

    def __init__(self, value: Node | None = None, source: NodeSource | None = None):
        super().__init__(source)
        self._value: Node | None = value

    def value(self) -> Node | None:
        return self._value


class RShift(BinaryOpTok):
    """
    The right shift operator >>.
    """

    def __init__(self, source: NodeSource | None = None):
        super().__init__(source)


class Set(Literal):
    """
    A set literal, which is a collection of unique values enclosed in curly braces.
    """

    def __init__(self, elts: Sequence[Node], source: NodeSource | None = None):
        super().__init__(source)
        self._elts: Sequence[Node] = elts

    def elts(self) -> Sequence[Node]:
        return self._elts


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


class Starred(Variable):
    """
    A starred expression, which is used to unpack elements from a sequence.
    This is also used when building a Call node with *args.
    """

    def __init__(self, value: Node, ctx: Node, source: NodeSource | None = None):
        super().__init__(source)
        self._value: Node = value
        self._ctx: Node = ctx

    def value(self) -> Node:
        return self._value

    def ctx(self) -> Node:
        return self._ctx


class Store(Context):
    """
    A store context, which indicates that a value is being written to.
    """

    def __init__(self, source: NodeSource | None = None):
        super().__init__(source)


class Str(Node):
    """
    A string literal.
    """

    def __init__(self, value: str, source: NodeSource | None = None):
        super().__init__(source)
        self._value: str = value

    def value(self) -> str:
        return self._value


class Sub(BinaryOpTok):
    """
    The subtraction operator -.
    """

    def __init__(self, source: NodeSource | None = None):
        super().__init__(source)


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


class TryStar(Node):
    """
    A try statement with a star expression, which is used to handle exceptions with a star pattern.
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


class Tuple(Literal):
    """
    A tuple literal, which is a sequence of values enclosed in parentheses.
    """

    def __init__(self, elts: Sequence[Node], ctx: Node, source: NodeSource | None = None):
        super().__init__(source)
        self._elts: Sequence[Node] = elts
        self._ctx: Node = ctx

    def elts(self) -> Sequence[Node]:
        return self._elts

    def ctx(self) -> Node:
        return self._ctx


class TypeAlias(Statement):
    """
    A type alias, which is used to define a new name for an existing type.
    """

    def __init__(self, name: str, value: Node, source: NodeSource | None = None):
        super().__init__(source)
        self._name: str = name
        self._value: Node = value

    def name(self) -> str:
        return self._name

    def value(self) -> Node:
        return self._value


class TypeVar(Node):
    """
    A type variable, which is used to define generic types.
    """

    def __init__(
        self,
        name: str,
        bound: Node | None = None,
        constraints: Sequence[Node] | None = None,
        source: NodeSource | None = None,
    ):
        super().__init__(source)
        self._name: str = name
        self._bound: Node | None = bound
        self._constraints: Sequence[Node] = constraints if constraints is not None else []

    def name(self) -> str:
        return self._name

    def bound(self) -> Node | None:
        return self._bound

    def constraints(self) -> Sequence[Node]:
        return self._constraints


class TypeVarTuple(Node):
    """
    A type variable tuple, which is used to define a variable-length tuple type.
    """

    def __init__(self, name: str, source: NodeSource | None = None):
        super().__init__(source)
        self._name: str = name

    def name(self) -> str:
        return self._name


class UAdd(UnaryOpTok):
    """
    The unary addition operator +.
    """

    def __init__(self, source: NodeSource | None = None):
        super().__init__(source)


class UnaryOp(Expression):
    """
    A unary operation, which is an operation that takes a single operand.
    """

    def __init__(self, op: UnaryOpTok, operand: Expression, source: NodeSource | None = None):
        super().__init__(source)
        self._op: UnaryOpTok = op
        self._operand: Expression = operand

    def op(self) -> UnaryOpTok:
        return self._op

    def operand(self) -> Expression:
        return self._operand


class USub(UnaryOpTok):
    """
    The unary subtraction operator -.
    """

    def __init__(self, source: NodeSource | None = None):
        super().__init__(source)


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


class Yield(Node):
    """
    A yield statement, which is used to produce a value from a generator function.
    """

    def __init__(self, value: Node | None = None, source: NodeSource | None = None):
        super().__init__(source)
        self._value: Node | None = value

    def value(self) -> Node | None:
        return self._value


class YieldFrom(Node):
    """
    A yield from statement, which is used to yield all values from an iterable.
    """

    def __init__(self, value: Node, source: NodeSource | None = None):
        super().__init__(source)
        self._value: Node = value

    def value(self) -> Node:
        return self._value


## Functions


def parse(source: io.IOBase | str, filename: str = "<unknown>") -> Node:
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
