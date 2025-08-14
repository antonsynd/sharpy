use unicode_xid::UnicodeXID;

/// Check if a character can start an identifier.
#[must_use]
pub fn is_id_start(ch: char) -> bool {
    ch.is_xid_start() || ch == '_'
}

/// Check if a character can continue an identifier.
#[must_use]
pub fn is_id_continue(ch: char) -> bool {
    ch.is_xid_continue()
}

/// Check if a string is a valid identifier.
pub fn is_valid_identifier(s: &str) -> bool {
    let mut chars = s.chars();
    match chars.next() {
        Some(first) if is_id_start(first) => chars.all(is_id_continue),
        _ => false,
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_is_id_start() {
        assert!(is_id_start('a'));
        assert!(is_id_start('A'));
        assert!(is_id_start('_'));

        assert!(!is_id_start('1'));
        assert!(!is_id_start('-'));
        assert!(!is_id_start(' '));
        assert!(!is_id_start('`'));
        assert!(!is_id_start('$'));
    }

    #[test]
    fn test_is_id_continue() {
        assert!(is_id_continue('a'));
        assert!(is_id_continue('A'));
        assert!(is_id_continue('_'));
        assert!(is_id_continue('1'));

        assert!(!is_id_continue('-'));
        assert!(!is_id_continue(' '));
        assert!(!is_id_continue('`'));
        assert!(!is_id_continue('$'));
    }

    #[test]
    fn test_is_valid_identifier() {
        assert!(is_valid_identifier("valid_identifier1"));
        assert!(is_valid_identifier("_valid_identifier1"));
        assert!(is_valid_identifier("__valid_identifier1"));
        assert!(is_valid_identifier("_"));
        assert!(is_valid_identifier("__"));

        assert!(!is_valid_identifier("1invalid_identifier"));
        assert!(!is_valid_identifier("invalid-identifier"));

        assert!(!is_valid_identifier("$invalid_identifier"));
        assert!(!is_valid_identifier("$$invalid_identifier"));
        assert!(!is_valid_identifier("`invalid_identifier"));
        assert!(!is_valid_identifier("``invalid_identifier"));
    }
}
