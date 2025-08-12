use unicode_xid::UnicodeXID;

/// Check if a character can start an identifier
#[must_use]
pub fn is_id_start(ch: char) -> bool {
    ch.is_xid_start() || ch == '_'
}

/// Check if a character can continue an identifier
#[must_use]
pub fn is_id_continue(ch: char) -> bool {
    ch.is_xid_continue()
}

/// Check if a string is a valid identifier
pub fn is_valid_identifier(s: &str) -> bool {
    let mut chars = s.chars();
    match chars.next() {
        Some(first) if is_id_start(first) => {
            chars.all(is_id_continue)
        }
        _ => false,
    }
}
