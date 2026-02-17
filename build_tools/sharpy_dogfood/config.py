"""
Configuration for Sharpy Dogfooding Tool.

Handles paths, timeouts, and backend configuration.
Extends the shared BaseConfig for common path handling and serialization.
"""

from dataclasses import dataclass, field
from pathlib import Path
from typing import Literal, Optional, Any
import json

# Import shared configuration base
from build_tools.shared.config import BaseConfig


BackendType = Literal["copilot", "claude", "klaude"]


@dataclass
class RateLimitConfig:
    """Rate limiting configuration per backend."""

    max_requests_per_window: int = 50
    window_seconds: int = 3600  # 1 hour
    request_cooldown: float = 2.0
    max_consecutive_errors: int = 3
    backoff_multiplier: float = 2.0
    max_backoff: float = 300.0


@dataclass
class BackendConfig:
    """Configuration for a specific AI backend."""

    name: BackendType
    enabled: bool = True
    rate_limit: RateLimitConfig = field(default_factory=RateLimitConfig)
    execution_timeout: float = 300.0  # 5 minutes default

    # CLI paths (auto-detected if None)
    claude_cli_path: Optional[str] = None
    copilot_cli_path: Optional[str] = None

    # Model to use for this backend (if None, uses backend default)
    model: Optional[str] = None


# Default models for dogfooding
CLAUDE_CLI_MODEL = "claude-sonnet-4-5-20250929"  # Full model name for Claude CLI
COPILOT_CLI_MODEL = "claude-sonnet-4.5"  # Copilot CLI model format


@dataclass
class Config(BaseConfig):
    """Main configuration for the dogfooding tool.

    Extends BaseConfig to inherit:
    - Common path handling (project_root, build_tools_dir, docs_dir, src_dir)
    - JSON serialization/deserialization (to_dict, from_dict, save, load)
    - Directory creation (ensure_directories)

    Adds dogfood-specific configuration for code generation, validation,
    and backend management.
    """

    # Override project_root with dogfood-specific default
    project_root: Path = field(
        default_factory=lambda: Path(__file__).parent.parent.parent.resolve()
    )

    # Output configuration
    output_dir: Path = field(default_factory=lambda: Path("dogfood_output"))
    issues_dir: Path = field(default_factory=lambda: Path("dogfood_output/issues"))
    successes_dir: Path = field(
        default_factory=lambda: Path("dogfood_output/successes")
    )
    skips_dir: Path = field(default_factory=lambda: Path("dogfood_output/skips"))

    # Execution limits
    max_iterations: int = 10
    max_regeneration_attempts: int = 3  # Max attempts to regenerate code with feedback
    generation_timeout: float = 180.0  # 3 minutes for code generation
    compilation_timeout: float = 60.0  # 1 minute for compilation
    execution_timeout: float = 30.0  # 30 seconds for running generated code

    # Backend configuration
    backends: dict[BackendType, BackendConfig] = field(
        default_factory=lambda: {
            "claude": BackendConfig(name="claude", model=CLAUDE_CLI_MODEL),
            "klaude": BackendConfig(
                name="klaude", enabled=False, model=CLAUDE_CLI_MODEL
            ),
            "copilot": BackendConfig(name="copilot", model=COPILOT_CLI_MODEL),
        }
    )

    # Feature coverage phases (0.1.0 to 0.1.18)
    supported_phases: list[str] = field(
        default_factory=lambda: [
            "0.1.0",  # Lexer Foundation
            "0.1.1",  # Parser Foundation
            "0.1.2",  # Code Generation Bootstrap
            "0.1.3",  # Variables & Expressions
            "0.1.4",  # Control Flow
            "0.1.5",  # Functions
            "0.1.6",  # Classes
            "0.1.7",  # Inheritance & Interfaces
            "0.1.8",  # Structs & Enums
            "0.1.9",  # Type System Enhancements
            "0.1.10",  # Module System
            "0.1.11",  # F-Strings & Collections
            "0.1.12",  # .NET Interop
            "0.1.13",  # Exception Handling
            "0.1.14",  # Lambda Expressions
            "0.1.15",  # Optional Types
            "0.1.16",  # Result Types
            "0.1.17",  # Maybe Expression
            "0.1.18",  # Try Expression
        ]
    )

    @property
    def spec_dir(self) -> Path:
        return self.project_root / "docs/language_specification"

    @property
    def phases_file(self) -> Path:
        return self.project_root / "docs/implementation_planning/phases.md"

    @property
    def task_list_file(self) -> Path:
        return (
            self.project_root
            / "docs/implementation_planning/task_list_0.1.0_to_0.1.10.md"
        )

    @property
    def sharpy_cli_project(self) -> Path:
        return self.project_root / "src/Sharpy.Cli"

    @property
    def snippets_dir(self) -> Path:
        return self.project_root / "snippets"

    @property
    def test_fixtures_dir(self) -> Path:
        return self.project_root / "src/Sharpy.Compiler.Tests/Integration/TestFixtures"

    def ensure_dirs(self) -> None:
        """Create output directories if they don't exist.

        Extends parent class to also create dogfood-specific output directories.
        """
        # First ensure parent directories via BaseConfig
        super().ensure_directories()

        # Make paths absolute relative to project root
        self.output_dir = self.project_root / self.output_dir
        self.issues_dir = self.project_root / self.issues_dir
        self.successes_dir = self.project_root / self.successes_dir
        self.skips_dir = self.project_root / self.skips_dir

        self.output_dir.mkdir(parents=True, exist_ok=True)
        self.issues_dir.mkdir(parents=True, exist_ok=True)
        self.successes_dir.mkdir(parents=True, exist_ok=True)
        self.skips_dir.mkdir(parents=True, exist_ok=True)

    @classmethod
    def from_file(cls, path: Path) -> "Config":
        """Load configuration from a JSON file.

        This method is kept for backward compatibility with existing code
        that calls Config.from_file(). Internally uses BaseConfig.load().
        """
        return cls.load(path)
