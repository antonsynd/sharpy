"""
Unit tests for shared configuration base.

Tests path resolution, JSON serialization/deserialization, and directory creation.
"""

import json
import pytest
from dataclasses import dataclass, field
from pathlib import Path
from tempfile import TemporaryDirectory

from build_tools.shared.config import BaseConfig


class TestBaseConfig:
    """Test BaseConfig base class functionality."""

    def test_default_project_root_is_cwd(self):
        """BaseConfig should default to current working directory."""
        config = BaseConfig()
        assert config.project_root == Path.cwd()

    def test_custom_project_root(self):
        """BaseConfig should accept custom project root."""
        custom_root = Path("/custom/path")
        config = BaseConfig(project_root=custom_root)
        assert config.project_root == custom_root

    def test_build_tools_dir_property(self):
        """build_tools_dir should be project_root/build_tools."""
        config = BaseConfig(project_root=Path("/project"))
        assert config.build_tools_dir == Path("/project/build_tools")

    def test_docs_dir_property(self):
        """docs_dir should be project_root/docs."""
        config = BaseConfig(project_root=Path("/project"))
        assert config.docs_dir == Path("/project/docs")

    def test_src_dir_property(self):
        """src_dir should be project_root/src."""
        config = BaseConfig(project_root=Path("/project"))
        assert config.src_dir == Path("/project/src")

    def test_ensure_directories_creates_dirs(self):
        """ensure_directories should create all standard directories."""
        with TemporaryDirectory() as tmpdir:
            root = Path(tmpdir) / "test_project"
            config = BaseConfig(project_root=root)

            # Directories shouldn't exist yet
            assert not config.build_tools_dir.exists()
            assert not config.docs_dir.exists()
            assert not config.src_dir.exists()

            # Create them
            config.ensure_directories()

            # Now they should exist
            assert config.build_tools_dir.exists()
            assert config.docs_dir.exists()
            assert config.src_dir.exists()

    def test_ensure_directories_idempotent(self):
        """ensure_directories should be safe to call multiple times."""
        with TemporaryDirectory() as tmpdir:
            root = Path(tmpdir) / "test_project"
            config = BaseConfig(project_root=root)

            # Call multiple times - should not raise
            config.ensure_directories()
            config.ensure_directories()
            config.ensure_directories()

            # Directories should still exist
            assert config.build_tools_dir.exists()

    def test_to_dict_converts_paths(self):
        """to_dict should convert Path objects to strings."""
        config = BaseConfig(project_root=Path("/project"))
        data = config.to_dict()

        assert isinstance(data["project_root"], str)
        assert data["project_root"] == "/project"

    def test_to_dict_excludes_properties(self):
        """to_dict should only include fields, not @property methods."""
        config = BaseConfig(project_root=Path("/project"))
        data = config.to_dict()

        # Should have project_root field
        assert "project_root" in data

        # Should NOT have property-based paths (they're computed)
        assert "build_tools_dir" not in data
        assert "docs_dir" not in data
        assert "src_dir" not in data

    def test_from_dict_converts_strings_to_paths(self):
        """from_dict should convert string paths back to Path objects."""
        data = {"project_root": "/project"}
        config = BaseConfig.from_dict(data)

        assert isinstance(config.project_root, Path)
        assert config.project_root == Path("/project")

    def test_from_dict_ignores_unknown_fields(self):
        """from_dict should ignore fields not defined in dataclass."""
        data = {
            "project_root": "/project",
            "unknown_field": "should be ignored",
            "another_unknown": 123,
        }
        config = BaseConfig.from_dict(data)

        # Should construct successfully without unknown fields
        assert config.project_root == Path("/project")
        assert not hasattr(config, "unknown_field")

    def test_save_creates_parent_directories(self):
        """save should create parent directories if they don't exist."""
        with TemporaryDirectory() as tmpdir:
            config_path = Path(tmpdir) / "subdir" / "config.json"
            config = BaseConfig(project_root=Path("/project"))

            # Parent directory doesn't exist yet
            assert not config_path.parent.exists()

            # Save should create it
            config.save(config_path)

            assert config_path.exists()
            assert config_path.parent.exists()

    def test_save_writes_valid_json(self):
        """save should write properly formatted JSON."""
        with TemporaryDirectory() as tmpdir:
            config_path = Path(tmpdir) / "config.json"
            config = BaseConfig(project_root=Path("/project"))
            config.save(config_path)

            # Should be valid JSON
            with open(config_path) as f:
                data = json.load(f)

            assert data["project_root"] == "/project"

    def test_load_reads_configuration(self):
        """load should read configuration from JSON file."""
        with TemporaryDirectory() as tmpdir:
            config_path = Path(tmpdir) / "config.json"

            # Write a config file manually
            with open(config_path, "w") as f:
                json.dump({"project_root": "/loaded/path"}, f)

            # Load it
            config = BaseConfig.load(config_path)

            assert config.project_root == Path("/loaded/path")

    def test_save_and_load_roundtrip(self):
        """Configuration should survive save/load roundtrip."""
        with TemporaryDirectory() as tmpdir:
            config_path = Path(tmpdir) / "config.json"
            original = BaseConfig(project_root=Path("/roundtrip/test"))

            # Save and load
            original.save(config_path)
            loaded = BaseConfig.load(config_path)

            assert loaded.project_root == original.project_root

    def test_load_nonexistent_file_raises(self):
        """load should raise FileNotFoundError for missing files."""
        nonexistent = Path("/nonexistent/config.json")
        with pytest.raises(FileNotFoundError):
            BaseConfig.load(nonexistent)

    def test_load_invalid_json_raises(self):
        """load should raise JSONDecodeError for invalid JSON."""
        with TemporaryDirectory() as tmpdir:
            config_path = Path(tmpdir) / "invalid.json"
            with open(config_path, "w") as f:
                f.write("{ invalid json }")

            with pytest.raises(json.JSONDecodeError):
                BaseConfig.load(config_path)


