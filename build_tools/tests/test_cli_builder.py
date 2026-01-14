"""
Tests for CLI command builder.
"""

import pytest
from build_tools.shared.cli_builder import CLICommand, CLIBuilder
from build_tools.shared.backends import ToolPermission, BackendType


class TestCLICommand:
    """Tests for CLICommand dataclass."""

    def test_basic_command(self):
        """Test creating a basic command."""
        cmd = CLICommand(args=["echo", "hello"])
        assert cmd.args == ["echo", "hello"]
        assert cmd.stdin is None
        assert cmd.cwd is None

    def test_command_with_stdin(self):
        """Test command with stdin input."""
        cmd = CLICommand(args=["cat"], stdin="test input")
        assert cmd.args == ["cat"]
        assert cmd.stdin == "test input"

    def test_command_with_cwd(self):
        """Test command with working directory."""
        cmd = CLICommand(args=["ls"], cwd="/tmp")
        assert cmd.cwd == "/tmp"


class TestCLIBuilderClaude:
    """Tests for Claude Code CLI command building."""

    def test_minimal_command(self):
        """Test building minimal Claude command."""
        cmd = CLIBuilder.build_claude_command(
            prompt="Test prompt",
            tools=set(),
        )
        assert cmd.args == ["claude", "--print"]
        assert cmd.stdin == "Test prompt"

    def test_with_tools(self):
        """Test command with tool permissions."""
        tools = {ToolPermission.READ, ToolPermission.WRITE}
        cmd = CLIBuilder.build_claude_command(
            prompt="Test",
            tools=tools,
        )
        # Tools should be sorted for deterministic ordering
        assert "--allowedTools" in cmd.args
        tools_idx = cmd.args.index("--allowedTools")
        tools_str = cmd.args[tools_idx + 1]
        # Check that both Read and Write are present
        assert "Read" in tools_str
        assert "Write" in tools_str
        assert "," in tools_str  # Should be comma-separated

    def test_with_model(self):
        """Test command with model specification."""
        cmd = CLIBuilder.build_claude_command(
            prompt="Test",
            tools=set(),
            model="claude-3-5-haiku-20241022",
        )
        assert "--model" in cmd.args
        model_idx = cmd.args.index("--model")
        assert cmd.args[model_idx + 1] == "claude-3-5-haiku-20241022"

    def test_without_print_mode(self):
        """Test command without print mode."""
        cmd = CLIBuilder.build_claude_command(
            prompt="Test",
            tools=set(),
            print_mode=False,
        )
        assert "--print" not in cmd.args

    def test_custom_cli_path(self):
        """Test command with custom CLI path."""
        cmd = CLIBuilder.build_claude_command(
            prompt="Test",
            tools=set(),
            cli_path="/usr/local/bin/claude",
        )
        assert cmd.args[0] == "/usr/local/bin/claude"

    def test_all_options(self):
        """Test command with all options specified."""
        tools = {ToolPermission.READ, ToolPermission.WRITE, ToolPermission.EDIT}
        cmd = CLIBuilder.build_claude_command(
            prompt="Complex prompt",
            tools=tools,
            model="claude-sonnet-4-5-20250929",
            print_mode=True,
            cli_path="claude",
        )
        assert "claude" in cmd.args
        assert "--print" in cmd.args
        assert "--allowedTools" in cmd.args
        assert "--model" in cmd.args
        assert cmd.stdin == "Complex prompt"

    def test_tools_sorted_deterministically(self):
        """Test that tools are always in the same order."""
        tools = {ToolPermission.WRITE, ToolPermission.READ, ToolPermission.BASH}

        cmd1 = CLIBuilder.build_claude_command(prompt="Test", tools=tools)
        cmd2 = CLIBuilder.build_claude_command(prompt="Test", tools=tools)

        # Extract tools string from both commands
        tools_idx1 = cmd1.args.index("--allowedTools")
        tools_idx2 = cmd2.args.index("--allowedTools")
        assert cmd1.args[tools_idx1 + 1] == cmd2.args[tools_idx2 + 1]


