"""
Abstract backend interface for AI model interactions.

Defines the core interfaces and types that all backend implementations
must follow, ensuring consistent behavior across different AI providers.
"""

from abc import ABC, abstractmethod
from dataclasses import dataclass, field
from enum import Enum
from typing import Optional


class BackendType(Enum):
    """Supported AI backend types."""

    CLAUDE_CODE = "claude_code"
    COPILOT = "copilot"
    # Future: CLAUDE_API = "claude_api"  # Direct API for smaller models


class ToolPermission(Enum):
    """Tool permissions that can be granted to AI backends.
    
    These map to the tools that the AI can use during execution,
    such as reading files, editing code, running bash commands, etc.
    """

    READ = "Read"
    WRITE = "Write"
    EDIT = "Edit"
    BASH = "Bash"
    GLOB = "Glob"
    GREP = "Grep"


@dataclass
class BackendConfig:
    """Configuration for a backend execution request.
    
    Attributes:
        timeout_seconds: Maximum execution time before termination
        allowed_tools: Set of tool permissions granted for this request
        model: Specific model to use (if backend supports selection)
        max_retries: Maximum number of retry attempts on transient failures
    """

    timeout_seconds: int = 300
    allowed_tools: set[ToolPermission] = field(
        default_factory=lambda: {ToolPermission.READ}
    )
    model: Optional[str] = None  # For backends that support model selection
    max_retries: int = 3


@dataclass
class BackendResponse:
    """Standardized response from any backend.
    
    All backends return this type to enable consistent error handling
    and response processing regardless of the underlying implementation.
    
    Attributes:
        success: Whether the execution completed successfully
        output: Standard output from the execution
        stderr: Standard error output (if any)
        exit_code: Process exit code (0 = success)
        duration_seconds: Execution time in seconds
        rate_limited: Whether this request was rate limited
        error_message: Human-readable error description (if any)
    """

    success: bool
    output: str
    stderr: str = ""
    exit_code: int = 0
    duration_seconds: float = 0.0
    rate_limited: bool = False
    error_message: Optional[str] = None


class Backend(ABC):
    """Abstract base class for AI backends.
    
    All backend implementations (Claude Code, Copilot, etc.) must
    inherit from this class and implement its abstract methods.
    
    This ensures a consistent interface for executing prompts,
    checking availability, and handling responses.
    """

    @property
    @abstractmethod
    def backend_type(self) -> BackendType:
        """Get the type identifier for this backend.
        
        Returns:
            BackendType enum value identifying this backend
        """
        ...

    @abstractmethod
    async def execute(
        self, prompt: str, config: Optional[BackendConfig] = None
    ) -> BackendResponse:
        """Execute a prompt via this backend.
        
        Args:
            prompt: The prompt/instruction to send to the AI
            config: Optional configuration for this execution
            
        Returns:
            BackendResponse with execution results
            
        Raises:
            May raise exceptions for unrecoverable errors (connection issues, etc.)
        """
        ...

    @abstractmethod
    def is_available(self) -> bool:
        """Check if this backend is currently available.
        
        Returns:
            True if the backend can accept requests, False otherwise
            
        Note:
            This should check both installation status and rate limit state.
        """
        ...