class TestBaseConfigInheritance:
    """Test that BaseConfig works as a base class."""

    def test_subclass_extends_base_config(self):
        """Subclasses should inherit BaseConfig functionality."""

        @dataclass
        class ToolConfig(BaseConfig):
            tool_option: str = "default"
            max_iterations: int = 10

        config = ToolConfig(project_root=Path("/tool"))

        # Should have base properties
        assert config.build_tools_dir == Path("/tool/build_tools")

        # Should have subclass fields
        assert config.tool_option == "default"
        assert config.max_iterations == 10

    def test_subclass_to_dict_includes_all_fields(self):
        """Subclass to_dict should include both base and subclass fields."""

        @dataclass
        class ToolConfig(BaseConfig):
            tool_option: str = "value"

        config = ToolConfig(project_root=Path("/tool"))
        data = config.to_dict()

        assert "project_root" in data
        assert "tool_option" in data
        assert data["tool_option"] == "value"

    def test_subclass_from_dict_constructs_correctly(self):
        """Subclass from_dict should populate all fields."""

        @dataclass
        class ToolConfig(BaseConfig):
            tool_option: str = "default"

        data = {"project_root": "/tool", "tool_option": "custom"}
        config = ToolConfig.from_dict(data)

        assert config.project_root == Path("/tool")
        assert config.tool_option == "custom"

    def test_subclass_save_and_load_roundtrip(self):
        """Subclass configuration should survive save/load roundtrip."""

        @dataclass
        class ToolConfig(BaseConfig):
            tool_option: str = "value"
            count: int = 42

        with TemporaryDirectory() as tmpdir:
            config_path = Path(tmpdir) / "tool_config.json"
            original = ToolConfig(
                project_root=Path("/roundtrip"), tool_option="custom", count=100
            )

            # Save and load
            original.save(config_path)
            loaded = ToolConfig.load(config_path)

            assert loaded.project_root == original.project_root
            assert loaded.tool_option == original.tool_option
            assert loaded.count == original.count

    def test_subclass_can_override_ensure_directories(self):
        """Subclasses can override ensure_directories to add custom dirs."""

        @dataclass
        class ToolConfig(BaseConfig):
            output_dir: Path = field(default_factory=lambda: Path("output"))

            def ensure_directories(self) -> None:
                super().ensure_directories()
                full_output = self.project_root / self.output_dir
                full_output.mkdir(parents=True, exist_ok=True)

        with TemporaryDirectory() as tmpdir:
            root = Path(tmpdir) / "test_project"
            config = ToolConfig(project_root=root, output_dir=Path("custom_output"))

            config.ensure_directories()

            # Base directories should exist
            assert config.build_tools_dir.exists()

            # Custom directory should also exist
            assert (root / "custom_output").exists()

    def test_subclass_with_nested_dataclass(self):
        """Subclass can include nested dataclasses in serialization."""

        @dataclass
        class NestedOptions:
            option_a: str = "a"
            option_b: int = 1

        @dataclass
        class ToolConfig(BaseConfig):
            nested: NestedOptions = field(default_factory=NestedOptions)

        config = ToolConfig(
            project_root=Path("/tool"),
            nested=NestedOptions(option_a="custom", option_b=42),
        )

        # Should serialize nested dataclass
        data = config.to_dict()
        assert "nested" in data
        assert data["nested"]["option_a"] == "custom"
        assert data["nested"]["option_b"] == 42

    def test_subclass_with_path_list(self):
        """Subclass with list of Path objects should serialize correctly."""

        @dataclass
        class ToolConfig(BaseConfig):
            search_paths: list[Path] = field(default_factory=list)

        config = ToolConfig(
            project_root=Path("/tool"), search_paths=[Path("/path1"), Path("/path2")]
        )

        # Should convert all paths to strings
        data = config.to_dict()
        assert data["search_paths"] == ["/path1", "/path2"]

    def test_subclass_with_path_dict(self):
        """Subclass with dict containing Path values should serialize correctly."""

        @dataclass
        class ToolConfig(BaseConfig):
            mappings: dict[str, Path] = field(default_factory=dict)

        config = ToolConfig(
            project_root=Path("/tool"),
            mappings={"key1": Path("/value1"), "key2": Path("/value2")},
        )

        # Should convert path values to strings
        data = config.to_dict()
        assert data["mappings"] == {"key1": "/value1", "key2": "/value2"}


if __name__ == "__main__":
    pytest.main([__file__, "-v"])
