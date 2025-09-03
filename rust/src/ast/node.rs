use super::types::{GenericType, OptionalType, QualifiedType, Type, TypeName, UnionType};
use crate::utils::position::SourceLocation;

/// Source location information for AST nodes
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct NodeSource {
    /// The starting line of the node (inclusive), 1-indexed.
    pub line_start: usize,

    /// The starting column of the node (inclusive), 1-indexed, based on
    /// Unicode scalar value offsets.
    pub col_start: usize,

    /// The ending line of the node (inclusive), 1-indexed.
    pub line_end: usize,

    /// The ending column of the node (exclusive), 1-indexed, based on
    /// Unicode scalar value offsets.
    pub col_end: usize,
}

impl NodeSource {
    #[must_use]
    pub const fn new(line_start: usize, col_start: usize, line_end: usize, col_end: usize) -> Self {
        Self {
            line_start,
            col_start,
            line_end,
            col_end,
        }
    }

    #[must_use]
    pub const fn from_source_location(start: &SourceLocation, end: &SourceLocation) -> Self {
        Self {
            line_start: start.line,
            col_start: start.column,
            line_end: end.line,
            col_end: end.column,
        }
    }
}

/// Unary operators
#[derive(Debug, Clone, PartialEq, Eq)]
pub enum UnaryOp {
    Not,      // not x
    UnaryAdd, // +x
    UnarySub, // -x
    Invert,   // ~x
}

/// Binary operators
#[derive(Debug, Clone, PartialEq, Eq)]
pub enum BinaryOp {
    Add,        // x + y
    Sub,        // x - y
    Mult,       // x * y
    MatMult,    // x @ y
    Div,        // x / y
    FloorDiv,   // x // y
    Mod,        // x % y
    Pow,        // x ** y
    LShift,     // x << y
    RShift,     // x >> y
    BitwiseOr,  // x | y
    BitwiseXor, // x ^ y
    BitwiseAnd, // x & y
}

/// Comparison operators
#[derive(Debug, Clone, PartialEq, Eq)]
pub enum CompOp {
    Eq,    // x == y
    NotEq, // x != y
    Lt,    // x < y
    LtE,   // x <= y
    Gt,    // x > y
    GtE,   // x >= y
    Is,    // x is y
    IsNot, // x is not y
    In,    // x in y
    NotIn, // x not in y
}

/// Boolean operators
#[derive(Debug, Clone, PartialEq, Eq)]
pub enum BoolOp {
    And, // x and y
    Or,  // x or y
}

/// Constant literals.
#[derive(Debug, Clone, PartialEq)]
pub enum ConstantValue {
    None,
    Bool(bool),
    Int(i64),
    Float(f64),
    Complex { real: f64, imag: f64 },
    Str(String),
    Bytes(Vec<u8>),
    Ellipsis,
}

#[derive(Debug, Clone, PartialEq)]
pub enum Node {
    // Root
    Module(Module),

    // Statements
    Assign(Assign),
    AugAssign(AugAssign),
    Assert(Assert),
    Pass(Pass),
    Delete(Delete),
    Return(Return),
    Raise(Raise),
    Break(Break),
    Continue(Continue),

    // Expressions
    BoolOp(BoolOp_),
    NamedExpr(NamedExpr),
    BinaryOp(BinaryOp_),
    UnaryOp(UnaryOp_),
    Lambda(Lambda),
    IfExp(IfExp),
    Dict(Dict),
    Set(Set),
    ListComp(ListComp),
    SetComp(SetComp),
    DictComp(DictComp),
    GeneratorExp(GeneratorExp),
    Await(Await),
    Yield(Yield),
    YieldFrom(YieldFrom),
    Compare(Compare),
    Call(Call),
    FormattedValue(FormattedValue),
    JoinedStr(JoinedStr),
    Constant(Constant),
    Attribute(Attribute),
    Subscript(Subscript),
    Starred(Starred),
    Name(Name),
    List(List),
    Tuple(Tuple),
    Slice(Slice),
    TypedName(TypedName),

    // Control flow
    If(If),
    For(For),
    AsyncFor(AsyncFor),
    While(While),
    With(With),
    AsyncWith(AsyncWith),
    Try(Try),
    TryStar(TryStar),
    Match(Match),

