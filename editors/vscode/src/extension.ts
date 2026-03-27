import * as fs from "fs";
import * as path from "path";
import {
  commands,
  debug,
  ExtensionContext,
  StatusBarAlignment,
  StatusBarItem,
  window,
  workspace,
} from "vscode";
import { SharpyDebugConfigProvider } from "./debugConfigProvider";
import {
  LanguageClient,
  LanguageClientOptions,
  ServerOptions,
  TransportKind,
} from "vscode-languageclient/node";

/**
 * Validates an executable path before it is passed to `cp.spawn()` or a debug
 * launch configuration. Returns `true` when the path is safe to use.
 *
 * - Bare command names (no path separator) are allowed — the OS resolves them
 *   via PATH, same as typing the command in a terminal.
 * - Paths containing a separator must resolve to an existing file.
 * - Empty / whitespace-only strings are rejected.
 */
export function validateExecutablePath(execPath: string): boolean {
  if (!execPath || !execPath.trim()) {
    return false;
  }

  // Bare command name — trust PATH resolution
  if (!execPath.includes("/") && !execPath.includes("\\")) {
    return true;
  }

  // Path with separator — resolve and check existence
  const resolved = path.resolve(execPath);
  try {
    const stat = fs.statSync(resolved);
    return stat.isFile();
  } catch {
    return false;
  }
}

let client: LanguageClient | undefined;
let statusBarItem: StatusBarItem | undefined;

function createClient(context: ExtensionContext): LanguageClient {
  const config = workspace.getConfiguration("sharpy");
  let serverPath = config.get<string>("serverPath") || "sharpyc";

  if (!validateExecutablePath(serverPath)) {
    const wasCustom = serverPath !== "sharpyc";
    if (wasCustom) {
      window.showWarningMessage(
        `Sharpy: configured serverPath "${serverPath}" is not a valid executable. Falling back to "sharpyc".`
      );
      serverPath = "sharpyc";
    }
  }

  const serverOptions: ServerOptions = {
    command: serverPath,
    args: ["lsp"],
    transport: TransportKind.stdio,
  };

  const clientOptions: LanguageClientOptions = {
    documentSelector: [{ scheme: "file", language: "sharpy" }],
    synchronize: {
      fileEvents: [
        workspace.createFileSystemWatcher("**/*.spy"),
        workspace.createFileSystemWatcher("**/*.spyproj"),
      ],
    },
    outputChannelName: "Sharpy Language Server",
  };

  const lc = new LanguageClient(
    "sharpy",
    "Sharpy Language Server",
    serverOptions,
    clientOptions
  );

  lc.outputChannel.appendLine(`Server command: ${serverPath} lsp`);

  return lc;
}

function updateStatusBar(running: boolean, toolchainVersion?: string): void {
  if (!statusBarItem) {
    return;
  }
  if (running) {
    const versionSuffix = toolchainVersion ? ` v${toolchainVersion}` : "";
    statusBarItem.text = `$(check) Sharpy${versionSuffix}`;
    statusBarItem.tooltip = toolchainVersion
      ? `Sharpy Language Server is running\nToolchain: ${toolchainVersion}`
      : "Sharpy Language Server is running";
  } else {
    statusBarItem.text = "$(warning) Sharpy";
    statusBarItem.tooltip = "Sharpy Language Server is stopped";
  }
}

async function startClient(context: ExtensionContext): Promise<void> {
  client = createClient(context);
  try {
    await client.start();
    const serverVersion = client.initializeResult?.serverInfo?.version;
    updateStatusBar(true, serverVersion);
  } catch (err) {
    const msg = err instanceof Error ? err.message : String(err);
    client.outputChannel.appendLine(`Failed to start server: ${msg}`);
    updateStatusBar(false);
    window.showWarningMessage(
      `Sharpy Language Server failed to start. Check the output channel for details.`
    );
  }
}

async function stopClient(): Promise<void> {
  if (client) {
    await client.stop();
    client = undefined;
  }
  updateStatusBar(false);
}

export async function activate(context: ExtensionContext): Promise<void> {
  statusBarItem = window.createStatusBarItem(StatusBarAlignment.Left, 0);
  statusBarItem.text = "$(loading~spin) Sharpy";
  statusBarItem.tooltip = "Sharpy Language Server is starting...";
  statusBarItem.show();
  context.subscriptions.push(statusBarItem);

  context.subscriptions.push(
    debug.registerDebugConfigurationProvider(
      "sharpy",
      new SharpyDebugConfigProvider()
    )
  );

  context.subscriptions.push(
    commands.registerCommand("sharpy.restartServer", async () => {
      await stopClient();
      await startClient(context);
      window.showInformationMessage("Sharpy Language Server restarted.");
    })
  );

  context.subscriptions.push(
    commands.registerCommand("sharpy.showOutputChannel", () => {
      if (client) {
        client.outputChannel.show();
      }
    })
  );

  await startClient(context);
}

export async function deactivate(): Promise<void> {
  await stopClient();
}
