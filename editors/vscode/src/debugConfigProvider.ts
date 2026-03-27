import * as path from "path";
import {
  CancellationToken,
  DebugConfiguration,
  DebugConfigurationProvider,
  ProviderResult,
  window,
  workspace,
  WorkspaceFolder,
} from "vscode";
import { validateExecutablePath } from "./extension";

export class SharpyDebugConfigProvider implements DebugConfigurationProvider {
  resolveDebugConfiguration(
    folder: WorkspaceFolder | undefined,
    config: DebugConfiguration,
    _token?: CancellationToken
  ): ProviderResult<DebugConfiguration> {
    if (!config.type && !config.request && !config.name) {
      const editor = window.activeTextEditor;
      if (editor && editor.document.languageId === "sharpy") {
        config.type = "sharpy";
        config.name = "Run Sharpy File";
        config.request = "launch";
        config.program = "${file}";
      }
    }

    if (!config.program) {
      window.showErrorMessage(
        "Cannot start debugging: no Sharpy file specified in 'program'."
      );
      return undefined;
    }

    const sharpyConfig = workspace.getConfiguration("sharpy");
    const serverPath = sharpyConfig.get<string>("serverPath") || "sharpyc";
    const dotnetPath =
      sharpyConfig.get<string>("debug.dotnetPath") || "dotnet";

    if (!validateExecutablePath(serverPath)) {
      window.showErrorMessage(
        `Sharpy: configured serverPath "${serverPath}" is not a valid executable. Debug session cancelled.`
      );
      return undefined;
    }

    if (!validateExecutablePath(dotnetPath)) {
      window.showErrorMessage(
        `Sharpy: configured dotnetPath "${dotnetPath}" is not a valid executable. Debug session cancelled.`
      );
      return undefined;
    }

    const spyFile = config.program as string;
    const workDir = folder?.uri.fsPath ?? path.dirname(spyFile);

    const resolved: DebugConfiguration = {
      type: "coreclr",
      request: "launch",
      name: config.name,
      preLaunchTask: undefined,
      program: dotnetPath,
      args: ["run", "--project", serverPath, "--", "run", spyFile],
      cwd: workDir,
      console: config.console ?? "integratedTerminal",
      stopAtEntry: config.stopAtEntry ?? false,
      env: config.env,
    };

    return resolved;
  }
}
