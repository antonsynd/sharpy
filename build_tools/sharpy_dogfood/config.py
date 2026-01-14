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


BackendType = Literal["copilot", "claude"]


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
    generated_dir: Path = field(
        default_factory=lambda: Path("dogfood_output/generated")
    )

    # Execution limits
    max_iterations: int = 10
    generation_timeout: float = 180.0  # 3 minutes for code generation
    compilation_timeout: float = 60.0  # 1 minute for compilation
    execution_timeout: float = 30.0  # 30 seconds for running generated code

    # Backend configuration
    backends: dict[BackendType, BackendConfig] = field(
        default_factory=lambda: {
            "claude": BackendConfig(name="claude"),
            "copilot": BackendConfig(name="copilot"),
        }
    )

    # Feature coverage phases (0.1.0 to 0.1.5)
    supported_phases: list[str] = field(
        default_factory=lambda: [
            "0.1.0",  # Lexer Foundation
            "0.1.1",  # Parser Foundation
            "0.1.2",  # Code Generation Bootstrap
            "0.1.3",  # Variables & Expressions
            "0.1.4",  # Control Flow
            "0.1.5",  # Functions
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
            / "docs/implementation_planning/task_list_0.1.0_to_0.1.5.md"
        )

    @property
    def sharpy_cli_project(self) -> Path:
        return self.project_root / "src/Sharpy.Cli"

    @property
    def snippets_dir(self) -> Path:
        return self.project_root / "snippets"

    def ensure_dirs(self) -> None:
        """Create output directories if they don't exist.

        Extends parent class to also create dogfood-specific output directories.
        """
        # First ensure parent directories via BaseConfig
        super().ensure_directories()

        # Make paths absolute relative to project root
        self.output_dir = self.project_root / self.output_dir
        self.issues_dir = self.project_root / self.issues_dir
        self.generated_dir = self.project_root / self.generated_dir

        self.output_dir.mkdir(parents=True, exist_ok=True)
        self.issues_dir.mkdir(parents=True, exist_ok=True)
        self.generated_dir.mkdir(parents=True, exist_ok=True)

    @classmethod
    def from_file(cls, path: Path) -> "Config":
        """Load configuration from a JSON file.

        This method is kept for backward compatibility with existing code
        that calls Config.from_file(). Internally uses BaseConfig.load().
        """
        return cls.load(path)