    // Definitions
    FunctionDef(FunctionDef),
    AsyncFunctionDef(AsyncFunctionDef),
    MemberDef(MemberDef),
    PropertyDef(PropertyDef),
    EventDef(EventDef),
    ClassDef(ClassDef),
    StructDef(StructDef),
    ProtocolDef(ProtocolDef),

    // Others
    Import(Import),
    ImportFrom(ImportFrom),
    ExceptHandler(ExceptHandler),
    MatchCase(MatchCase),

    // Type expressions (syntactic)
    TypeName(TypeName),
    QualifiedType(QualifiedType),
    GenericType(GenericType),
    OptionalType(OptionalType),
    UnionType(UnionType),

    // Legacy type (for semantic analysis)
    Type(Type),
    TypeAlias(TypeAlias),
}

impl Node {
    #[allow(dead_code)]
    pub const fn source(&self) -> Option<&NodeSource> {
        match self {
            Self::Assign(n) => n.source.as_ref(),
            Self::BoolOp(n) => n.source.as_ref(),
            Self::Constant(n) => n.source.as_ref(),
            Self::IfExp(n) => n.source.as_ref(),
            Self::List(n) => n.source.as_ref(),
            Self::Module(n) => n.source.as_ref(),
            Self::Name(n) => n.source.as_ref(),
            Self::NamedExpr(n) => n.source.as_ref(),
            Self::Tuple(n) => n.source.as_ref(),
            Self::TypedName(n) => n.source.as_ref(),
            // Type expressions
            Self::TypeName(n) => n.source.as_ref(),
            Self::QualifiedType(n) => n.source.as_ref(),
            Self::GenericType(n) => n.source.as_ref(),
            Self::OptionalType(n) => n.source.as_ref(),
            Self::UnionType(n) => n.source.as_ref(),
            // Add all other variants...
            _ => None, // Temporary fallback
        }
    }

    #[allow(dead_code)]
    const fn source_mut(&mut self) -> Option<&mut NodeSource> {
        match self {
            Self::Assign(n) => n.source.as_mut(),
            Self::BoolOp(n) => n.source.as_mut(),
            Self::Constant(n) => n.source.as_mut(),
            Self::IfExp(n) => n.source.as_mut(),
            Self::List(n) => n.source.as_mut(),
            Self::Module(n) => n.source.as_mut(),
            Self::Name(n) => n.source.as_mut(),
            Self::NamedExpr(n) => n.source.as_mut(),
            Self::Tuple(n) => n.source.as_mut(),
            Self::TypedName(n) => n.source.as_mut(),
            // Type expressions
            Self::TypeName(n) => n.source.as_mut(),
            Self::QualifiedType(n) => n.source.as_mut(),
            Self::GenericType(n) => n.source.as_mut(),
            Self::OptionalType(n) => n.source.as_mut(),
            Self::UnionType(n) => n.source.as_mut(),
            // Add all other variants...
            _ => None, // Temporary fallback
        }
    }

    #[allow(dead_code)]
    const fn set_source(&mut self, source: NodeSource) {
        match self {
            Self::Assign(n) => n.source = Some(source),
            Self::BoolOp(n) => n.source = Some(source),
            Self::Constant(n) => n.source = Some(source),
            Self::IfExp(n) => n.source = Some(source),
            Self::List(n) => n.source = Some(source),
            Self::Module(n) => n.source = Some(source),
            Self::Name(n) => n.source = Some(source),
            Self::NamedExpr(n) => n.source = Some(source),
            Self::Tuple(n) => n.source = Some(source),
            Self::TypedName(n) => n.source = Some(source),
            // Type expressions
            Self::TypeName(n) => n.source = Some(source),
            Self::QualifiedType(n) => n.source = Some(source),
            Self::GenericType(n) => n.source = Some(source),
            Self::OptionalType(n) => n.source = Some(source),
            Self::UnionType(n) => n.source = Some(source),
            // Add all other variants...
            _ => {} // Temporary fallback
        }
    }
}

// Node variants

/// An alias used in imports, e.g. `import x as y`.
/// TODO: Is this correct?
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct Alias {
    /// The name of the alias.
    pub name: String,

    /// The value of the alias.
    pub as_name: Option<String>,

    pub source: Option<NodeSource>,
}

/// An argument in a function or lambda definition. An argument has a name,
/// typically has a type, and may have a default value.
#[derive(Debug, Clone, PartialEq)]
pub struct Arg {
    pub name: String,

    /// `self` arguments and arguments in lambdas where static type inference
    /// can occur can be untyped. Others must be typed.
    pub type_: Option<Box<Node>>,

