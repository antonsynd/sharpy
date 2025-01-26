from enum import Enum, auto
from io import StringIO
from typing import AbstractSet, Dict, MutableSequence, Optional, Sequence, Tuple, Type, Union


class Source:
    """
    Container for the source of a particular AST node.
    """

    def __init__(
        self, lineno: int = -1, col_offset: int = -1, end_lineno: int = -1, end_col_offset: int = -1
    ):
        self.lineno: int = lineno
        self.col_offset: int = col_offset
        self.end_lineno: int = end_lineno
        self.end_col_offset: int = end_col_offset

    def __repr__(self) -> str:
        buffer = StringIO()
        buffer.write("Source(")

        properties: Sequence[str] = [
            f"lineno = {self.lineno}"
            f"col_offset = {self.col_offset}"
            f"end_lineno = {self.end_lineno}"
            f"end_col_offset = {self.end_col_offset}"
        ]

        buffer.write(", ".join(properties))
        buffer.write(")")

        return buffer.getvalue()


class AST:
    """
    Base class for all AST nodes.
    """

    def __init__(self, source: Optional[Source] = None):
        self.source: Optional[Source] = source

    def _name(self) -> str:
        return "AST"

    def _properties(self) -> Sequence[str]:
        return []

    def __repr__(self) -> str:
        buffer = StringIO()
        buffer.write(f"{self._name()}(")

        props: MutableSequence[str] = [f"source = {self.source}"]
        props.extend(self._properties())

        buffer.write(", ".join(props))
        buffer.write(")")

        return buffer.getvalue()


class Module(AST):
    """
    Contains the code for a module (a file).
    """

    def __init__(self, body: MutableSequence[AST]):
        super().__init__()

        self.body: MutableSequence[AST] = body

    def _name(self) -> str:
        return "Module"

    def _properties(self) -> Sequence[str]:
        return [
            f"source = {self.source}",
            f"body = {self.body}",
        ]


class FunctionType(AST):
    """
    TODO: should be function signature to include the argument names.
    """

    def __init__(self, argtypes: MutableSequence[AST], returns: Optional[AST] = None):
        super().__init__()

        self.argtypes: MutableSequence[AST] = argtypes
        self.returns: Optional[AST] = returns

    def __repr__(self) -> str:
        buffer = StringIO()
        buffer.write("FunctionType(")

        properties: Sequence[str] = [
            f"source = {self.source}",
            f"argtypes = {self.argtypes}",
            f"returns = {self.returns}",
        ]

        buffer.write(", ".join(properties))
        buffer.write(")")

        return buffer.getvalue()


class ConstantType(Enum):
    BIG_INT = "bigint"
    BYTE = "byte"
    BYTES = "bytes"
    COMPLEX = "complex"
    DOUBLE = "double"
    ELLIPSIS = "Ellipsis"  # Maybe ... ?
    FLOAT = "float"
    INT = "int"
    NONE = "None"
    STR = "str"


class Constant(AST):
    """
    Any literal.
    """

    def __init__(self, value: str, kind: ConstantType):
        super().__init__()

        self.value: str = value
        self.kind: ConstantType = kind

    def __repr__(self) -> str:
        buffer = StringIO()
        buffer.write("Constant(")

        properties: Sequence[str] = [
            f"source = {self.source}",
            f"value = {self.value}",
            f"kind = {self.kind}",
        ]

        buffer.write(", ".join(properties))
        buffer.write(")")

        return buffer.getvalue()


class FormattedValue(AST):
    """
    Single formatting field in an f-string

    {:0f}
    """

    def __init__(self, value: AST, conversion: int, format_spec: AST):
        super().__init__()

        self.value: AST = value
        self.conversion: int = conversion
        self.format_spec: AST = format_spec

    def __repr__(self) -> str:
        buffer = StringIO()
        buffer.write("FormattedValue(")

        properties: Sequence[str] = [
            f"source = {self.source}",
            f"value = {self.value}",
            f"conversion = {self.conversion}",
            f"format_spec = {self.format_spec}",
        ]

        buffer.write(", ".join(properties))
        buffer.write(")")

        return buffer.getvalue()


class JoinedStr(AST):
    """
    An f-string.

    f"foobar"
    """

    def __init__(self, values: MutableSequence[AST]):
        super().__init__()

        self.values: MutableSequence[Union[FormattedValue, Constant]] = values

    def __repr__(self) -> str:
        buffer = StringIO()
        buffer.write("JoinedStr(")

        properties: Sequence[str] = [
            f"source = {self.source}",
            f"values = {self.values}",
        ]

        buffer.write(", ".join(properties))
        buffer.write(")")

        return buffer.getvalue()


class List(AST):
    """
    A list literal.

    [a, b] = [1, 3]  # store (assignment target)
    a = [1, 3]       # load
    """

    def __init__(self, elts: MutableSequence[AST], ctx: AST):
        super().__init__()

        self.elts: MutableSequence[AST] = elts
        # Store if assignment target, else Load
        self.ctx: Union[Store, Load] = ctx

    def __repr__(self) -> str:
        buffer = StringIO()
        buffer.write("List(")

        properties: Sequence[str] = [
            f"source = {self.source}",
            f"elts = {self.elts}",
            f"ctx = {self.ctx}",
        ]

        buffer.write(", ".join(properties))
        buffer.write(")")

        return buffer.getvalue()


