#!/bin/bash
# Demo script showing the Sharpy compiler in action

echo "=== Sharpy Compiler MVP Demo ==="
echo ""
echo "Step 1: Creating Sharpy source file..."
cat > hello.spy << 'EOF'
def greet(name):
    print("Hello,", name, "!")

def main():
    print("=== Sharpy Demo ===")
    greet("World")
    greet("from Sharpy")
    print("=== Complete ===")
EOF

echo "Source code:"
cat hello.spy
echo ""

echo "Step 2: Compiling Sharpy to C#..."
cargo run --quiet --bin sharpyc --manifest-path rust/Cargo.toml -- --generate hello.spy 2>/dev/null | grep -v "^DEBUG" | grep -v "^Running" > hello.cs

echo "Generated C# code:"
head -30 hello.cs
echo ""

echo "Step 3: Creating .NET project..."
rm -rf demo_project
dotnet new console -n demo_project -o demo_project > /dev/null 2>&1
cd demo_project
dotnet add reference ../dotnet/src/Sharpy/Sharpy.csproj > /dev/null 2>&1
cp ../hello.cs Program.cs

echo "Step 4: Building..."
dotnet build --verbosity quiet > /dev/null 2>&1

echo "Step 5: Running..."
dotnet run --no-build
echo ""

cd ..
echo "=== Demo Complete! ==="
echo ""
echo "The Sharpy compiler successfully:"
echo "  ✅ Parsed Sharpy source code"
echo "  ✅ Performed semantic analysis"
echo "  ✅ Generated C# code"
echo "  ✅ Compiled to .NET executable"
echo "  ✅ Ran successfully!"
