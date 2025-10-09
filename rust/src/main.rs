use clap::Parser;
use sharpy_compiler_toolchain::{
    Parser as SharpyParser, SharpyLexer, TokenType, codegen::CodeGenerator,
    semantic::SemanticAnalyzer,
};
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

    /// Parse only (parser test mode)
    #[arg(short, long)]
    parse: bool,

    /// Generate C# code (full compilation)
    #[arg(short = 'g', long)]
    generate: bool,

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
    } else if args.parse {
        parse_input(&input, args.verbose);
    } else if args.generate {
        generate_code(&input, args.verbose);
    } else {
        println!(
            "Compilation not yet implemented. Use --tokenize, --parse, or --generate to test different stages."
        );
    }
}

fn parse_input(input: &str, verbose: bool) {
    let mut lexer = SharpyLexer::new(input);

    match lexer.tokenize_all() {
        Ok(tokens) => {
            if verbose {
                println!("Successfully tokenized {} tokens", tokens.len());
            }

            let mut parser = SharpyParser::new(tokens);
            match parser.parse_module() {
                Ok(module_ast) => {
                    if verbose {
                        println!("Successfully parsed module:");
                        println!("{module_ast:#?}");
                    } else {
                        println!("Parse successful - Module AST generated");
                    }
                }
                Err(error) => {
                    eprintln!("Parser error: {error}");
                    std::process::exit(1);
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
                        _ => println!("{:?}", token.token_type),
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

fn generate_code(input: &str, verbose: bool) {
    // Step 1: Tokenize
    let mut lexer = SharpyLexer::new(input);
    let tokens = match lexer.tokenize_all() {
        Ok(tokens) => {
            if verbose {
                println!("[1/3] Tokenization successful ({} tokens)", tokens.len());
            }
            tokens
        }
        Err(errors) => {
            eprintln!("Lexer errors:");
            for error in &errors {
                eprintln!("  {error}");
            }
            std::process::exit(1);
        }
    };

    // Step 2: Parse
    let mut parser = SharpyParser::new(tokens);
    let module_node = match parser.parse_module() {
        Ok(ast) => {
            if verbose {
                println!("[2/3] Parsing successful");
            }
            ast
        }
        Err(error) => {
            eprintln!("Parser error: {error}");
            std::process::exit(1);
        }
    };

    // Extract the Module from the Node
    let module = if let sharpy_compiler_toolchain::ast::Node::Module(m) = module_node {
        m
    } else {
        eprintln!("Expected Module node from parser");
        std::process::exit(1);
    };

    // Step 2.5: Semantic Analysis
    let mut analyzer = SemanticAnalyzer::new();
    if let Err(errors) = analyzer.analyze_module(&module.body, Some("main".to_string())) {
        eprintln!("Semantic errors:");
        for error in &errors {
            eprintln!("  {error}");
        }
        std::process::exit(1);
    }
    if verbose {
        println!("[2.5/3] Semantic analysis successful");
    }

    // Get the symbol table from the analyzer
    let symbol_table = analyzer.get_symbol_table().clone();

    // Step 3: Generate C# code
    let mut generator = CodeGenerator::new(symbol_table);
    match generator.generate(&module) {
        Ok(csharp_code) => {
            if verbose {
                println!("[3/3] Code generation successful\n");
                println!("Generated C# code:");
                println!("{}", "=".repeat(60));
            }
            println!("{csharp_code}");
            if verbose {
                println!("{}", "=".repeat(60));
            }
        }
        Err(error) => {
            eprintln!("Code generation error: {error}");
            std::process::exit(1);
        }
    }
}