class Tuple(AST):
    """
    A tuple literal.

    (a, b) = [1, 3]  # store (assignment target)
    a = (1, 3)       # load
    """

    def __init__(self, elts: MutableSequence[AST], ctx: AST):
        super().__init__()

        self.elts: MutableSequence[AST] = elts
        # Store if assignment target, else Load
        self.ctx: Union[Store, Load] = ctx

    def __repr__(self) -> str:
        buffer = StringIO()
        buffer.write("Tuple(")

        properties: Sequence[str] = [
            f"source = {self.source}",
            f"elts = {self.elts}",
            f"ctx = {self.ctx}",
        ]

        buffer.write(", ".join(properties))
        buffer.write(")")

        return buffer.getvalue()


class Set(AST):
    """
    A set literal.
    """

    def __init__(self, elts: MutableSequence[AST]):
        super().__init__()

        self.elts: MutableSequence[AST] = elts

    def __repr__(self) -> str:
        buffer = StringIO()
        buffer.write("Set(")

        properties: Sequence[str] = [
            f"source = {self.source}",
            f"elts = {self.elts}",
        ]

        buffer.write(", ".join(properties))
        buffer.write(")")

        return buffer.getvalue()


class Dict(AST):
    """
    A dict literal.
    """

    def __init__(self, keys: MutableSequence[AST], values: MutableSequence[AST]):
        super().__init__()

        # Unpacking puts the AST into values and None at keys
        # {"a": 5, *other}
        # keys = {"a", None}
        # values = {5, values of other}
        self.keys: MutableSequence[AST] = keys
        self.values: MutableSequence[AST] = values

    def __repr__(self) -> str:
        buffer = StringIO()
        buffer.write("Dict(")

        properties: Sequence[str] = [
            f"source = {self.source}",
            f"keys = {self.keys}",
            f"values = {self.values}",
        ]

        buffer.write(", ".join(properties))
        buffer.write(")")

        return buffer.getvalue()


class Context(AST):
    def __init__(self):
        super().__init__()

    def __repr__(self) -> str:
        buffer = StringIO()
        buffer.write("Context(")

        properties: Sequence[str] = [
            f"source = {self.source}",
        ]

        buffer.write(", ".join(properties))
        buffer.write(")")

        return buffer.getvalue()


class Name(AST):
    """
    Any identifier.
    """

    def __init__(self, id: str, ctx: Context):
        super().__init__()

        self.id: str = id
        self.ctx: Context = ctx

    def __repr__(self) -> str:
        buffer = StringIO()
        buffer.write("Name(")

        properties: Sequence[str] = [
            f"source = {self.source}",
            f"id = {self.id}",
            f"ctx = {self.ctx}",
        ]

        buffer.write(", ".join(properties))
        buffer.write(")")

        return buffer.getvalue()


class Load(Context):
    """
    Context where a name is loaded (accessed).

    ... = name
    """

    def __init__(self):
        super().__init__()

    def __repr__(self) -> str:
        buffer = StringIO()
        buffer.write("Load(")

        properties: Sequence[str] = [
            f"source = {self.source}",
        ]

        buffer.write(", ".join(properties))
        buffer.write(")")

        return buffer.getvalue()


class Store(Context):
    """
    Context where a name is the target for storing a value (setting).

    name = ...
    """

    def __init__(self):
        super().__init__()

    def __repr__(self) -> str:
        buffer = StringIO()
        buffer.write("Store(")

        properties: Sequence[str] = [
            f"source = {self.source}",
        ]

        buffer.write(", ".join(properties))
        buffer.write(")")

        return buffer.getvalue()


class Del(Context):
    """
    Context where a name is the target for deletion.

    del name
    """

    def __init__(self):
        super().__init__()

    def __repr__(self) -> str:
        buffer = StringIO()
        buffer.write("Del(")

        properties: Sequence[str] = [
            f"source = {self.source}",
        ]

        buffer.write(", ".join(properties))
        buffer.write(")")

        return buffer.getvalue()


class Starred(AST):
    def __init__(self, value: Name, ctx: Context):
        # TODO: ctx = Store() by default?
        super().__init__()

        self.value: Name = value
        self.ctx: Context = ctx

    def __repr__(self) -> str:
        buffer = StringIO()
        buffer.write("Starred(")

        properties: Sequence[str] = [
            f"source = {self.source}",
            f"value = {self.value}",
            f"ctx = {self.ctx}",
        ]

        buffer.write(", ".join(properties))
        buffer.write(")")

        return buffer.getvalue()


class Expr(AST):
    def __init__(self, value: AST):
        super().__init__()
        self.value: Union[Constant, Name, Lambda, Yield, YieldFrom] = value

    def __repr__(self) -> str:
        buffer = StringIO()
        buffer.write("Expr(")

        properties: Sequence[str] = [
            f"source = {self.source}",
            f"value = {self.value}",
        ]

        buffer.write(", ".join(properties))
        buffer.write(")")

        return buffer.getvalue()


class UnaryOp(AST):
    def __init__(self, op: AST, operand: AST):
        super().__init__()

        self.op: Union[UAdd, USub, Not, Invert] = op
        self.operand: AST = operand

    def __repr__(self) -> str:
        buffer = StringIO()
        buffer.write("UnaryOp(")

        properties: Sequence[str] = [
            f"source = {self.source}",
            f"op = {self.op}",
            f"operand = {self.operand}",
        ]

        buffer.write(", ".join(properties))
        buffer.write(")")

        return buffer.getvalue()


class UAdd(AST):
    def __init__(self):
        super().__init__()

    def __repr__(self) -> str:
        buffer = StringIO()
        buffer.write("UAdd(")

        properties: Sequence[str] = [
            f"source = {self.source}",
        ]

        buffer.write(", ".join(properties))
        buffer.write(")")

        return buffer.getvalue()


class USub(AST):
    def __init__(self):
        super().__init__()

    def __repr__(self) -> str:
        buffer = StringIO()
        buffer.write("USub(")

        properties: Sequence[str] = [
            f"source = {self.source}",
        ]

        buffer.write(", ".join(properties))
        buffer.write(")")

        return buffer.getvalue()


