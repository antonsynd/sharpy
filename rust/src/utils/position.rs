#[derive(Debug, Clone, PartialEq, Eq)]
pub struct SourceLocation {
    /// The line number, 1-indexed.
    pub line: usize,

    /// The column number, 1-indexed, based on Unicode scalar value offsets.
    pub column: usize,

    /// The start position in bytes (not Unicode scalar value offsets).
    pub start: usize,

    /// The end position in bytes (not Unicode scalar value offsets).
    pub end: usize,
}

impl SourceLocation {
    /// Creates a new source location.
    #[must_use]
    pub const fn new(line: usize, column: usize, start: usize, end: usize) -> Self {
        Self {
            line,
            column,
            start,
            end,
        }
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
