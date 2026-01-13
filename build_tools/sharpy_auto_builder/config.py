"""
Configuration for Sharpy Auto Builder.

Handles rate limiting, backend selection, and paths.
"""

from dataclasses import dataclass, field
from pathlib import Path
from typing import Literal, Optional
from datetime import timedelta
import json


BackendType = Literal["copilot", "claude_code"]


@dataclass
class RateLimitConfig:
    """Rate limiting configuration per backend."""

    # Maximum requests per time window
    max_requests_per_window: int = 50

    # Time window duration in seconds
    window_seconds: int = 3600  # 1 hour

    # Cooldown between requests in seconds
    request_cooldown: float = 2.0

    # Maximum consecutive errors before switching backends
    max_consecutive_errors: int = 3

    # Backoff multiplier for rate limit hits
    backoff_multiplier: float = 2.0

    # Maximum backoff in seconds
    max_backoff: float = 300.0


@dataclass
class BackendConfig:
    """Configuration for a specific backend."""

    name: BackendType
    enabled: bool = True
    rate_limit: RateLimitConfig = field(default_factory=RateLimitConfig)

    # Backend-specific settings
    model: str = "claude-sonnet-4-5-20250929"  # Default to Sonnet 4.5
    max_tokens: int = 16384

    # For Claude Code
    claude_code_path: Optional[str] = None

    # For GitHub Copilot CLI
    copilot_cli_path: Optional[str] = None