class Not(AST):
    def __init__(self):
        super().__init__()

    def __repr__(self) -> str:
        buffer = StringIO()
        buffer.write("Not(")

        properties: Sequence[str] = [
            f"source = {self.source}",
        ]

        buffer.write(", ".join(properties))
        buffer.write(")")

        return buffer.getvalue()


class Invert(AST):
    def __init__(self):
        super().__init__()

    def __repr__(self) -> str:
        buffer = StringIO()
        buffer.write("Invert(")

        properties: Sequence[str] = [
            f"source = {self.source}",
        ]

        buffer.write(", ".join(properties))
        buffer.write(")")

        return buffer.getvalue()


class BinOp(AST):
    def __init__(self, left: AST, op: AST, right: AST):
        super().__init__()

        self.left: AST
        self.op = Union[
            Add, Sub, Mult, Div, FloorDiv, Mod, Pow, LShift, RShift, BitOr, BitXor, BitAnd, MatMult
        ]
        self.right: AST

    def __repr__(self) -> str:
        buffer = StringIO()
        buffer.write("BinOp(")

        properties: Sequence[str] = [
            f"source = {self.source}",
            f"left = {self.left}",
            f"op = {self.op}",
            f"right = {self.right}",
        ]

        buffer.write(", ".join(properties))
        buffer.write(")")

        return buffer.getvalue()


class Add(AST):
    def __init__(self):
        super().__init__()

    def __repr__(self) -> str:
        buffer = StringIO()
        buffer.write("Add(")

        properties: Sequence[str] = [
            f"source = {self.source}",
        ]

        buffer.write(", ".join(properties))
        buffer.write(")")

        return buffer.getvalue()


class Sub(AST):
    def __init__(self):
        super().__init__()

    def __repr__(self) -> str:
        buffer = StringIO()
        buffer.write("Sub(")

        properties: Sequence[str] = [
            f"source = {self.source}",
        ]

        buffer.write(", ".join(properties))
        buffer.write(")")

        return buffer.getvalue()


class Mult(AST):
    def __init__(self):
        super().__init__()

    def __repr__(self) -> str:
        buffer = StringIO()
        buffer.write("Mult(")

        properties: Sequence[str] = [
            f"source = {self.source}",
        ]

        buffer.write(", ".join(properties))
        buffer.write(")")

        return buffer.getvalue()


class Div(AST):
    def __init__(self):
        super().__init__()

    def __repr__(self) -> str:
        buffer = StringIO()
        buffer.write("Div(")

        properties: Sequence[str] = [
            f"source = {self.source}",
        ]

        buffer.write(", ".join(properties))
        buffer.write(")")

        return buffer.getvalue()


class FloorDiv(AST):
    def __init__(self):
        super().__init__()

    def __repr__(self) -> str:
        buffer = StringIO()
        buffer.write("FloorDiv(")

        properties: Sequence[str] = [
            f"source = {self.source}",
        ]

        buffer.write(", ".join(properties))
        buffer.write(")")

        return buffer.getvalue()


class Mod(AST):
    def __init__(self):
        super().__init__()

    def __repr__(self) -> str:
        buffer = StringIO()
        buffer.write("Mod(")

        properties: Sequence[str] = [
            f"source = {self.source}",
        ]

        buffer.write(", ".join(properties))
        buffer.write(")")

        return buffer.getvalue()


class Pow(AST):
    def __init__(self):
        super().__init__()

    def __repr__(self) -> str:
        buffer = StringIO()
        buffer.write("Pow(")

        properties: Sequence[str] = [
            f"source = {self.source}",
        ]

        buffer.write(", ".join(properties))
        buffer.write(")")

        return buffer.getvalue()


class LShift(AST):
    def __init__(self):
        super().__init__()

    def __repr__(self) -> str:
        buffer = StringIO()
        buffer.write("LShift(")

        properties: Sequence[str] = [
            f"source = {self.source}",
        ]

        buffer.write(", ".join(properties))
        buffer.write(")")

        return buffer.getvalue()


class RShift(AST):
    def __init__(self):
        super().__init__()

    def __repr__(self) -> str:
        buffer = StringIO()
        buffer.write("RShift(")

        properties: Sequence[str] = [
            f"source = {self.source}",
        ]

        buffer.write(", ".join(properties))
        buffer.write(")")

        return buffer.getvalue()


class BitOr(AST):
    def __init__(self):
        super().__init__()

    def __repr__(self) -> str:
        buffer = StringIO()
        buffer.write("BitOr(")

        properties: Sequence[str] = [
            f"source = {self.source}",
        ]

        buffer.write(", ".join(properties))
        buffer.write(")")

        return buffer.getvalue()


class BitXor(AST):
    def __init__(self):
        super().__init__()

    def __repr__(self) -> str:
        buffer = StringIO()
        buffer.write("BitXor(")

        properties: Sequence[str] = [
            f"source = {self.source}",
        ]

        buffer.write(", ".join(properties))
        buffer.write(")")

        return buffer.getvalue()


class BitAnd(AST):
    def __init__(self):
        super().__init__()

    def __repr__(self) -> str:
        buffer = StringIO()
        buffer.write("BitAnd(")

        properties: Sequence[str] = [
            f"source = {self.source}",
        ]

        buffer.write(", ".join(properties))
        buffer.write(")")

        return buffer.getvalue()


class MatMult(AST):
    """
    Matrix multiplication
    """

    def __init__(self):
        super().__init__()

    def __repr__(self) -> str:
        buffer = StringIO()
        buffer.write("MatMult(")

        properties: Sequence[str] = [
            f"source = {self.source}",
        ]

        buffer.write(", ".join(properties))
        buffer.write(")")

        return buffer.getvalue()


