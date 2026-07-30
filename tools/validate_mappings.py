#!/usr/bin/env python3
"""Static checks for source mapping coverage. No third-party packages required."""
from pathlib import Path
import re

root = Path(__file__).resolve().parents[1]
text = (root / "src" / "KanaMap.cs").read_text(encoding="utf-8")
keys = set(re.findall(r'\{ "([^"]+)", new\[\]', text))
required = {
    "あ", "い", "う", "え", "お", "か", "し", "つ", "ふ", "ら",
    "きゃ", "しゃ", "ちゃ", "にゃ", "ふぁ", "てぃ", "ゔぁ"
}
missing = sorted(required - keys)
if missing:
    raise SystemExit("Missing required mappings: " + ", ".join(missing))
print(f"Kana mappings: {len(keys)}")
print("Static mapping check passed.")
