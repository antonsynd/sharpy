use crate::lexer::token::TokenType;
use std::collections::HashMap;

pub struct KeywordMap {
    keywords: HashMap<&'static str, TokenType>,
    soft_keywords: HashMap<&'static str, TokenType>,
}

impl KeywordMap {
    /// Creates a new keyword map with all Sharpy keywords.
    #[must_use]
    pub fn new() -> Self {
        let mut keywords = HashMap::new();
        let mut soft_keywords = HashMap::new();

        // Hard keywords - these are always reserved
        keywords.insert("False", TokenType::False);
        keywords.insert("True", TokenType::True);
        keywords.insert("None", TokenType::None);
        keywords.insert("and", TokenType::And);
        keywords.insert("or", TokenType::Or);
        keywords.insert("not", TokenType::Not);
        keywords.insert("class", TokenType::Class);
        keywords.insert("struct", TokenType::Struct);
        keywords.insert("protocol", TokenType::Protocol);
        keywords.insert("property", TokenType::Property);
        keywords.insert("def", TokenType::Def);
        keywords.insert("return", TokenType::Return);
        keywords.insert("if", TokenType::If);
        keywords.insert("else", TokenType::Else);
        keywords.insert("elif", TokenType::Elif);
        keywords.insert("for", TokenType::For);
        keywords.insert("while", TokenType::While);
        keywords.insert("break", TokenType::Break);
        keywords.insert("continue", TokenType::Continue);
        keywords.insert("pass", TokenType::Pass);
        keywords.insert("try", TokenType::Try);
        keywords.insert("except", TokenType::Except);
        keywords.insert("finally", TokenType::Finally);
        keywords.insert("raise", TokenType::Raise);
        keywords.insert("import", TokenType::Import);
        keywords.insert("from", TokenType::From);
        keywords.insert("as", TokenType::As);
        keywords.insert("async", TokenType::Async);
        keywords.insert("await", TokenType::Await);
        keywords.insert("assert", TokenType::Assert);
        keywords.insert("del", TokenType::Del);
        keywords.insert("global", TokenType::Global);
        keywords.insert("nonlocal", TokenType::Nonlocal);
        keywords.insert("with", TokenType::With);
        keywords.insert("yield", TokenType::Yield);
        keywords.insert("in", TokenType::In);
        keywords.insert("is", TokenType::Is);
        keywords.insert("lambda", TokenType::Lambda);
        keywords.insert("event", TokenType::Event);

        // Soft keywords - context dependent
        soft_keywords.insert("type", TokenType::Type);
        soft_keywords.insert("match", TokenType::Match);
        soft_keywords.insert("case", TokenType::Case);
        soft_keywords.insert("get", TokenType::Get);
        soft_keywords.insert("set", TokenType::Set);
        soft_keywords.insert("_", TokenType::Wildcard);

        Self { keywords, soft_keywords }
    }

    /// Gets a hard keyword token type for the given identifier.
    #[must_use]
    pub fn get_keyword(&self, identifier: &str) -> Option<&TokenType> {
        self.keywords.get(identifier)
    }

    /// Gets a soft keyword token type for the given identifier.
    #[must_use]
    pub fn get_soft_keyword(&self, identifier: &str) -> Option<&TokenType> {
        self.soft_keywords.get(identifier)
    }

    /// Checks if the identifier is a hard keyword.
    #[must_use]
    pub fn is_keyword(&self, identifier: &str) -> bool {
        self.keywords.contains_key(identifier)
    }

    /// Checks if the identifier is a soft keyword.
    #[must_use]
    pub fn is_soft_keyword(&self, identifier: &str) -> bool {
        self.soft_keywords.contains_key(identifier)
    }
}

impl Default for KeywordMap {
    fn default() -> Self {
        Self::new()
    }
}