class BoolOp(AST):
    def __init__(self, op: AST, values: MutableSequence[AST]):
        super().__init__()

        self.op: Union[Or, And] = op
        self.values: MutableSequence[AST] = values

    def __repr__(self) -> str:
        buffer = StringIO()
        buffer.write("BoolOp(")

        properties: Sequence[str] = [
            f"source = {self.source}",
            f"op = {self.op}",
            f"values = {self.values}",
        ]

        buffer.write(", ".join(properties))
        buffer.write(")")

        return buffer.getvalue()


class Or(AST):
    def __init__(self):
        super().__init__()

    def __repr__(self) -> str:
        buffer = StringIO()
        buffer.write("Or(")

        properties: Sequence[str] = [
            f"source = {self.source}",
        ]

        buffer.write(", ".join(properties))
        buffer.write(")")

        return buffer.getvalue()


class And(AST):
    def __init__(self):
        super().__init__()

    def __repr__(self) -> str:
        buffer = StringIO()
        buffer.write("And(")

        properties: Sequence[str] = [
            f"source = {self.source}",
        ]

        buffer.write(", ".join(properties))
        buffer.write(")")

        return buffer.getvalue()


class Compare(AST):
    def __init__(self, left: AST, ops: MutableSequence[AST], comparators: MutableSequence[AST]):
        super().__init__()

        self.left: AST = left
        self.ops: MutableSequence[Union[Eq, NotEq, Lt, LtE, Gt, GtE, Is, IsNot, In, NotIn]] = ops
        self.comparators: MutableSequence[AST] = comparators

    def __repr__(self) -> str:
        buffer = StringIO()
        buffer.write("Compare(")

        properties: Sequence[str] = [
            f"source = {self.source}",
            f"left = {self.left}",
            f"ops = {self.ops}",
            f"comparators = {self.comparators}",
        ]

        buffer.write(", ".join(properties))
        buffer.write(")")

        return buffer.getvalue()


class Eq(AST):
    def __init__(self):
        super().__init__()

    def __repr__(self) -> str:
        buffer = StringIO()
        buffer.write("Eq(")

        properties: Sequence[str] = [
            f"source = {self.source}",
        ]

        buffer.write(", ".join(properties))
        buffer.write(")")

        return buffer.getvalue()


class NotEq(AST):
    def __init__(self):
        super().__init__()

    def __repr__(self) -> str:
        buffer = StringIO()
        buffer.write("NotEq(")

        properties: Sequence[str] = [
            f"source = {self.source}",
        ]

        buffer.write(", ".join(properties))
        buffer.write(")")

        return buffer.getvalue()


class Lt(AST):
    def __init__(self):
        super().__init__()

    def __repr__(self) -> str:
        buffer = StringIO()
        buffer.write("Lt(")

        properties: Sequence[str] = [
            f"source = {self.source}",
        ]

        buffer.write(", ".join(properties))
        buffer.write(")")

        return buffer.getvalue()


class LtE(AST):
    def __init__(self):
        super().__init__()

    def __repr__(self) -> str:
        buffer = StringIO()
        buffer.write("LtE(")

        properties: Sequence[str] = [
            f"source = {self.source}",
        ]

        buffer.write(", ".join(properties))
        buffer.write(")")

        return buffer.getvalue()


class Gt(AST):
    def __init__(self):
        super().__init__()

    def __repr__(self) -> str:
        buffer = StringIO()
        buffer.write("Gt(")

        properties: Sequence[str] = [
            f"source = {self.source}",
        ]

        buffer.write(", ".join(properties))
        buffer.write(")")

        return buffer.getvalue()


class GtE(AST):
    def __init__(self):
        super().__init__()

    def __repr__(self) -> str:
        buffer = StringIO()
        buffer.write("GtE(")

        properties: Sequence[str] = [
            f"source = {self.source}",
        ]

        buffer.write(", ".join(properties))
        buffer.write(")")

        return buffer.getvalue()


class Is(AST):
    def __init__(self):
        super().__init__()

    def __repr__(self) -> str:
        buffer = StringIO()
        buffer.write("Is(")

        properties: Sequence[str] = [
            f"source = {self.source}",
        ]

        buffer.write(", ".join(properties))
        buffer.write(")")

        return buffer.getvalue()


class IsNot(AST):
    def __init__(self):
        super().__init__()

    def _name(self) -> str:
        return "IsNot"


class In(AST):
    def __init__(self):
        super().__init__()

    def _name(self) -> str:
        return "In"


class NotIn(AST):
    def __init__(self):
        super().__init__()

    def _name(self) -> str:
        return "NotIn"


class Keyword(AST):
    def __init__(self, arg: str, value: AST):
        super().__init__()

        self.arg: str = arg
        self.value: AST = value

    def _name(self) -> str:
        return "Keyword"

    def _properties(self) -> Sequence[str]:
        return [
            f"source = {self.source}",
            f"arg = {self.arg}",
            f"value = {self.value}",
        ]


class Call(AST):
    def __init__(self, func: AST, args: MutableSequence[AST], keywords: MutableSequence[Keyword]):
        super().__init__()

        self.func: Union[Name, Attribute] = func
        self.args: MutableSequence[AST] = args
        self.keywords: MutableSequence[Keyword] = keywords

    def _name(self) -> str:
        return "Call"

    def _properties(self) -> Sequence[str]:
        return [
            f"source = {self.source}",
            f"func = {self.func}",
            f"args = {self.args}",
            f"keywords = {self.keywords}",
        ]


