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
        keywords.insert("and", TokenType::And);
        keywords.insert("as", TokenType::As);
        keywords.insert("assert", TokenType::Assert);
        keywords.insert("async", TokenType::Async);
        keywords.insert("await", TokenType::Await);
        keywords.insert("break", TokenType::Break);
        keywords.insert("class", TokenType::Class);
        keywords.insert("continue", TokenType::Continue);
        keywords.insert("def", TokenType::Def);
        keywords.insert("del", TokenType::Del);
        keywords.insert("elif", TokenType::Elif);
        keywords.insert("else", TokenType::Else);
        keywords.insert("except", TokenType::Except);
        keywords.insert("False", TokenType::False);
        keywords.insert("finally", TokenType::Finally);
        keywords.insert("for", TokenType::For);
        keywords.insert("from", TokenType::From);
        keywords.insert("if", TokenType::If);
        keywords.insert("in", TokenType::In);
        keywords.insert("is", TokenType::Is);
        keywords.insert("import", TokenType::Import);
        keywords.insert("lambda", TokenType::Lambda);
        keywords.insert("None", TokenType::None);
        keywords.insert("not", TokenType::Not);
        keywords.insert("or", TokenType::Or);
        keywords.insert("pass", TokenType::Pass);
        keywords.insert("property", TokenType::Property);
        keywords.insert("protocol", TokenType::Protocol);
        keywords.insert("raise", TokenType::Raise);
        keywords.insert("return", TokenType::Return);
        keywords.insert("struct", TokenType::Struct);
        keywords.insert("True", TokenType::True);
        keywords.insert("try", TokenType::Try);
        keywords.insert("while", TokenType::While);
        keywords.insert("with", TokenType::With);
        keywords.insert("yield", TokenType::Yield);

        // Soft keywords - context dependent
        soft_keywords.insert("_", TokenType::Wildcard);
        soft_keywords.insert("case", TokenType::Case);
        soft_keywords.insert("event", TokenType::Event);
        soft_keywords.insert("get", TokenType::Get);
        soft_keywords.insert("match", TokenType::Match);
        soft_keywords.insert("set", TokenType::Set);
        soft_keywords.insert("type", TokenType::Type);

        Self {
            keywords,
            soft_keywords,
        }
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