@dataclass
class Config:
    """Main configuration for Sharpy Auto Builder."""

    # Project paths
    project_root: Path = field(
        default_factory=lambda: Path("/Users/anton/Documents/github/sharpy")
    )

    # Task list path (required - no default)
    task_list_path: Optional[Path] = None

    @property
    def spec_dir(self) -> Path:
        return self.project_root / "docs/language_specification"

    @property
    def agents_dir(self) -> Path:
        return self.project_root / ".github/agents"

    @property
    def src_dir(self) -> Path:
        return self.project_root / "src"

    @property
    def build_tools_dir(self) -> Path:
        return self.project_root / "build_tools"

    # State and tracking
    @property
    def state_dir(self) -> Path:
        return self.build_tools_dir / "sharpy_auto_builder/state"

    @property
    def ground_truth_path(self) -> Path:
        return self.state_dir / "ground_truth.json"

    @property
    def execution_log_path(self) -> Path:
        return self.state_dir / "execution_log.jsonl"

    # Human-in-the-loop
    @property
    def questions_dir(self) -> Path:
        return self.state_dir / "questions"

    @property
    def answers_dir(self) -> Path:
        return self.state_dir / "answers"

    @property
    def human_review_dir(self) -> Path:
        return self.state_dir / "human_review"

    # Backend configurations
    backends: dict[BackendType, BackendConfig] = field(
        default_factory=lambda: {
            "claude_code": BackendConfig(
                name="claude_code",
                enabled=True,
                model="claude-sonnet-4-5-20250929",
                rate_limit=RateLimitConfig(
                    max_requests_per_window=50,
                    window_seconds=3600,
                    request_cooldown=3.0,
                ),
            ),
            "copilot": BackendConfig(
                name="copilot",
                enabled=False,  # Disabled by default - gh copilot CLI has limited non-interactive support
                rate_limit=RateLimitConfig(
                    max_requests_per_window=100,
                    window_seconds=3600,
                    request_cooldown=2.0,
                ),
            ),
        }
    )

    # Preferred backend order (for failover)
    backend_priority: list[BackendType] = field(
        default_factory=lambda: ["claude_code", "copilot"]
    )

    # Validation settings
    run_spec_adherence_check: bool = True
    run_verification_after_implementation: bool = True
    run_hallucination_defense: bool = True

    # Execution settings
    max_retries_per_task: int = 3
    max_test_fix_attempts: int = 3  # Max attempts to fix tests the agent broke
    max_validation_fix_attempts: int = 2  # Max attempts to address validation issues
    create_followup_task_on_fix_failure: bool = (
        True  # Create a task when agent can't fix tests
    )
    require_human_approval_for_critical: bool = True
    auto_commit: bool = False
    create_pr: bool = False
    rate_limit_pause_hours: float = (
        3.0  # Hours to pause when all backends are rate-limited
    )

    # Test execution settings
    test_timeout: float = (
        300.0  # 5 minutes default timeout for test execution (to catch infinite loops)
    )

    # Human-in-the-loop settings
    human_wait_timeout: float = 3600.0  # 1 hour default timeout for human responses
    human_check_interval: float = 5.0  # seconds between checks for human input

    def ensure_directories(self) -> None:
        """Create required directories if they don't exist."""
        for path in [
            self.state_dir,
            self.questions_dir,
            self.answers_dir,
            self.human_review_dir,
        ]:
            path.mkdir(parents=True, exist_ok=True)

    def to_dict(self) -> dict:
        """Convert config to dictionary for serialization."""
        return {
            "project_root": str(self.project_root),
            "task_list_path": str(self.task_list_path) if self.task_list_path else None,
            "backends": {
                name: {
                    "name": cfg.name,
                    "enabled": cfg.enabled,
                    "model": cfg.model,
                    "max_tokens": cfg.max_tokens,
                    "rate_limit": {
                        "max_requests_per_window": cfg.rate_limit.max_requests_per_window,
                        "window_seconds": cfg.rate_limit.window_seconds,
                        "request_cooldown": cfg.rate_limit.request_cooldown,
                    },
                }
                for name, cfg in self.backends.items()
            },
            "backend_priority": self.backend_priority,
            "run_spec_adherence_check": self.run_spec_adherence_check,
            "run_verification_after_implementation": self.run_verification_after_implementation,
            "run_hallucination_defense": self.run_hallucination_defense,
            "max_retries_per_task": self.max_retries_per_task,
            "max_test_fix_attempts": self.max_test_fix_attempts,
            "max_validation_fix_attempts": self.max_validation_fix_attempts,
            "create_followup_task_on_fix_failure": self.create_followup_task_on_fix_failure,
            "require_human_approval_for_critical": self.require_human_approval_for_critical,
            "auto_commit": self.auto_commit,
            "create_pr": self.create_pr,
            "rate_limit_pause_hours": self.rate_limit_pause_hours,
            "test_timeout": self.test_timeout,
            "human_wait_timeout": self.human_wait_timeout,
            "human_check_interval": self.human_check_interval,
        }

    @classmethod
    def from_dict(cls, data: dict) -> "Config":
        """Load config from dictionary."""
        config = cls()
        if "project_root" in data:
            config.project_root = Path(data["project_root"])
        if "task_list_path" in data and data["task_list_path"] is not None:
            config.task_list_path = Path(data["task_list_path"])
        if "backends" in data:
            for name, cfg_data in data["backends"].items():
                if name in config.backends:
                    config.backends[name].enabled = cfg_data.get("enabled", True)
                    config.backends[name].model = cfg_data.get(
                        "model", "claude-sonnet-4-5-20250929"
                    )
                    if "max_tokens" in cfg_data:
                        config.backends[name].max_tokens = cfg_data["max_tokens"]
                    if "claude_code_path" in cfg_data:
                        config.backends[name].claude_code_path = cfg_data[
                            "claude_code_path"
                        ]
                    if "copilot_cli_path" in cfg_data:
                        config.backends[name].copilot_cli_path = cfg_data[
                            "copilot_cli_path"
                        ]
                    # Load rate limit settings
                    if "rate_limit" in cfg_data:
                        rl_data = cfg_data["rate_limit"]
                        rl = config.backends[name].rate_limit
                        if "max_requests_per_window" in rl_data:
                            rl.max_requests_per_window = rl_data[
                                "max_requests_per_window"
                            ]
                        if "window_seconds" in rl_data:
                            rl.window_seconds = rl_data["window_seconds"]
                        if "request_cooldown" in rl_data:
                            rl.request_cooldown = rl_data["request_cooldown"]
                        if "max_consecutive_errors" in rl_data:
                            rl.max_consecutive_errors = rl_data[
                                "max_consecutive_errors"
                            ]
                        if "backoff_multiplier" in rl_data:
                            rl.backoff_multiplier = rl_data["backoff_multiplier"]
                        if "max_backoff" in rl_data:
                            rl.max_backoff = rl_data["max_backoff"]
        if "backend_priority" in data:
            config.backend_priority = data["backend_priority"]
        if "run_spec_adherence_check" in data:
            config.run_spec_adherence_check = data["run_spec_adherence_check"]
        if "run_verification_after_implementation" in data:
            config.run_verification_after_implementation = data[
                "run_verification_after_implementation"
            ]
        if "run_hallucination_defense" in data:
            config.run_hallucination_defense = data["run_hallucination_defense"]
        if "max_retries_per_task" in data:
            config.max_retries_per_task = data["max_retries_per_task"]
        if "max_test_fix_attempts" in data:
            config.max_test_fix_attempts = data["max_test_fix_attempts"]
        if "create_followup_task_on_fix_failure" in data:
            config.create_followup_task_on_fix_failure = data[
                "create_followup_task_on_fix_failure"
            ]
        if "require_human_approval_for_critical" in data:
            config.require_human_approval_for_critical = data[
                "require_human_approval_for_critical"
            ]
        if "auto_commit" in data:
            config.auto_commit = data["auto_commit"]
        if "create_pr" in data:
            config.create_pr = data["create_pr"]
        if "rate_limit_pause_hours" in data:
            config.rate_limit_pause_hours = data["rate_limit_pause_hours"]
        if "test_timeout" in data:
            config.test_timeout = data["test_timeout"]
        if "human_wait_timeout" in data:
            config.human_wait_timeout = data["human_wait_timeout"]
        if "human_check_interval" in data:
            config.human_check_interval = data["human_check_interval"]
        if "max_validation_fix_attempts" in data:
            config.max_validation_fix_attempts = data["max_validation_fix_attempts"]
        return config

    def save(self, path: Optional[Path] = None) -> None:
        """Save config to JSON file."""
        path = path or (self.state_dir / "config.json")
        path.parent.mkdir(parents=True, exist_ok=True)
        with open(path, "w") as f:
            json.dump(self.to_dict(), f, indent=2)

    @classmethod
    def load(cls, path: Path) -> "Config":
        """Load config from JSON file."""
        with open(path) as f:
            data = json.load(f)
        return cls.from_dict(data)


# Default configuration instance
DEFAULT_CONFIG = Config()