class IfExp(AST):
    def __init__(self, test: AST, body: AST, orelse: AST):
        super().__init__()

        self.test: AST
        self.body: AST
        self.orelse: AST

    def _name(self) -> str:
        return "IfExp"

    def _properties(self) -> Sequence[str]:
        return [
            f"test = {self.test}",
            f"body = {self.body}",
            f"orelse = {self.orelse}",
        ]


class Attribute(AST):
    def __init__(self, value: Name, attr: str, ctx: AST):
        super().__init__()

        self.value: Name = value
        self.attr: str = attr
        self.ctx: Union[Load, Store, Del] = ctx

    def _name(self) -> str:
        return "Attribute"

    def _properties(self) -> Sequence[str]:
        return [
            f"value = {self.value}",
            f"attr = {self.attr}",
            f"ctx = {self.ctx}",
        ]


class NamedExpr(AST):
    """
    if (n := "str"):
        print(n)
    """

    def __init__(self, target: Name, value: AST):
        super().__init__()

        self.target: Name = target
        self.value: AST = value

    def _name(self) -> str:
        return "NamedExpr"

    def _properties(self) -> Sequence[str]:
        return [
            f"target = {self.target}",
            f"value = {self.value}",
        ]


class Slice(AST):
    def __init__(self, lower: Optional[int], upper: Optional[int], step: Optional[int]):
        super().__init__()

        self.lower: Optional[int] = lower
        self.upper: Optional[int] = upper
        self.step: Optional[int] = step

    def _name(self) -> str:
        return "Slice"

    def _properties(self) -> Sequence[str]:
        return [
            f"lower = {self.lower}",
            f"upper = {self.upper}",
            f"step = {self.step}",
        ]


class Subscript(AST):
    def __init__(self, value: AST, slice: Slice, ctx: AST):
        super().__init__()

        self.value: AST = value
        self.slice: Slice = slice
        self.ctx: Union[Load, Store, Del] = ctx

    def _name(self) -> str:
        return "Subscript"

    def _properties(self) -> Sequence[str]:
        return [
            f"value = {self.value}",
            f"slice = {self.slice}",
            f"ctx = {self.ctx}",
        ]


class Comprehension(AST):
    def __init__(self, target: AST, iter: AST, ifs: MutableSequence[AST], is_async: bool):
        self.target: AST = target
        self.iter: AST = iter
        self.ifs: MutableSequence[AST] = ifs
        self.is_async: bool = is_async

    def _name(self) -> str:
        return "Comprehension"

    def _properties(self) -> Sequence[str]:
        return [
            f"target = {self.target}",
            f"iter = {self.iter}",
            f"ifs = {self.ifs}",
            f"is_async = {self.is_async}",
        ]


class CompBase(AST):
    def __init__(self, elt: AST, generators: MutableSequence[Comprehension]):
        super().__init__()

        self.elt: AST = elt
        self.generators: MutableSequence[Comprehension] = generators

    def _name(self) -> str:
        return "CompBase"

    def _properties(self) -> Sequence[str]:
        return [
            f"elt = {self.elt}",
            f"generators = {self.generators}",
        ]


class ListComp(CompBase):
    def __init__(self, elt: AST, generators: MutableSequence[Comprehension]):
        super().__init__(elt, generators)

    def _name(self) -> str:
        return "ListComp"


class SetComp(CompBase):
    def __init__(self, elt: AST, generators: MutableSequence[Comprehension]):
        super().__init__(elt, generators)

    def _name(self) -> str:
        return "SetComp"


class GeneratorExp(CompBase):
    def __init__(self, elt: AST, generators: MutableSequence[Comprehension]):
        super().__init__(elt, generators)

    def _name(self) -> str:
        return "GeneratorExp"


class DictComp(AST):
    def __init__(self, key: AST, value: AST, generators: MutableSequence[Comprehension]):
        super().__init__()

        self.key: AST = key
        self.value: AST = value
        self.generators: MutableSequence[Comprehension] = generators

    def _name(self) -> str:
        return "DictComp"

    def _properties(self) -> Sequence[str]:
        return [
            f"key = {self.key}",
            f"value = {self.value}",
            f"generators = {self.generators}",
        ]


type Assignable = Union[Name, Tuple, List]


class Assign(AST):
    # a = b = 1
    # (a, b) = (2, 3)
    # e = (a, b) = [c, d] = (4, 5)
    def __init__(self, targets: MutableSequence[Assignable], value: AST):
        super().__init__()

        self.targets: MutableSequence[Assignable] = targets
        self.value: AST = value

    def _name(self) -> str:
        return "Assign"

    def _properties(self) -> Sequence[str]:
        return [
            f"targets = {self.targets}",
            f"value = {self.value}",
        ]


class AnnAssign(AST):
    # Annotated assignment
    def __init__(self, target: AST, annotation: AST, value: Optional[AST], simple: bool):
        super().__init__()

        self.target: AST = target
        self.annotation: AST = annotation
        self.value: Optional[AST] = value
        self.simple: bool = simple

    def _name(self) -> str:
        return "AnnAssign"

    def _properties(self) -> Sequence[str]:
        return [
            f"target = {self.target}",
            f"annotation = {self.annotation}",
            f"value = {self.value}",
            f"simple = {self.simple}",
        ]


class Operator(Enum):
    PLUS = "+"
    MINUS = "-"


class AugAssign(AST):
    def __init__(self, target: AST, op: Operator, value: AST):
        super().__init__()

        self.target: AST = target
        self.op: Operator = op
        self.value: AST = value

    def _name(self) -> str:
        return "AugAssign"

    def _properties(self) -> Sequence[str]:
        return [
            f"target = {self.target}",
            f"op = {self.op}",
            f"value = {self.value}",
        ]