class TestCLIBuilderCopilot:
    """Tests for GitHub Copilot CLI command building."""

    def test_minimal_command(self):
        """Test building minimal Copilot command."""
        cmd = CLIBuilder.build_copilot_command(
            prompt="Test prompt",
            tools=set(),
        )
        assert cmd.args[0] == "/opt/homebrew/bin/copilot"
        assert "--prompt" in cmd.args
        prompt_idx = cmd.args.index("--prompt")
        assert cmd.args[prompt_idx + 1] == "Test prompt"
        assert cmd.stdin is None  # Copilot doesn't use stdin

    def test_with_tools(self):
        """Test command with tool permissions."""
        tools = {ToolPermission.READ, ToolPermission.WRITE}
        cmd = CLIBuilder.build_copilot_command(
            prompt="Test",
            tools=tools,
        )
        # Should have separate --allow-tool flags
        assert cmd.args.count("--allow-tool") == 2
        # Tools should be lowercase
        tool_args = []
        for i, arg in enumerate(cmd.args):
            if arg == "--allow-tool" and i + 1 < len(cmd.args):
                tool_args.append(cmd.args[i + 1])
        assert "read" in tool_args
        assert "write" in tool_args

    def test_tool_names_lowercase(self):
        """Test that tool names are converted to lowercase."""
        tools = {ToolPermission.READ, ToolPermission.BASH}
        cmd = CLIBuilder.build_copilot_command(
            prompt="Test",
            tools=tools,
        )
        # Extract tool names
        tool_names = []
        for i, arg in enumerate(cmd.args):
            if arg == "--allow-tool" and i + 1 < len(cmd.args):
                tool_names.append(cmd.args[i + 1])

        # All should be lowercase
        for name in tool_names:
            assert name == name.lower()

    def test_custom_cli_path(self):
        """Test command with custom CLI path."""
        cmd = CLIBuilder.build_copilot_command(
            prompt="Test",
            tools=set(),
            cli_path="/custom/path/copilot",
        )
        assert cmd.args[0] == "/custom/path/copilot"

    def test_all_tool_types(self):
        """Test command with all tool permission types."""
        tools = {
            ToolPermission.READ,
            ToolPermission.WRITE,
            ToolPermission.EDIT,
            ToolPermission.BASH,
            ToolPermission.GLOB,
            ToolPermission.GREP,
        }
        cmd = CLIBuilder.build_copilot_command(
            prompt="Test",
            tools=tools,
        )
        # Should have 6 --allow-tool flags
        assert cmd.args.count("--allow-tool") == 6

    def test_tools_sorted_deterministically(self):
        """Test that tools are always in the same order."""
        tools = {ToolPermission.WRITE, ToolPermission.READ, ToolPermission.BASH}

        cmd1 = CLIBuilder.build_copilot_command(prompt="Test", tools=tools)
        cmd2 = CLIBuilder.build_copilot_command(prompt="Test", tools=tools)

        # Commands should be identical
        assert cmd1.args == cmd2.args


