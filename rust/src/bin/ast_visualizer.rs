#!/usr/bin/env rust

use anyhow::{Context, Result};
use clap::Parser;
use sharpy_compiler_toolchain::ast::node::{Module, NodeSource};
use sharpy_compiler_toolchain::lexer::SharpyLexer;
use sharpy_compiler_toolchain::parser::Parser as SharpyParser;
use std::fs;
use std::path::PathBuf;
use std::process::Command;

// Include the ast_renderer code directly since we're in a binary
include!("visualizer/ast_renderer.rs");

#[derive(Parser)]
#[command(name = "sharpy-ast-visualizer")]
#[command(about = "Generate AST diagrams from Sharpy source files")]
struct Args {
    /// Input Sharpy source file
    #[arg(short, long)]
    input: PathBuf,

    /// Output directory for PNG files
    #[arg(short, long, default_value = ".")]
    output_dir: PathBuf,

    /// Base name for output files (default: input file stem)
    #[arg(short, long)]
    basename: Option<String>,

    /// Keep temporary DOT files
    #[arg(long)]
    keep_temp: bool,

    /// Enable debug output
    #[arg(long)]
    debug: bool,

    /// Render only specific node types (comma-separated)
    #[arg(long)]
    filter_nodes: Option<String>,

    /// Skip rendering and just output DOT format
    #[arg(long)]
    dot_only: bool,
}

fn main() -> Result<()> {
    let args = Args::parse();

    // Read the input file
    let source = fs::read_to_string(&args.input)
        .with_context(|| format!("Failed to read input file: {}", args.input.display()))?;

    if args.debug {
        println!("Parsing Sharpy source file: {}", args.input.display());
    }

    // Parse the source code
    let mut lexer = SharpyLexer::new(&source);
    let tokens = lexer
        .tokenize_all()
        .map_err(|errors| anyhow::anyhow!("Failed to tokenize input: {:?}", errors))?;

    let mut parser = SharpyParser::new(tokens);
    let statements = parser
        .parse()
        .map_err(|error| anyhow::anyhow!("Failed to parse input: {:?}", error))?;

    // Create a Module node to represent the entire AST
    let module = Module {
        body: statements,
        source: Some(NodeSource {
            line_start: 1,
            col_start: 1,
            line_end: source.lines().count(),
            col_end: source.lines().last().map_or(0, str::len),
        }),
    };
    let ast = sharpy_compiler_toolchain::ast::node::Node::Module(module);

    if args.debug {
        println!("Successfully parsed AST with root node: {ast:?}");
    }

    // Create output directory if it doesn't exist
    fs::create_dir_all(&args.output_dir).with_context(|| {
        format!(
            "Failed to create output directory: {}",
            args.output_dir.display()
        )
    })?;

    // Determine output base name
    let basename = args.basename.unwrap_or_else(|| {
        args.input
            .file_stem()
            .and_then(|s| s.to_str())
            .unwrap_or("sharpy_ast")
            .to_string()
    });

    // Create the renderer
    let filter_nodes: Option<Vec<String>> = args
        .filter_nodes
        .map(|s| s.split(',').map(|s| s.trim().to_string()).collect());

    let renderer = ASTRenderer::new(filter_nodes, args.debug);

    // Generate DOT content
    let dot_content = renderer.render_ast(&ast)?;

    if args.debug {
        println!("Generated DOT content ({} bytes)", dot_content.len());
    }

    // Output paths
    let dot_path = args.output_dir.join(format!("{basename}.dot"));
    let png_path = args.output_dir.join(format!("{basename}.png"));

    // Write DOT file
    fs::write(&dot_path, &dot_content)
        .with_context(|| format!("Failed to write DOT file: {}", dot_path.display()))?;

    if args.dot_only {
        println!("DOT file written to: {}", dot_path.display());
        return Ok(());
    }

    // Check if Graphviz is available
    let dot_available = Command::new("dot")
        .arg("-V")
        .output()
        .map(|output| output.status.success())
        .unwrap_or(false);

    if !dot_available {
        eprintln!("Warning: Graphviz 'dot' command not found. Only DOT file will be generated.");
        println!("DOT file written to: {}", dot_path.display());
        println!(
            "To generate PNG: dot -Tpng {} -o {}",
            dot_path.display(),
            png_path.display()
        );
        return Ok(());
    }

    // Generate PNG using Graphviz
    let output = Command::new("dot")
        .args(["-Tpng", "-o"])
        .arg(&png_path)
        .arg(&dot_path)
        .output()
        .with_context(|| "Failed to execute dot command")?;

    if !output.status.success() {
        let stderr = String::from_utf8_lossy(&output.stderr);
        anyhow::bail!("Graphviz dot command failed: {}", stderr);
    }

    println!("AST PNG generated: {}", png_path.display());

    // Clean up temporary DOT file unless requested to keep
    if args.keep_temp {
        println!("DOT file kept at: {}", dot_path.display());
    } else if let Err(e) = fs::remove_file(&dot_path) {
        eprintln!("Warning: Failed to remove temporary DOT file: {e}");
    }

    Ok(())
}
