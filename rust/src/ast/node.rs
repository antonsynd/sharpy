use super::types::Type;
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

/// Main AST node enum - this is the key difference from Python's class hierarchy
/// In Rust, we use enums with variants instead of inheritance
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
    BinOp(BinOp),
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
    Type(Type),
    TypeAlias(TypeAlias),
}

impl Node {
    const fn source(&self) -> Option<&NodeSource> {
        match self {
            Self::Module(n) => n.source.as_ref(),
            Self::Assign(n) => n.source.as_ref(),
            Self::Constant(n) => n.source.as_ref(),
            Self::Name(n) => n.source.as_ref(),
            // Add all other variants...
            _ => None, // Temporary fallback
        }
    }

    const fn source_mut(&mut self) -> Option<&mut NodeSource> {
        match self {
            Self::Module(n) => n.source.as_mut(),
            Self::Assign(n) => n.source.as_mut(),
            Self::Constant(n) => n.source.as_mut(),
            Self::Name(n) => n.source.as_mut(),
            // Add all other variants...
            _ => None, // Temporary fallback
        }
    }

    const fn set_source(&mut self, source: NodeSource) {
        match self {
            Self::Module(n) => n.source = Some(source),
            Self::Assign(n) => n.source = Some(source),
            Self::Constant(n) => n.source = Some(source),
            Self::Name(n) => n.source = Some(source),
            // Add all other variants...
            _ => {} // Temporary fallback
        }
    }
}

// Node variants

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct Alias {
    pub name: String,
    pub asname: Option<String>,
    pub source: Option<NodeSource>,
}

#[derive(Debug, Clone, PartialEq)]
pub struct Arg {
    pub arg: String,
    pub annotation: Option<Box<Node>>,
    pub type_comment: Option<String>,
    pub source: Option<NodeSource>,
}

// Helper structs
#[derive(Debug, Clone, PartialEq)]
pub struct Arguments {
    pub posonlyargs: Vec<Arg>,
    pub args: Vec<Arg>,
    pub vararg: Option<Arg>,
    pub kwonlyargs: Vec<Arg>,
    pub kw_defaults: Vec<Option<Node>>,
    pub kwarg: Option<Arg>,
    pub defaults: Vec<Node>,
}

/// Assert statement, e.g. `assert x == 5, "Should be 5"`.
#[derive(Debug, Clone, PartialEq)]
pub struct Assert {
    pub test: Box<Node>,
    pub msg: Option<Box<Node>>,
    pub source: Option<NodeSource>,
}

/// An assignment expression, e.g. `x = 5`. Also accepts destructuring assignment
/// for lists and tuples.
#[derive(Debug, Clone, PartialEq)]
pub struct Assign {
    pub targets: Vec<Node>,
    pub value: Box<Node>,
    pub source: Option<NodeSource>,
}

#[derive(Debug, Clone, PartialEq)]
pub struct AsyncFor {
    pub target: Box<Node>,
    pub iter: Box<Node>,
    pub body: Vec<Node>,
    pub orelse: Vec<Node>,
    pub source: Option<NodeSource>,
}

#[derive(Debug, Clone, PartialEq)]
pub struct AsyncFunctionDef {
    pub name: String,
    pub args: Arguments,
    pub body: Vec<Node>,
    pub decorator_list: Vec<Node>,
    pub returns: Option<Box<Node>>,
    pub type_comment: Option<String>,
    pub source: Option<NodeSource>,
}

#[derive(Debug, Clone, PartialEq)]
pub struct AsyncWith {
    pub items: Vec<WithItem>,
    pub body: Vec<Node>,
    pub source: Option<NodeSource>,
}

#[derive(Debug, Clone, PartialEq)]
pub struct Attribute {
    pub value: Box<Node>,
    pub attr: String,
    pub source: Option<NodeSource>,
}

/// An augmented assignment expression, e.g. `x += 5`.
#[derive(Debug, Clone, PartialEq)]
pub struct AugAssign {
    pub target: Box<Node>,
    pub op: BinaryOp,
    pub value: Box<Node>,
    pub source: Option<NodeSource>,
}

#[derive(Debug, Clone, PartialEq)]
pub struct Await {
    pub value: Box<Node>,
    pub source: Option<NodeSource>,
}

/// A binary operation, e.g. `x + y`.
#[derive(Debug, Clone, PartialEq)]
pub struct BinOp {
    pub left: Box<Node>,
    pub op: BinaryOp,
    pub right: Box<Node>,
    pub source: Option<NodeSource>,
}

/// Boolean operations like `or` and `and`.
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

#[derive(Debug, Clone, PartialEq)]
pub struct Call {
    pub func: Box<Node>,
    pub args: Vec<Node>,
    pub keywords: Vec<Keyword>,
    pub source: Option<NodeSource>,
}

