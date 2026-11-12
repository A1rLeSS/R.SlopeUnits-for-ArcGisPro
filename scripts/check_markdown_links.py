"""Check relative Markdown links for files that are missing from the checkout."""

from pathlib import Path
import re
from urllib.parse import unquote, urlsplit

ROOT = Path(__file__).resolve().parents[1]
LINK = re.compile(r"!?\[[^\]]*\]\((?:<([^>]+)>|([^\s)]+))(?:\s+[^)]*)?\)")
SKIP_DIRS = {".git", "artifacts", "bin", "obj", "runtime", "__pycache__"}


def markdown_files():
    for path in ROOT.rglob("*.md"):
        if not any(part in SKIP_DIRS for part in path.relative_to(ROOT).parts):
            yield path


def main():
    errors = []
    for source in markdown_files():
        content = source.read_text(encoding="utf-8-sig")
        for match in LINK.finditer(content):
            target = unquote(match.group(1) or match.group(2)).strip()
            parsed = urlsplit(target)
            if parsed.scheme or target.startswith("#") or not parsed.path:
                continue
            candidate = (source.parent / parsed.path).resolve()
            try:
                candidate.relative_to(ROOT.resolve())
            except ValueError:
                errors.append(f"{source.relative_to(ROOT)}: link escapes repository: {target}")
                continue
            if not candidate.exists():
                errors.append(f"{source.relative_to(ROOT)}: missing link target: {target}")

    if errors:
        print("\n".join(errors))
        return 1
    print("All local Markdown links point to existing files.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
