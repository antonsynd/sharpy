#[derive(Debug, Clone, PartialEq, Eq)]
pub struct SourceLocation {
    pub line: usize,
    pub column: usize,
    pub start: usize,
    pub end: usize,
}

impl SourceLocation {
    /// Creates a new source location.
    #[must_use]
    pub const fn new(line: usize, column: usize, start: usize, end: usize) -> Self {
        Self { line, column, start, end }
    }

    /// Creates a source location for a single character.
    #[must_use]
    pub const fn single_char(line: usize, column: usize, position: usize) -> Self {
        Self {
            line,
            column,
            start: position,
            end: position + 1,
        }
    }
}