    /// The default value, if any. Invalid for `self` arguments.
    pub default: Option<Box<Node>>,
    pub source: Option<NodeSource>,
}

/// Arguments in a function or lambda definition. Supports an optional leading
/// vararg, and any number of arguments.
#[derive(Debug, Clone, PartialEq)]
pub struct Arguments {
    pub vararg: Option<Arg>,
    pub args: Vec<Arg>,
}

/// Assert statement, e.g. `assert x == 5, "Should be 5"`.
#[derive(Debug, Clone, PartialEq)]
pub struct Assert {
    /// A test expression whose boolean value is expected to be true. This
    /// can also be an expression whose type implements `__bool__()`.
    pub test: Box<Node>,

    /// The message to emit if the assertion fails. Typically a string literal
    /// but can be any expression whose type implements `__str__()`.
    pub msg: Option<Box<Node>>,
    pub source: Option<NodeSource>,
}

/// An assignment expression, e.g. `x = 5`.
#[derive(Debug, Clone, PartialEq)]
pub struct Assign {
    /// For destructuring assignments, the target is a Tuple node of
    /// Name/TypedName nodes.
    pub target: Box<Node>,

    /// For destructuring assignments, the value is a Tuple node.
    pub value: Box<Node>,
    pub source: Option<NodeSource>,
}

/// An async for loop, e.g. `async for x in y:`. Allows an optional else clause
/// if the loop does not exit via `break`.
#[derive(Debug, Clone, PartialEq)]
pub struct AsyncFor {
    /// The target of the for loop, typically a variable. It can also be a
    /// destructuring pattern implemented as a Tuple node.
    pub target: Box<Node>,

    /// The iterable being looped over.
    pub iter: Box<Node>,

    /// The body of the for loop.
    pub body: Vec<Node>,

    /// The optional else clause.
    pub else_: Vec<Node>,

    pub source: Option<NodeSource>,
}

/// An async function definition, e.g. `async def foo():`
#[derive(Debug, Clone, PartialEq)]
pub struct AsyncFunctionDef {
    pub name: String,
    pub args: Arguments,
    pub decorators: Vec<Node>,
    /// The return type of the function. If omitted, then it must be inferrable
    /// from the body.
    pub return_type: Option<Box<Node>>,
    pub body: Vec<Node>,
    pub source: Option<NodeSource>,
}

/// An async with statement, e.g. `async for x in y:`
/// TODO
#[derive(Debug, Clone, PartialEq)]
pub struct AsyncWith {
    pub items: Vec<WithItem>,
    pub body: Vec<Node>,
    pub source: Option<NodeSource>,
}

/// TODO: What is this, is this a decorator?
#[derive(Debug, Clone, PartialEq)]
pub struct Attribute {
    pub value: Box<Node>,
    pub attr: String,
    pub source: Option<NodeSource>,
}

/// An augmented assignment expression, e.g. `x += 5`.
#[derive(Debug, Clone, PartialEq)]
pub struct AugAssign {
    /// Cannot be a destructuring pattern.
    pub target: Box<Node>,
    pub op: BinaryOp,
    pub value: Box<Node>,
    pub source: Option<NodeSource>,
}

/// An await expression, e.g. `await x`.
#[derive(Debug, Clone, PartialEq)]
pub struct Await {
    pub future: Box<Node>,
    pub source: Option<NodeSource>,
}

/// A binary operation, e.g. `x + y`.
#[derive(Debug, Clone, PartialEq)]
pub struct BinaryOp_ {
    pub left: Box<Node>,
    pub op: BinaryOp,
    pub right: Box<Node>,
    pub source: Option<NodeSource>,
}

/// Boolean operations like `or` and `and`. Values must be at least two, and
/// the same operator evaluation is applied to every value.
#[derive(Debug, Clone, PartialEq)]
pub struct BoolOp_ {
    pub op: BoolOp,
    pub values: Vec<Node>,
    pub source: Option<NodeSource>,
}

/// Break statement, e.g. `break`.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct Break {
    pub source: Option<NodeSource>,
}

/// A function call expression, e.g. `foo(bar)`.
#[derive(Debug, Clone, PartialEq)]
pub struct Call {
    /// Typically a function name, but it can be an expression that returns a
    /// function that is immediately invoked.
    pub function: Box<Node>,

