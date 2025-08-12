use clap::Parser;

/// A simple program that greets someone
#[derive(Parser, Debug)]
#[command(name = "hello")]
#[command(about = "A simple greeting program", long_about = None)]
struct Args {
    /// Name of the person to greet
    #[arg(short, long, default_value = "World")]
    name: String,

    /// Number of times to greet
    #[arg(short = 'c', long, default_value_t = 1)]
    count: u8,

    /// Use uppercase for the greeting
    #[arg(short, long)]
    uppercase: bool,
}

fn main() {
    let args = Args::parse();

    let greeting = if args.uppercase {
        format!("HELLO {}!", args.name.to_uppercase())
    } else {
        format!("Hello {}!", args.name)
    };

    for _ in 0..args.count {
        println!("{}", greeting);
    }
}