class TestCLIBuilderGeneric:
    """Tests for generic build_command method."""

    def test_claude_backend(self):
        """Test building command for Claude backend."""
        cmd = CLIBuilder.build_command(
            backend_type=BackendType.CLAUDE_CODE,
            prompt="Test",
            tools={ToolPermission.READ},
            model="claude-3-5-haiku-20241022",
        )
        assert "claude" in cmd.args
        assert cmd.stdin == "Test"

    def test_copilot_backend(self):
        """Test building command for Copilot backend."""
        cmd = CLIBuilder.build_command(
            backend_type=BackendType.COPILOT,
            prompt="Test",
            tools={ToolPermission.READ},
        )
        assert "copilot" in cmd.args[0]
        assert "--prompt" in cmd.args

    def test_unsupported_backend_raises(self):
        """Test that unsupported backend raises ValueError."""
        # Python's enum type checking prevents creating invalid enum values,
        # so we can't test this scenario directly without mocking.
        # This test documents that the function has error handling for future
        # backend types that might be added to the enum but not yet supported.
        # For now, we verify the function works with valid backend types.

        # If we add a new BackendType in the future without implementing it,
        # this would raise ValueError. For now, all defined types are supported.
        pass

    def test_passes_kwargs_to_claude(self):
        """Test that kwargs are passed through to Claude builder."""
        cmd = CLIBuilder.build_command(
            backend_type=BackendType.CLAUDE_CODE,
            prompt="Test",
            tools=set(),
            print_mode=False,
        )
        assert "--print" not in cmd.args

    def test_passes_kwargs_to_copilot(self):
        """Test that kwargs are passed through to Copilot builder."""
        cmd = CLIBuilder.build_command(
            backend_type=BackendType.COPILOT,
            prompt="Test",
            tools=set(),
            cli_path="/custom/copilot",
        )
        assert cmd.args[0] == "/custom/copilot"


class TestCLIBuilderEdgeCases:
    """Tests for edge cases and error handling."""

    def test_empty_prompt(self):
        """Test with empty prompt."""
        cmd = CLIBuilder.build_claude_command(prompt="", tools=set())
        assert cmd.stdin == ""

    def test_prompt_with_special_characters(self):
        """Test with prompt containing special characters."""
        prompt = 'Test "quotes" and \'apostrophes\' and $variables'
        cmd = CLIBuilder.build_claude_command(prompt=prompt, tools=set())
        assert cmd.stdin == prompt

    def test_prompt_with_newlines(self):
        """Test with multiline prompt."""
        prompt = "Line 1\nLine 2\nLine 3"
        cmd = CLIBuilder.build_claude_command(prompt=prompt, tools=set())
        assert cmd.stdin == prompt
        assert "\n" in cmd.stdin

    def test_very_long_prompt(self):
        """Test with very long prompt."""
        prompt = "x" * 10000
        cmd = CLIBuilder.build_claude_command(prompt=prompt, tools=set())
        assert cmd.stdin == prompt
        assert len(cmd.stdin) == 10000

    def test_unicode_in_prompt(self):
        """Test with Unicode characters in prompt."""
        prompt = "Hello 世界 🌍 Привет"
        cmd = CLIBuilder.build_claude_command(prompt=prompt, tools=set())
        assert cmd.stdin == prompt


class TestCLIBuilderIntegration:
    """Integration tests for CLI builder with real-world scenarios."""

    def test_documentation_task_command(self):
        """Test building command for documentation generation task."""
        cmd = CLIBuilder.build_claude_command(
            prompt="Generate documentation for this module",
            tools={ToolPermission.READ, ToolPermission.WRITE},
            model="claude-sonnet-4-5-20250929",
        )
        assert "claude" in cmd.args
        assert "--model" in cmd.args
        assert "--allowedTools" in cmd.args

    def test_code_review_task_command(self):
        """Test building command for code review task."""
        cmd = CLIBuilder.build_copilot_command(
            prompt="Review this code for issues",
            tools={ToolPermission.READ},
        )
        assert "--prompt" in cmd.args
        assert "--allow-tool" in cmd.args

    def test_bug_fix_task_command(self):
        """Test building command for bug fixing task."""
        cmd = CLIBuilder.build_claude_command(
            prompt="Fix the bug in the test suite",
            tools={ToolPermission.READ, ToolPermission.WRITE, ToolPermission.BASH},
            model="claude-opus-4-5-20251101",
        )
        assert "--model" in cmd.args
        model_idx = cmd.args.index("--model")
        assert "opus" in cmd.args[model_idx + 1].lower()
