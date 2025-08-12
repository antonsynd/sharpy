use clap::Parser;
use sharpy_compiler_toolchain::{SharpyLexer, TokenType};
use std::fs;
use std::io::{self, Read};

/// Sharpy language compiler toolchain
#[derive(Parser, Debug)]
#[command(name = "sharpyc")]
#[command(about = "Sharpy language compiler", long_about = None)]
struct Args {
    /// Input file to compile
    input: Option<String>,

    /// Tokenize only (lexer test mode)
    #[arg(short, long)]
    tokenize: bool,

    /// Verbose output
    #[arg(short, long)]
    verbose: bool,
}

fn main() {
    let args = Args::parse();

    let input = args.input.map_or_else(
        || {
            // Read from stdin
            let mut buffer = String::new();
            match io::stdin().read_to_string(&mut buffer) {
                Ok(_) => buffer,
                Err(err) => {
                    eprintln!("Error reading from stdin: {err}");
                    std::process::exit(1);
                }
            }
        },
        |filename| match fs::read_to_string(&filename) {
            Ok(content) => content,
            Err(err) => {
                eprintln!("Error reading file '{filename}': {err}");
                std::process::exit(1);
            }
        },
    );

    if args.tokenize {
        tokenize_input(&input, args.verbose);
    } else {
        println!("Compilation not yet implemented. Use --tokenize to test the lexer.");
    }
}

fn tokenize_input(input: &str, verbose: bool) {
    let mut lexer = SharpyLexer::new(input);

    match lexer.tokenize_all() {
        Ok(tokens) => {
            if verbose {
                println!("Successfully tokenized {} tokens:", tokens.len());
                for (i, token) in tokens.iter().enumerate() {
                    println!("{i:3}: {token:?}");
                }
            } else {
                for token in &tokens {
                    match &token.token_type {
                        TokenType::Eof => break,
                        _ => println!("{:?}: '{}'", token.token_type, token.lexeme),
                    }
                }
            }
        }
        Err(errors) => {
            eprintln!("Lexer errors:");
            for error in &errors {
                eprintln!("  {error}");
            }
            std::process::exit(1);
        }
    }
}