    /// These are the positional arguments passed to the function.
    pub positional_args: Vec<Node>,

    /// These are the keyword arguments passed to the function.
    pub keyword_args: Vec<Node>,
    pub source: Option<NodeSource>,
}

/// A class definition, e.g. `class Foo:`
#[derive(Debug, Clone, PartialEq)]
pub struct ClassDef {
    pub name: String,
    pub bases: Vec<Node>,
    pub body: Vec<Node>,
    pub source: Option<NodeSource>,
}

/// A comparison operation, e.g. `x < y`. The operators and comparators can
/// be more than one, e.g. `x < y <= z`, which is equivalent to
/// `x < y and y <= z`.
#[derive(Debug, Clone, PartialEq)]
pub struct Compare {
    pub left: Box<Node>,
    pub ops: Vec<CompOp>,
    pub comparators: Vec<Node>,
    pub source: Option<NodeSource>,
}

#[derive(Debug, Clone, PartialEq)]
pub struct Comprehension {
    pub target: Box<Node>,
    pub iter: Box<Node>,
    pub ifs: Vec<Node>,
    pub is_async: bool,
    pub source: Option<NodeSource>,
}

/// A constant value, e.g. 3, False, or "hello". This excludes more complex
/// literals like list literals.
#[derive(Debug, Clone, PartialEq)]
pub struct Constant {
    pub value: ConstantValue,
    pub source: Option<NodeSource>,
}

/// Continue statement, e.g. `continue`.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct Continue {
    pub source: Option<NodeSource>,
}

/// Delete statement for deleting dictionary keys, e.g. `del x["foobar"]`.
#[derive(Debug, Clone, PartialEq)]
pub struct Delete {
    pub targets: Vec<Node>,
    pub source: Option<NodeSource>,
}

#[derive(Debug, Clone, PartialEq)]
pub struct Dict {
    pub keys: Vec<Option<Node>>, // None for **dict expansion
    pub values: Vec<Node>,
    pub source: Option<NodeSource>,
}

#[derive(Debug, Clone, PartialEq)]
pub struct DictComp {
    pub key: Box<Node>,
    pub value: Box<Node>,
    pub generators: Vec<Comprehension>,
    pub source: Option<NodeSource>,
}

#[derive(Debug, Clone, PartialEq)]
pub struct EventDef {
    pub name: String,
    pub type_: Option<Box<Node>>,
    pub default: Option<Box<Node>>,
    pub source: Option<NodeSource>,
}

#[derive(Debug, Clone, PartialEq)]
pub struct ExceptHandler {
    pub type_: Option<Box<Node>>,
    pub name: Option<String>,
    pub body: Vec<Node>,
    pub source: Option<NodeSource>,
}

/// An for loop, e.g. `for x in y:`. Allows an optional else clause if the
/// loop does not exit via `break`.
#[derive(Debug, Clone, PartialEq)]
pub struct For {
    /// The target of the for loop, typically a variable. It can also be a
    /// destructuring pattern implemented as a Tuple node.
    pub target: Box<Node>,

    /// The iterable being looped over.
    pub iter: Box<Node>,

    /// The body of the for loop.
    pub body: Vec<Node>,

    /// The optional else clause.
    pub else_: Vec<Node>,
    pub source: Option<NodeSource>,
}

/// A function definition, e.g. `def foo():`
#[derive(Debug, Clone, PartialEq)]
pub struct FunctionDef {
    pub name: String,
    pub args: Arguments,
    pub decorators: Vec<Node>,
    /// The return type of the function. If omitted, then it must be inferrable
    /// from the body.
    pub return_type: Option<Box<Node>>,
    pub body: Vec<Node>,
    pub source: Option<NodeSource>,
}

#[derive(Debug, Clone, PartialEq)]
pub struct GeneratorExp {
    pub element: Box<Node>,
    pub generators: Vec<Comprehension>,
    pub source: Option<NodeSource>,
}

/// An if statement, e.g. `if x:` with an optional else clause implemented
/// as a vector of statements (which may be empty).
#[derive(Debug, Clone, PartialEq)]
pub struct If {
    pub test: Box<Node>,
    pub body: Vec<Node>,

    /// Else statements, if any. Can be empty.
    pub else_: Vec<Node>,
    pub source: Option<NodeSource>,
}