#[derive(Debug, Clone, PartialEq)]
pub struct ClassDef {
    pub name: String,
    pub base: Option<Box<Node>>,
    pub protocols: Vec<Node>,
    pub members: Vec<Node>,
    pub properties: Vec<Node>,
    pub events: Vec<Node>,
    pub functions: Vec<Node>,
    pub source: Option<NodeSource>,
}

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
    pub type_: Box<Node>,
    pub default: Box<Node>,
}

#[derive(Debug, Clone, PartialEq)]
pub struct ExceptHandler {
    pub type_: Option<Box<Node>>,
    pub name: Option<String>,
    pub body: Vec<Node>,
    pub source: Option<NodeSource>,
}

#[derive(Debug, Clone, PartialEq)]
pub struct For {
    pub target: Box<Node>,
    pub iter: Box<Node>,
    pub body: Vec<Node>,
    pub orelse: Vec<Node>,
    pub source: Option<NodeSource>,
}

#[derive(Debug, Clone, PartialEq)]
pub struct FunctionDef {
    pub name: String,
    pub args: Arguments,
    pub body: Vec<Node>,
    pub decorator_list: Vec<Node>,
    pub returns: Option<Box<Node>>,
    pub type_comment: Option<String>,
    pub source: Option<NodeSource>,
}

#[derive(Debug, Clone, PartialEq)]
pub struct GeneratorExp {
    pub elt: Box<Node>,
    pub generators: Vec<Comprehension>,
    pub source: Option<NodeSource>,
}

#[derive(Debug, Clone, PartialEq)]
pub struct If {
    pub test: Box<Node>,
    pub body: Vec<Node>,
    pub orelse: Vec<Node>,
    pub source: Option<NodeSource>,
}

#[derive(Debug, Clone, PartialEq)]
pub struct IfExp {
    pub test: Box<Node>,
    pub body: Box<Node>,
    pub orelse: Box<Node>,
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

#[derive(Debug, Clone, PartialEq)]
pub struct Lambda {
    pub args: Arguments,
    pub return_type: Option<Box<Node>>,
    pub body: Box<Node>,
    pub source: Option<NodeSource>,
}

#[derive(Debug, Clone, PartialEq)]
pub struct List {
    pub elts: Vec<Node>,
    pub source: Option<NodeSource>,
}

#[derive(Debug, Clone, PartialEq)]
pub struct ListComp {
    pub elt: Box<Node>,
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
    pub type_: Box<Node>,
    pub default: Box<Node>,
}

/// A module.
#[derive(Debug, Clone, PartialEq)]
pub struct Module {
    pub body: Vec<Node>,
    pub source: Option<NodeSource>,
}

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
}

#[derive(Debug, Clone, PartialEq)]
pub struct ProtocolDef {
    pub name: String,
    pub bases: Vec<Node>,
    pub properties: Vec<Node>,
    pub functions: Vec<Node>,
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
    pub value: Option<Box<Node>>,
    pub source: Option<NodeSource>,
}

#[derive(Debug, Clone, PartialEq)]
pub struct Set {
    pub elts: Vec<Node>,
    pub source: Option<NodeSource>,
}

#[derive(Debug, Clone, PartialEq)]
pub struct SetComp {
    pub elt: Box<Node>,
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
    pub protocols: Vec<Node>,
    pub members: Vec<Node>,
    pub properties: Vec<Node>,
    pub events: Vec<Node>,
    pub functions: Vec<Node>,
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
    pub orelse: Vec<Node>,
    pub finalbody: Vec<Node>,
    pub source: Option<NodeSource>,
}

#[derive(Debug, Clone, PartialEq)]
pub struct TryStar {
    pub body: Vec<Node>,
    pub handlers: Vec<ExceptHandler>,
    pub orelse: Vec<Node>,
    pub finalbody: Vec<Node>,
    pub source: Option<NodeSource>,
}

#[derive(Debug, Clone, PartialEq)]
pub struct Tuple {
    pub elts: Vec<Node>,
    pub source: Option<NodeSource>,
}

#[derive(Debug, Clone, PartialEq)]
pub struct TypeAlias {
    pub name: Box<Node>,
    pub type_params: Vec<Node>,
    pub value: Box<Node>,
    pub source: Option<NodeSource>,
}

/// A unary operation, e.g. `+x`, `-x`, or `not x`.
#[derive(Debug, Clone, PartialEq)]
pub struct UnaryOp_ {
    pub op: UnaryOp,
    pub operand: Box<Node>,
    pub source: Option<NodeSource>,
}

#[derive(Debug, Clone, PartialEq)]
pub struct While {
    pub test: Box<Node>,
    pub body: Vec<Node>,
    pub orelse: Vec<Node>,
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

#[derive(Debug, Clone, PartialEq)]
pub struct Yield {
    pub value: Option<Box<Node>>,
    pub source: Option<NodeSource>,
}

#[derive(Debug, Clone, PartialEq)]
pub struct YieldFrom {
    pub value: Box<Node>,
    pub source: Option<NodeSource>,
}