class Raise(AST):
    # raise x
    # raise x from y
    def __init__(self, exc: Optional[AST], cause: Optional[AST]):
        super().__init__()

        self.exc: Optional[AST] = exc
        self.cause: Optional[AST] = cause

    def _name(self) -> str:
        return "Raise"

    def _properties(self) -> Sequence[str]:
        return [
            f"exc = {self.exc}",
            f"cause = {self.cause}",
        ]


class Assert(AST):
    # assert x, "message"
    def __init__(self, test: AST, msg: Optional[AST]):
        super().__init__()

        self.test: AST = test
        self.msg: Optional[AST] = msg

    def _name(self) -> str:
        return "Assert"

    def _properties(self) -> Sequence[str]:
        return [
            f"test = {self.test}",
            f"msg = {self.msg}",
        ]


class Delete(AST):
    # del x, y, z
    def __init__(self, targets: MutableSequence[AST]):
        super().__init__()

        self.targets: MutableSequence[AST] = targets

    def _name(self) -> str:
        return "Delete"

    def _properties(self) -> Sequence[str]:
        return [
            f"targets = {self.targets}",
        ]


class Pass(AST):
    # pass
    def __init__(self):
        super().__init__()

    def _name(self) -> str:
        return "Pass"


class TypeAlias(AST):
    def __init__(self, name: AST, type_params: MutableSequence[AST], value: AST):
        super().__init__()

        self.name: AST = name
        self.type_params: MutableSequence[AST] = type_params
        self.value: AST = value

    def _name(self) -> str:
        return "TypeAlias"

    def _properties(self) -> Sequence[str]:
        return [
            f"name = {self.name}",
            f"type_params = {self.type_params}",
            f"value = {self.value}",
        ]


class Alias(AST):
    def __init__(self, name: str, asname: Optional[str]):
        super().__init__()

        self.name: str = name
        self.asname: Optional[str] = asname

    def _name(self) -> str:
        return "AugAssign"

    def _properties(self) -> Sequence[str]:
        return [
            f"name = {self.name}",
            f"asname = {self.asname}",
        ]


class Import(AST):
    def __init__(self, names: MutableSequence[Alias]):
        super().__init__()

        self.names: MutableSequence[Alias] = names

    def _name(self) -> str:
        return "Import"

    def _properties(self) -> Sequence[str]:
        return [
            f"names = {self.names}",
        ]


class ImportFrom(AST):
    def __init__(self, module: Optional[str], names: MutableSequence[Alias], level: Optional[int]):
        super().__init__()

        self.module: Optional[str] = module
        self.names: MutableSequence[Alias] = names
        self.level: Optional[int] = level

    def _name(self) -> str:
        return "ImportFrom"

    def _properties(self) -> Sequence[str]:
        return [
            f"module = {self.module}",
            f"names = {self.names}",
            f"level = {self.level}",
        ]


class If(AST):
    def __init__(self, test: AST, body: AST, orelse: MutableSequence[AST]):
        super().__init__()

        self.test: AST = test
        self.body: AST = body
        self.orelse: MutableSequence[AST] = orelse

    def _name(self) -> str:
        return "If"

    def _properties(self) -> Sequence[str]:
        return [
            f"test = {self.test}",
            f"body = {self.body}",
            f"orelse = {self.orelse}",
        ]


class For(AST):
    def __init__(
        self, target: AST, iter: AST, body: MutableSequence[AST], orelse: MutableSequence[AST]
    ):
        super().__init__()

        self.target: AST = target
        self.iter: AST = iter
        self.body: MutableSequence[AST] = body
        self.orelse: MutableSequence[AST] = orelse

    def _name(self) -> str:
        return "For"

    def _properties(self) -> Sequence[str]:
        return [
            f"target = {self.target}",
            f"iter = {self.iter}",
            f"body = {self.body}",
            f"orelse = {self.orelse}",
        ]


class While(AST):
    def __init__(self, test: AST, body: AST, orelse: MutableSequence[AST]):
        super().__init__()

        self.test: AST = test
        self.body: AST = body
        self.orelse: MutableSequence[AST] = orelse

    def _name(self) -> str:
        return "While"

    def _properties(self) -> Sequence[str]:
        return [
            f"test = {self.test}",
            f"body = {self.body}",
            f"orelse = {self.orelse}",
        ]


class Break(AST):
    def __init__(self):
        super().__init__()

    def _name(self) -> str:
        return "Break"


class Continue(AST):
    def __init__(self):
        super().__init__()

    def _name(self) -> str:
        return "Continue"


class ExceptHandler(AST):
    """
    type: None if there's no exception type provided

    except type as name:
        body
    """

    def __init__(self, type: Optional[Name], name: Optional[str], body: MutableSequence[AST]):
        super().__init__()

        self.type: Optional[Name] = type
        self.name: Optional[str] = name
        self.body: MutableSequence[AST] = body

    def _name(self) -> str:
        return "ExceptHandler"

    def _properties(self) -> Sequence[str]:
        return [
            f"type = {self.type}",
            f"name = {self.name}",
            f"body = {self.body}",
        ]


class Try(AST):
    def __init__(
        self,
        body: MutableSequence[AST],
        handlers: MutableSequence[ExceptHandler],
        orelse: MutableSequence[AST],
        finalbody: MutableSequence[AST],
    ):
        super().__init__()

        self.body: MutableSequence[AST] = body
        self.handlers: MutableSequence[ExceptHandler] = handlers
        self.orelse: MutableSequence[AST] = orelse
        self.finalbody: MutableSequence[AST] = finalbody

    def _name(self) -> str:
        return "Try"

    def _properties(self) -> Sequence[str]:
        return [
            f"body = {self.body}",
            f"handlers = {self.handlers}",
            f"orelse = {self.orelse}",
            f"finalbody = {self.finalbody}",
        ]


