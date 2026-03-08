# VS Code Extension Development

## Prerequisites

- [Node.js](https://nodejs.org/) (v18+)
- npm (comes with Node.js)

## Setup

From the extension directory (`editors/vscode/`):

```bash
npm install
```

## Building

The extension uses [esbuild](https://esbuild.github.io/) to bundle TypeScript source into `dist/extension.js`.

```bash
# Development build (with sourcemaps)
node esbuild.mjs

# Production build (minified, no sourcemaps)
node esbuild.mjs --production

# Watch mode (rebuilds on file changes)
node esbuild.mjs --watch
```

Or from the repo root using the build script:

```bash
build_tools/bin/build_sharpy build --project vscode
```

## Installing for Local Development

The extension is installed via a symlink so that builds are immediately available to VS Code:

```bash
ln -s "$(pwd)/editors/vscode" ~/.vscode/extensions/sharpy-lang
```

After creating the symlink, build the extension and reload VS Code (`Developer: Reload Window`).

## Troubleshooting

### "Cannot find module '.../dist/extension.js'"

The TypeScript source hasn't been compiled. Run:

```bash
cd editors/vscode
npm install   # if node_modules/ is missing
node esbuild.mjs
```

Then reload VS Code.

### Language server not starting

Ensure `sharpyc` is in your PATH or set `sharpy.serverPath` in VS Code settings. The compiler must be built first:

```bash
build_tools/bin/build_sharpy build --project cli
build_tools/bin/build_sharpy install
```

## Project Structure

```
editors/vscode/
├── src/
│   ├── extension.ts          # Extension entry point (activation, LSP client)
│   └── debugConfigProvider.ts # Debug configuration provider
├── syntaxes/
│   └── sharpy.tmLanguage.json # TextMate grammar for syntax highlighting
├── snippets/
│   └── sharpy.json            # Code snippets
├── dist/
│   └── extension.js           # Compiled output (git-ignored, built by esbuild)
├── esbuild.mjs                # Build script
├── tsconfig.json               # TypeScript configuration
├── package.json                # Extension manifest and dependencies
└── language-configuration.json # Bracket matching, comment toggling, etc.
```
