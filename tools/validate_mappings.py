#!/usr/bin/env python3
"""Static checks for JA ARPA mapping coverage. No third-party packages required."""
from pathlib import Path
import collections
import re

root = Path(__file__).resolve().parents[1]
kana_text = (root / "src" / "KanaMap.cs").read_text(encoding="utf-8")
g2p_text = (root / "src" / "JapaneseArpaG2p.cs").read_text(encoding="utf-8")

entry_matches = re.findall(r'\{\s*"([^"]+)",\s*new\[\]\s*\{([^}]*)\}\s*\}', kana_text)
entries = {
    key: re.findall(r'"([^"]+)"', phones)
    for key, phones in entry_matches
}
if len(entries) != len(entry_matches):
    raise SystemExit("Duplicate KanaMap key detected")

required = {
    # Basic and yoon.
    "あ", "い", "う", "え", "お", "か", "し", "つ", "ふ", "ら",
    "きゃ", "ぎゃ", "しゃ", "じゃ", "ちゃ", "ぢゃ", "にゃ", "ひゃ",
    "びゃ", "ぴゃ", "みゃ", "りゃ",
    # Extended / loanword morae requested for regression coverage.
    "いぇ", "うぃ", "うぇ", "うぉ", "きぇ", "ぎぇ",
    "くぁ", "くぃ", "くぇ", "くぉ", "くゎ",
    "ぐぁ", "ぐぃ", "ぐぇ", "ぐぉ", "ぐゎ",
    "しぇ", "じぇ", "ちぇ", "ぢぇ", "すぃ", "ずぃ",
    "つぁ", "つぃ", "つゅ", "つぇ", "つぉ",
    "てゃ", "てぃ", "てゅ", "てぇ", "てょ",
    "でゃ", "でぃ", "でゅ", "でぇ", "でょ",
    "とぁ", "とぃ", "とぅ", "とぇ", "とぉ",
    "どぁ", "どぃ", "どぅ", "どぇ", "どぉ",
    "にぇ", "ひぇ", "びぇ", "ぴぇ", "みぇ", "りぇ",
    "ふぁ", "ふぃ", "ふぇ", "ふぉ", "ふゃ", "ふゅ", "ふょ",
    "ゔ", "ゔぁ", "ゔぃ", "ゔぇ", "ゔぉ", "ゔゃ", "ゔゅ", "ゔょ",
}
missing = sorted(required - entries.keys())
if missing:
    raise SystemExit("Missing required mappings: " + ", ".join(missing))

valid_symbols = {
    "aa", "ae", "ah", "ao", "aw", "ax", "ay", "b", "ch", "d", "dh",
    "dr", "dx", "eh", "er", "ey", "f", "g", "hh", "ih", "iy", "jh",
    "k", "l", "m", "n", "ng", "ow", "oy", "p", "q", "r", "s", "sh",
    "t", "th", "tr", "uh", "uw", "v", "w", "y", "z", "zh", "-",
}
invalid = [(k, p) for k, phones in entries.items() for p in phones if p not in valid_symbols]
if invalid:
    raise SystemExit("Invalid ARPAbet symbols: " + repr(invalid))

# Make sure the canonical moraic nasal behavior is present in source.
if 'return "n";' not in g2p_text or "canonical ARPAbet /n/" not in g2p_text:
    raise SystemExit("Canonical ん -> n implementation not found")

# Romaji dictionary must not contain duplicate keys.
romaji_section = g2p_text[g2p_text.index("RomajiToKana"):g2p_text.index("private static readonly string[] RomajiKeys")]
romaji_keys = re.findall(r'\{\s*"([^"]+)",\s*"[^"]+"\s*\}', romaji_section)
duplicates = sorted(k for k, count in collections.Counter(romaji_keys).items() if count > 1)
if duplicates:
    raise SystemExit("Duplicate romaji keys: " + ", ".join(duplicates))

print(f"Kana mappings: {len(entries)}")
print(f"Required extended morae: {len(required)} / {len(required)}")
print(f"Romaji mappings: {len(romaji_keys)}")
print("ARPAbet symbol validation: PASS")
print("Canonical ん -> n static check: PASS")
print("Static mapping check passed.")