class TryStar(Try):
    def __init__(
        self,
        body: MutableSequence[AST],
        handlers: MutableSequence[ExceptHandler],
        orelse: MutableSequence[AST],
        finalbody: MutableSequence[AST],
    ):
        super().__init__(body=body, handlers=handlers, orelse=orelse, finalbody=finalbody)

    def _name(self) -> str:
        return "TryStar"


class WithItem(AST):
    def __init__(
        self,
        context_expr: Union[AST, Name, Call],
        optional_vars: Optional[Union[Name, Tuple, List]],
    ):
        super().__init__()

        self.context_expr: Union[AST, Name, Call] = context_expr
        self.optional_vars: Optional[Union[Name, Tuple, List]] = optional_vars

    def _name(self) -> str:
        return "WithItem"

    def _properties(self) -> Sequence[str]:
        return [
            f"context_expr = {self.context_expr}",
            f"optional_vars = {self.optional_vars}",
        ]


class With(AST):
    def __init__(self, items: MutableSequence[WithItem], body: MutableSequence[AST]):
        super().__init__()

        self.items: MutableSequence[WithItem] = items
        self.body: MutableSequence[AST] = body

    def _name(self) -> str:
        return "With"

    def _properties(self) -> Sequence[str]:
        return [
            f"items = {self.items}",
            f"body = {self.body}",
        ]


class MatchCase(AST):
    def __init__(self, pattern: AST, guard: Optional[AST], body: MutableSequence[AST]):
        super().__init__()

        self.pattern: AST = pattern
        self.guard: Optional[AST] = guard
        self.body: MutableSequence[AST] = body

    def _name(self) -> str:
        return "MatchCase"

    def _properties(self) -> Sequence[str]:
        return [
            f"pattern = {self.pattern}",
            f"guard = {self.guard}",
            f"body = {self.body}",
        ]


class Match(AST):
    def __init__(self, subject: AST, cases: MutableSequence[MatchCase]):
        super().__init__()

        self.subject: AST = subject
        self.cases: MutableSequence[MatchCase] = cases

    def _name(self) -> str:
        return "Match"

    def _properties(self) -> Sequence[str]:
        return [
            f"subject = {self.subject}",
            f"cases = {self.cases}",
        ]


class MatchValue(AST):
    def __init__(self, value: Constant):
        super().__init__()

        self.value: Constant = value

    def _name(self) -> str:
        return "MatchValue"

    def _properties(self) -> Sequence[str]:
        return [
            f"value = {self.value}",
        ]


class MatchSingleton(AST):
    """
    match x:
        case None:
            pass

    value = None
    """

    def __init__(self, value: Constant):
        super().__init__()

        # according to the docs, this should only
        # be True, False, or None. Use MatchValue
        # otherwise.
        self.value: Constant = value

    def _name(self) -> str:
        return "MatchSingleton"

    def _properties(self) -> Sequence[str]:
        return [
            f"value = {self.value}",
        ]


class MatchSequence(AST):
    """
    match x:
        case [1, 2]:
            pass

    patterns = [1, 2]
    """

    def __init__(self, patterns: MutableSequence[MatchValue]):
        super().__init__()

        self.patterns: MutableSequence[MatchValue] = patterns

    def _name(self) -> str:
        return "MatchSequence"

    def _properties(self) -> Sequence[str]:
        return [
            f"patterns = {self.patterns}",
        ]


class MatchStar(AST):
    """
    match x:
        case [1, 2, *rest]:
            pass
        case [*_]:
            pass

    name = rest or None
    """

    def __init__(self, name: Optional[str]):
        super().__init__()

        self.name: Optional[str] = name

    def _name(self) -> str:
        return "MatchStar"

    def _properties(self) -> Sequence[str]:
        return [
            f"name = {self.name}",
        ]


class MatchMapping(AST):
    """
    match x:
        case {1: _, 2: _}:
            pass
        case {**rest}:
            pass

    keys = [1, 2], patterns = [MatchAs(), MatchAs()]
    rest = rest
    """

    def __init__(
        self, keys: MutableSequence[AST], patterns: MutableSequence[AST], rest: Optional[Name]
    ):
        super().__init__()

        self.keys: MutableSequence[AST] = keys
        self.patterns: MutableSequence[AST] = patterns
        self.rest: Optional[Name] = rest

    def _name(self) -> str:
        return "MatchMapping"

    def _properties(self) -> Sequence[str]:
        return [
            f"keys = {self.keys}",
            f"patterns = {self.patterns}",
            f"rest = {self.rest}",
        ]


class MatchClass(AST):
    """
    match x:
        case Point2D(0, 0)
            pass
        case Point3D(x=0, y=0, z=0)
            pass
    """

    def __init__(
        self,
        cls: AST,
        patterns: MutableSequence[AST],
        kwd_attrs: MutableSequence[str],
        kwd_patterns: MutableSequence[MatchValue],
    ):
        super().__init__()

        self.cls: AST = cls
        self.patterns: MutableSequence[AST] = patterns
        self.kwd_attrs: MutableSequence[str] = kwd_attrs
        self.kwd_patterns: MutableSequence[MatchValue] = kwd_patterns

    def _name(self) -> str:
        return "MatchClass"

    def _properties(self) -> Sequence[str]:
        return [
            f"cls = {self.cls}",
            f"patterns = {self.patterns}",
            f"kwd_attrs = {self.kwd_attrs}",
            f"kwd_patterns = {self.kwd_patterns}",
        ]