/// An if expression, e.g. `x if y else z` where y is the test, x is the
/// body and z is the else clause.
#[derive(Debug, Clone, PartialEq)]
pub struct IfExp {
    pub test: Box<Node>,
    pub body: Box<Node>,
    pub else_: Box<Node>,
    pub source: Option<NodeSource>,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct Import {
    pub names: Vec<Alias>,
    pub source: Option<NodeSource>,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct ImportFrom {
    pub module: Option<String>,
    pub names: Vec<Alias>,
    pub level: usize,
    pub source: Option<NodeSource>,
}

#[derive(Debug, Clone, PartialEq)]
pub struct FormattedValue {
    pub value: Box<Node>,
    pub conversion: Option<String>,
    pub format_spec: Option<Box<Node>>,
    pub source: Option<NodeSource>,
}

#[derive(Debug, Clone, PartialEq)]
pub struct JoinedStr {
    pub values: Vec<Node>,
    pub source: Option<NodeSource>,
}

#[derive(Debug, Clone, PartialEq)]
pub struct Keyword {
    pub arg: Option<String>, // None for **kwargs
    pub value: Box<Node>,
    pub source: Option<NodeSource>,
}

/// A lambda expression, e.g. `lambda x: x + 1`.
///
/// The return type is optional, being inferred from the body. Argument types
/// are also inferred from context when possible.
///
/// TODO: What does the syntax for typed arguments look like?
#[derive(Debug, Clone, PartialEq)]
pub struct Lambda {
    pub args: Arguments,
    pub return_type: Option<Box<Node>>,
    pub body: Box<Node>,
    pub source: Option<NodeSource>,
}

/// A list literal.
#[derive(Debug, Clone, PartialEq)]
pub struct List {
    pub elements: Vec<Node>,
    pub source: Option<NodeSource>,
}

#[derive(Debug, Clone, PartialEq)]
pub struct ListComp {
    pub element: Box<Node>,
    pub generators: Vec<Comprehension>,
    pub source: Option<NodeSource>,
}

#[derive(Debug, Clone, PartialEq)]
pub struct Match {
    pub subject: Box<Node>,
    pub cases: Vec<MatchCase>,
    pub source: Option<NodeSource>,
}

#[derive(Debug, Clone, PartialEq)]
pub struct MatchCase {
    pub pattern: Box<Node>,
    pub guard: Option<Box<Node>>,
    pub body: Vec<Node>,
    pub source: Option<NodeSource>,
}

#[derive(Debug, Clone, PartialEq)]
pub struct MemberDef {
    pub name: String,
    pub type_: Option<Box<Node>>,
    pub default: Option<Box<Node>>,
    pub source: Option<NodeSource>,
}

/// A module, which is a collection of statements.
#[derive(Debug, Clone, PartialEq)]
pub struct Module {
    pub body: Vec<Node>,
    pub source: Option<NodeSource>,
}

/// A simple name, e.g. `x`. See also `TypedName` which has a type declaration.
///
/// This is used in cases where the variable is declared but its type should be
/// inferred from its context (e.g. the value it is being assigned to) or if
/// the variable is being used (loaded), or if the variable was already
/// declared and simply being reassigned to another value compatible with its
/// existing type.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct Name {
    pub id: String,
    pub source: Option<NodeSource>,
}

#[derive(Debug, Clone, PartialEq)]
pub struct NamedExpr {
    pub target: Box<Node>,
    pub value: Box<Node>,
    pub source: Option<NodeSource>,
}

/// Pass statement, e.g. pass.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct Pass {
    pub source: Option<NodeSource>,
}

#[derive(Debug, Clone, PartialEq)]
pub struct PropertyDef {
    pub name: String,
    pub type_: Box<Node>,
    pub default: Box<Node>,
    pub getter: Option<Box<Node>>,
    pub setter: Option<Box<Node>>,
    pub source: Option<NodeSource>,
}

#[derive(Debug, Clone, PartialEq)]
pub struct ProtocolDef {
    pub name: String,
    pub bases: Vec<Node>,
    pub body: Vec<Node>,
    pub source: Option<NodeSource>,
}

#[derive(Debug, Clone, PartialEq)]
pub struct Raise {
    pub exc: Option<Box<Node>>,
    pub cause: Option<Box<Node>>,
    pub source: Option<NodeSource>,
}

/// Return statement, e.g. `return x`.
#[derive(Debug, Clone, PartialEq)]
pub struct Return {
    /// The value being returned. Multiple values being returned must be stored
    /// as a tuple.
    pub value: Option<Box<Node>>,
    pub source: Option<NodeSource>,
}

