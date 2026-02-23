"""
CLI command builder for AI backends.

Provides utilities for building CLI commands for different backend types
with proper argument formatting and escaping.
"""

from dataclasses import dataclass
from typing import Optional
from .backends import ToolPermission, BackendType


@dataclass
class CLICommand:
    """Represents a CLI command to execute.

    Attributes:
        args: Command arguments as a list (e.g., ["claude", "--print"])
        stdin: Optional input to send via stdin
        cwd: Optional working directory for command execution
    """

    args: list[str]
    stdin: Optional[str] = None
    cwd: Optional[str] = None


class CLIBuilder:
    """Builds CLI commands for different backend types.

    This class provides methods to construct properly formatted CLI commands
    for various AI backends, handling tool permissions, model selection,
    and backend-specific flags.

    Example:
        ```python
        from build_tools.shared.cli_builder import CLIBuilder
        from build_tools.shared.backends import ToolPermission

        tools = {ToolPermission.READ, ToolPermission.WRITE}
        cmd = CLIBuilder.build_claude_command(
            prompt="Generate a function",
            tools=tools,
            model="claude-sonnet-4-6"
        )

        # cmd.args = ["claude", "--print", "--allowedTools", "Read,Write", "--model", "..."]
        # cmd.stdin = "Generate a function"
        ```
    """

    @classmethod
    def build_claude_command(
        cls,
        prompt: str,
        tools: set[ToolPermission],
        model: Optional[str] = None,
        print_mode: bool = True,
        cli_path: str = "claude",
    ) -> CLICommand:
        """Build Claude Code CLI command.

        Args:
            prompt: The prompt to send to Claude
            tools: Set of allowed tool permissions
            model: Optional model identifier (e.g., "claude-sonnet-4-6")
            print_mode: Whether to use --print flag (default: True)
            cli_path: Path to claude CLI executable (default: "claude")

        Returns:
            CLICommand with args and stdin configured

        Example:
            ```python
            cmd = CLIBuilder.build_claude_command(
                prompt="Write a test",
                tools={ToolPermission.READ, ToolPermission.WRITE},
                model="claude-haiku-4-5-20251001"
            )
            ```
        """
        args = [cli_path]

        if print_mode:
            args.append("--print")

        # Add tool permissions as comma-separated list
        if tools:
            tools_str = ",".join(t.value for t in sorted(tools, key=lambda x: x.value))
            args.extend(["--allowedTools", tools_str])

        # Add model if specified
        if model:
            args.extend(["--model", model])

        # Claude accepts prompt via stdin
        return CLICommand(args=args, stdin=prompt)

    @classmethod
    def build_copilot_command(
        cls,
        prompt: str,
        tools: set[ToolPermission],
        cli_path: str = "/opt/homebrew/bin/copilot",
    ) -> CLICommand:
        """Build GitHub Copilot CLI command.

        Args:
            prompt: The prompt to send to Copilot
            tools: Set of allowed tool permissions
            cli_path: Path to copilot CLI executable

        Returns:
            CLICommand with args configured (no stdin needed for Copilot)

        Note:
            Copilot CLI does not support model selection via command-line flags.

        Example:
            ```python
            cmd = CLIBuilder.build_copilot_command(
                prompt="Write a test",
                tools={ToolPermission.READ, ToolPermission.BASH}
            )
            ```
        """
        args = [cli_path, "--prompt", prompt]

        # Add tool permissions - Copilot uses separate --allow-tool flags
        # with lowercase tool names
        if tools:
            for tool in sorted(tools, key=lambda x: x.value):
                tool_name = tool.value.lower()
                args.extend(["--allow-tool", tool_name])

        # Copilot doesn't use stdin for prompts
        return CLICommand(args=args, stdin=None)

    @classmethod
    def build_command(
        cls,
        backend_type: BackendType,
        prompt: str,
        tools: set[ToolPermission],
        model: Optional[str] = None,
        **kwargs,
    ) -> CLICommand:
        """Build command for any backend type.

        This is a convenience method that delegates to the appropriate
        backend-specific builder based on the backend type.

        Args:
            backend_type: The type of backend to build a command for
            prompt: The prompt to send
            tools: Set of allowed tool permissions
            model: Optional model identifier (only used for Claude)
            **kwargs: Additional backend-specific arguments

        Returns:
            CLICommand for the specified backend

        Raises:
            ValueError: If backend_type is not supported

        Example:
            ```python
            cmd = CLIBuilder.build_command(
                BackendType.CLAUDE_CODE,
                prompt="Generate code",
                tools={ToolPermission.READ},
                model="claude-sonnet-4-6"
            )
            ```
        """
        if backend_type == BackendType.CLAUDE_CODE:
            return cls.build_claude_command(
                prompt=prompt,
                tools=tools,
                model=model,
                **kwargs,
            )
        elif backend_type == BackendType.COPILOT:
            return cls.build_copilot_command(
                prompt=prompt,
                tools=tools,
                **kwargs,
            )
        else:
            raise ValueError(f"Unsupported backend type: {backend_type}")
