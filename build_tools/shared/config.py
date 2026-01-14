"""
Shared configuration base for build tools.

Provides common path resolution, JSON serialization, and directory management
utilities that can be extended by tool-specific configurations.
"""

from dataclasses import dataclass, field, asdict, fields
from pathlib import Path
from typing import TypeVar, Any
import json


T = TypeVar('T', bound='BaseConfig')


@dataclass
class BaseConfig:
    """
    Base configuration with common paths and settings.

    Provides standardized access to project directories and utilities for
    JSON serialization/deserialization. Designed to be extended by tool-specific
    configuration classes.

    Example:
        @dataclass
        class MyToolConfig(BaseConfig):
            tool_specific_option: str = "default"
            max_iterations: int = 10

        config = MyToolConfig(project_root=Path("/path/to/project"))
        config.ensure_directories()
        config.save(Path("config.json"))
    """

    project_root: Path = field(default_factory=lambda: Path.cwd())

    @property
    def build_tools_dir(self) -> Path:
        """Path to the build_tools directory."""
        return self.project_root / "build_tools"

    @property
    def docs_dir(self) -> Path:
        """Path to the docs directory."""
        return self.project_root / "docs"

    @property
    def src_dir(self) -> Path:
        """Path to the src directory."""
        return self.project_root / "src"

    def ensure_directories(self) -> None:
        """
        Create required directories if they don't exist.

        Creates the following directories with parents as needed:
        - build_tools_dir
        - docs_dir
        - src_dir

        Subclasses can override to add additional directories.
        """
        self.build_tools_dir.mkdir(parents=True, exist_ok=True)
        self.docs_dir.mkdir(parents=True, exist_ok=True)
        self.src_dir.mkdir(parents=True, exist_ok=True)

    def to_dict(self) -> dict[str, Any]:
        """
        Serialize configuration to dictionary.

        Handles Path objects by converting them to strings.
        Recursively processes nested dataclasses.

        Returns:
            Dictionary representation suitable for JSON serialization.
        """
        def convert_value(value: Any) -> Any:
            """Convert value to JSON-serializable format."""
            if isinstance(value, Path):
                return str(value)
            elif isinstance(value, dict):
                return {k: convert_value(v) for k, v in value.items()}
            elif isinstance(value, (list, tuple)):
                return [convert_value(item) for item in value]
            elif hasattr(value, '__dataclass_fields__'):
                # Nested dataclass
                return {
                    f.name: convert_value(getattr(value, f.name))
                    for f in fields(value)
                }
            else:
                return value

        return {
            field.name: convert_value(getattr(self, field.name))
            for field in fields(self)
        }

    @classmethod
    def from_dict(cls: type[T], data: dict[str, Any]) -> T:
        """
        Deserialize configuration from dictionary.

        Handles Path objects by converting strings back to Path instances.
        Filters out properties and unknown fields.

        Args:
            data: Dictionary containing configuration values.

        Returns:
            New instance of the configuration class.

        Raises:
            TypeError: If required fields are missing or have incorrect types.
        """
        # Get field names and types from the dataclass
        field_info = {f.name: f.type for f in fields(cls)}

        # Filter to only known fields (excludes @property methods)
        filtered_data = {}
        for key, value in data.items():
            if key not in field_info:
                continue  # Skip unknown fields

            # Convert string paths back to Path objects
            field_type = field_info[key]
            if field_type == Path or (hasattr(field_type, '__origin__') and Path in str(field_type)):
                if isinstance(value, str):
                    filtered_data[key] = Path(value)
                elif isinstance(value, Path):
                    filtered_data[key] = value
                else:
                    filtered_data[key] = value
            else:
                filtered_data[key] = value

        return cls(**filtered_data)

    def save(self, path: Path) -> None:
        """
        Save configuration to JSON file.

        Creates parent directories if they don't exist.
        Writes formatted JSON with 2-space indentation.

        Args:
            path: Path to the output JSON file.

        Raises:
            OSError: If file cannot be written.
        """
        path.parent.mkdir(parents=True, exist_ok=True)
        with open(path, 'w', encoding='utf-8') as f:
            json.dump(self.to_dict(), f, indent=2)

    @classmethod
    def load(cls: type[T], path: Path) -> T:
        """
        Load configuration from JSON file.

        Args:
            path: Path to the JSON configuration file.

        Returns:
            New instance of the configuration class.

        Raises:
            FileNotFoundError: If the configuration file doesn't exist.
            json.JSONDecodeError: If the file contains invalid JSON.
        """
        with open(path, 'r', encoding='utf-8') as f:
            data = json.load(f)
        return cls.from_dict(data)


__all__ = ['BaseConfig']