/// A set literal, e.g. `{1, 2, 3}`.
#[derive(Debug, Clone, PartialEq)]
pub struct Set {
    pub elements: Vec<Node>,
    pub source: Option<NodeSource>,
}

#[derive(Debug, Clone, PartialEq)]
pub struct SetComp {
    pub element: Box<Node>,
    pub generators: Vec<Comprehension>,
    pub source: Option<NodeSource>,
}

#[derive(Debug, Clone, PartialEq)]
pub struct Slice {
    pub lower: Option<Box<Node>>,
    pub upper: Option<Box<Node>>,
    pub step: Option<Box<Node>>,
    pub source: Option<NodeSource>,
}

#[derive(Debug, Clone, PartialEq)]
pub struct Starred {
    pub value: Box<Node>,
    pub source: Option<NodeSource>,
}

#[derive(Debug, Clone, PartialEq)]
pub struct StructDef {
    pub name: String,
    pub bases: Vec<Node>,
    pub body: Vec<Node>,
    pub source: Option<NodeSource>,
}

#[derive(Debug, Clone, PartialEq)]
pub struct Subscript {
    pub value: Box<Node>,
    pub slice: Box<Node>,
    pub source: Option<NodeSource>,
}

#[derive(Debug, Clone, PartialEq)]
pub struct Try {
    pub body: Vec<Node>,
    pub handlers: Vec<ExceptHandler>,
    pub else_: Vec<Node>,
    pub finalbody: Vec<Node>,
    pub source: Option<NodeSource>,
}

#[derive(Debug, Clone, PartialEq)]
pub struct TryStar {
    pub body: Vec<Node>,
    pub handlers: Vec<ExceptHandler>,
    pub else_: Vec<Node>,
    pub finalbody: Vec<Node>,
    pub source: Option<NodeSource>,
}

/// A tuple literal, e.g. `(1, "hello", False)`. It can also be empty, e.g.
/// `(,)`, or a tuple with a single element `(1,)`.
#[derive(Debug, Clone, PartialEq)]
pub struct Tuple {
    pub elements: Vec<Node>,
    pub source: Option<NodeSource>,
}

#[derive(Debug, Clone, PartialEq)]
pub struct TypeAlias {
    pub name: Box<Node>,
    pub type_params: Vec<Node>,
    pub value: Box<Node>,
    pub source: Option<NodeSource>,
}

/// A name with an associated type, e.g. `x: int`. See `Name` for the untyped
/// version.
///
/// This should be used when first declaring a variable and its type
/// should not be inferred from its context. It can also be used to create a
/// variable local to the scope, shadowing an existing one, possibly with
/// a different type.
#[derive(Debug, Clone, PartialEq)]
pub struct TypedName {
    pub id: String,
    pub type_: Box<Node>,
    pub source: Option<NodeSource>,
}

/// A unary operation, e.g. `+x`, `-x`, or `not x`.
#[derive(Debug, Clone, PartialEq)]
pub struct UnaryOp_ {
    pub op: UnaryOp,
    pub operand: Box<Node>,
    pub source: Option<NodeSource>,
}

/// A while statement, e.g. `while x: y`. Allows an optional else clause
/// if the body does not exit via `break`.
#[derive(Debug, Clone, PartialEq)]
pub struct While {
    pub test: Box<Node>,
    pub body: Vec<Node>,

    /// The else clause implemented as a vector of statements. Can be empty.
    pub else_: Vec<Node>,
    pub source: Option<NodeSource>,
}

#[derive(Debug, Clone, PartialEq)]
pub struct With {
    pub items: Vec<WithItem>,
    pub body: Vec<Node>,
    pub source: Option<NodeSource>,
}

#[derive(Debug, Clone, PartialEq)]
pub struct WithItem {
    pub context_expr: Box<Node>,
    pub optional_vars: Option<Box<Node>>,
    pub source: Option<NodeSource>,
}

/// A yield expression, e.g. `yield x`.
#[derive(Debug, Clone, PartialEq)]
pub struct Yield {
    pub value: Option<Box<Node>>,
    pub source: Option<NodeSource>,
}

/// A yield from expression, e.g. `yield from x`.
#[derive(Debug, Clone, PartialEq)]
pub struct YieldFrom {
    pub generator: Box<Node>,
    pub source: Option<NodeSource>,
}
