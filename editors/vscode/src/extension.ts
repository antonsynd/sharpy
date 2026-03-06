import * as path from "path";
import {
  commands,
  ExtensionContext,
  StatusBarAlignment,
  StatusBarItem,
  window,
  workspace,
} from "vscode";
import {
  LanguageClient,
  LanguageClientOptions,
  ServerOptions,
  TransportKind,
} from "vscode-languageclient/node";

let client: LanguageClient | undefined;
let statusBarItem: StatusBarItem | undefined;

function createClient(context: ExtensionContext): LanguageClient {
  const config = workspace.getConfiguration("sharpy");
  const serverPath = config.get<string>("serverPath") || "sharpyc";

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
  };

  return new LanguageClient(
    "sharpy",
    "Sharpy Language Server",
    serverOptions,
    clientOptions
  );
}

function updateStatusBar(running: boolean): void {
  if (!statusBarItem) {
    return;
  }
  if (running) {
    statusBarItem.text = "$(check) Sharpy";
    statusBarItem.tooltip = "Sharpy Language Server is running";
  } else {
    statusBarItem.text = "$(warning) Sharpy";
    statusBarItem.tooltip = "Sharpy Language Server is stopped";
  }
}

async function startClient(context: ExtensionContext): Promise<void> {
  client = createClient(context);
  await client.start();
  updateStatusBar(true);
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