class MatchAs(AST):
    """
    match x:
        case [x] as y:
            pass
        case _
            pass

    pattern = MatchSequence(x), name = y
    pattern = None, name = None
    """

    def __init__(self, pattern: Optional[AST], name: Optional[str]):
        super().__init__()

        self.pattern: Optional[AST] = pattern
        self.name: Optional[str] = name

    def _name(self) -> str:
        return "MatchAs"

    def _properties(self) -> Sequence[str]:
        return [
            f"pattern = {self.pattern}",
            f"name = {self.name}",
        ]


class MatchOr(AST):
    """
    match x:
        case [x] | (y):
            pass
    """

    def __init__(self, patterns: MutableSequence[AST]):
        super().__init__()

        self.patterns: MutableSequence[AST] = patterns

    def _name(self) -> str:
        return "MatchOr"

    def _properties(self) -> Sequence[str]:
        return [
            f"patterns = {self.patterns}",
        ]


class TypeVar(AST):
    """
    type Alias[T: int = bool] = List[T]

    name = Alias, bound = int, default_value = bool

    bound = T must be a subtype
    default_value = T is this if not specified
    """

    def __init__(
        self, name: str, bound: Optional[Union[Tuple, Name]], default_value: Optional[Name]
    ):
        super().__init__()

        self.name: str = name
        self.bound: Optional[Union[Tuple, Name]] = bound
        self.default_value: Optional[Name] = default_value

    def _name(self) -> str:
        return "TypeVar"

    def _properties(self) -> Sequence[str]:
        return [
            f"name = {self.name}",
            f"bound = {self.bound}",
            f"default_value = {self.default_value}",
        ]


class ParamSpec(AST):
    """
    type Alias[**P = [int, str]] = Callable[P, int]
    """

    def __init__(self, name: str, default_value: Optional[AST]):
        super().__init__()

        self.name: str = name
        self.default_value: Optional[AST] = default_value

    def _name(self) -> str:
        return "ParamSpec"

    def _properties(self) -> Sequence[str]:
        return [
            f"name = {self.name}",
            f"default_value = {self.default_value}",
        ]


class TypeVarTuple(AST):
    """
    type Alias[*Ts = ()] = tuple[*Ts]
    """

    def __init__(self, name: str, default_value: Optional[AST]):
        super().__init__()

        self.name: str = name
        self.default_value: Optional[AST] = default_value

    def _name(self) -> str:
        return "TypeVarTuple"

    def _properties(self) -> Sequence[str]:
        return [
            f"name = {self.name}",
            f"default_value = {self.default_value}",
        ]


class Arguments(AST):
    def __init__(self, posonlyargs, args, vararg, kwonlyargs, kw_defaults, kwarg, defaults):
        super().__init__()


class Arg(AST):
    def __init__(self, arg, annotation, type_comment):
        super().__init__()


class FunctionDef(AST):
    def __init__(
        self,
        name: str,
        args: Arguments,
        body: MutableSequence[AST],
        decorator_list: MutableSequence[AST],
        returns: AST,
        type_params: MutableSequence[AST],
    ):
        super().__init__()

        self.name: str = name
        self.args: Arguments = args
        self.body: MutableSequence[AST] = body
        self.decorator_list: MutableSequence[AST] = decorator_list
        self.returns: AST = returns
        self.type_params: MutableSequence[AST] = type_params


class Lambda(AST):
    def __init__(self, args: Arguments, body: MutableSequence[AST]):
        super().__init__()

        self.args: Arguments = args
        self.body: MutableSequence[AST] = body


class Return(AST):
    def __init__(self, value: Optional[AST]):
        super().__init__()

        self.value: Optional[AST] = value


class Yield(AST):
    def __init__(self, value: AST):
        super().__init__()

        self.value: AST = value


class YieldFrom(AST):
    def __init__(self, value: AST):
        super().__init__()

        self.value: AST = value


class Global(AST):
    def __init__(self, names: MutableSequence[str]):
        super().__init__()

        self.names: MutableSequence[str] = names


class Nonlocal(AST):
    def __init__(self, names: MutableSequence[str]):
        super().__init__()

        self.names: MutableSequence[str] = names


class ClassDef(AST):
    def __init__(
        self,
        name: str,
        bases: MutableSequence[Name],
        keywords: MutableSequence[Keyword],
        body: MutableSequence[AST],
        decorator_list: MutableSequence[AST],
        type_params: MutableSequence[AST],
    ):
        super().__init__()

        self.name: str = name
        self.bases: MutableSequence[Name] = bases
        self.keywords: MutableSequence[Keyword] = keywords
        self.body: MutableSequence[AST] = body
        self.decorator_list: MutableSequence[AST] = decorator_list
        self.type_params: MutableSequence[AST] = type_params


class AsyncFunctionDef(AST):
    def __init__(
        self,
        name: str,
        args: Arguments,
        body: MutableSequence[AST],
        decorator_list: MutableSequence[AST],
        returns: AST,
        type_params: MutableSequence[AST],
    ):
        super().__init__()

        self.name: str = name
        self.args: Arguments = args
        self.body: MutableSequence[AST] = body
        self.decorator_list: MutableSequence[AST] = decorator_list
        self.returns: AST = returns
        self.type_params: MutableSequence[AST] = type_params


class Await(AST):
    def __init__(self, value: AST):
        super().__init__()

        self.value: AST = value


class AsyncFor(AST):
    def __init__(
        self, target: AST, iter: AST, body: MutableSequence[AST], orelse: MutableSequence[AST]
    ):
        super().__init__()

        self.target: AST = target
        self.iter: AST = iter
        self.body: MutableSequence[AST] = body
        self.orelse: MutableSequence[AST] = orelse


class AsyncWith(AST):
    def __init__(self, items: MutableSequence[WithItem], body: MutableSequence[AST]):
        super().__init__()

        self.items: MutableSequence[WithItem] = items
        self.body: MutableSequence[AST] = body
