import sys
from pathlib import Path

src_directory: Path = Path(__file__).parent.parent / "src"
sys.path.append(str(src_directory))
