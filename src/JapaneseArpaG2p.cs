using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using OpenUtau.Api;

namespace JapaneseArpasingPhonemizer {
    internal sealed class JapaneseArpaG2p : IG2p {
        private static readonly HashSet<string> Vowels = new HashSet<string> {
            "aa", "ae", "ah", "ao", "aw", "ax", "ay", "eh", "er", "ey",
            "ih", "iy", "ow", "oy", "uh", "uw"
        };

        private static readonly HashSet<string> Glides = new HashSet<string> {
            "l", "r", "w", "y"
        };

        private static readonly HashSet<string> Symbols = new HashSet<string> {
            "-", "aa", "ae", "ah", "ao", "aw", "ax", "ay", "b", "ch", "d",
            "dh", "dr", "dx", "eh", "er", "ey", "f", "g", "hh", "ih", "iy",
            "jh", "k", "l", "m", "n", "ng", "ow", "oy", "p", "q", "r", "s",
            "sh", "t", "th", "tr", "uh", "uw", "v", "w", "y", "z", "zh"
        };

        public bool IsValidSymbol(string symbol) => Symbols.Contains(symbol);
        public bool IsVowel(string symbol) => Vowels.Contains(symbol);
        public bool IsGlide(string symbol) => Glides.Contains(symbol);

        public string[] Query(string grapheme) {
            return KanaConverter.Resolve(grapheme, null, null);
        }

        public string[] UnpackHint(string hint, char separator = ' ') {
            if (string.IsNullOrWhiteSpace(hint)) {
                return Array.Empty<string>();
            }
            return hint.Split(new[] { separator, ' ', '\t', ',', '/' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(symbol => symbol.Trim().ToLowerInvariant())
                .Where(IsValidSymbol)
                .ToArray();
        }

        public static bool IsVowelSymbol(string symbol) => Vowels.Contains(symbol);
    }

    internal static class KanaConverter {
        private static readonly char[] IgnoredCharacters = {
            ' ', '\t', '\r', '\n', '、', '。', '，', '．', ',', '.', '！', '!', '？', '?',
            '「', '」', '『', '』', '（', '）', '(', ')', '・', '…', '〜', '~'
        };

        public static string[] Resolve(string lyric, string nextLyric, string previousVowel) {
            if (string.IsNullOrWhiteSpace(lyric)) {
                return Array.Empty<string>();
            }

            string normalized = NormalizeKana(lyric.Trim());
            var direct = ParseDirectSymbols(normalized);
            if (direct.Length > 0) {
                return direct;
            }

            var tokens = Tokenize(normalized);
            if (tokens.Count == 0) {
                return Array.Empty<string>();
            }

            var result = new List<string>();
            string lastVowel = previousVowel;
            for (int i = 0; i < tokens.Count; ++i) {
                string token = tokens[i];
                string[] nextPhones = GetFollowingPhones(tokens, i + 1, nextLyric);

                if (token == "ん") {
                    result.Add(ResolveNasal(nextPhones.FirstOrDefault()));
                    continue;
                }
                if (token == "っ") {
                    result.Add(ResolveSokuon(nextPhones));
                    continue;
                }
                if (token == "ー") {
                    result.Add(string.IsNullOrEmpty(lastVowel) ? "uw" : lastVowel);
                    continue;
                }
                if (KanaMap.Entries.TryGetValue(token, out var phones)) {
                    result.AddRange(phones);
                    string vowel = LastVowel(phones);
                    if (!string.IsNullOrEmpty(vowel)) {
                        lastVowel = vowel;
                    }
                }
            }

            // A sokuon in a multi-character lyric may create k,k or ch,ch.
            // Keep only one adjacent copy. Separate-note sokuon timing is handled by the phonemizer.
            var collapsed = new List<string>();
            foreach (string phone in result) {
                if (collapsed.Count > 0 && collapsed[collapsed.Count - 1] == phone &&
                    !JapaneseArpaG2p.IsVowelSymbol(phone)) {
                    continue;
                }
                collapsed.Add(phone);
            }
            return collapsed.ToArray();
        }

        public static bool IsSokuon(string lyric) {
            return NormalizeKana(lyric ?? string.Empty).Trim() == "っ";
        }

        public static bool IsSyllabicNasal(string lyric) {
            return NormalizeKana(lyric ?? string.Empty).Trim() == "ん";
        }

        public static string ResolveSokuonForNext(string nextLyric) {
            return ResolveSokuon(GetInitialPhones(nextLyric));
        }

        public static string LastVowel(IEnumerable<string> phones) {
            if (phones == null) {
                return null;
            }
            string result = null;
            foreach (string phone in phones) {
                if (JapaneseArpaG2p.IsVowelSymbol(phone)) {
                    result = phone;
                }
            }
            return result;
        }

        private static string[] GetFollowingPhones(IReadOnlyList<string> tokens, int start, string externalNext) {
            for (int i = start; i < tokens.Count; ++i) {
                string token = tokens[i];
                if (token == "ー" || token == "っ") {
                    continue;
                }
                if (token == "ん") {
                    return new[] { "n" };
                }
                if (KanaMap.Entries.TryGetValue(token, out var phones) && phones.Length > 0) {
                    return phones;
                }
            }
            return GetInitialPhones(externalNext);
        }

        private static string[] GetInitialPhones(string lyric) {
            if (string.IsNullOrWhiteSpace(lyric)) {
                return Array.Empty<string>();
            }
            string normalized = NormalizeKana(lyric.Trim());
            var direct = ParseDirectSymbols(normalized);
            if (direct.Length > 0) {
                return direct;
            }
            foreach (string token in Tokenize(normalized)) {
                if (token == "ー" || token == "っ") {
                    continue;
                }
                if (token == "ん") {
                    return new[] { "n" };
                }
                if (KanaMap.Entries.TryGetValue(token, out var phones) && phones.Length > 0) {
                    return phones;
                }
            }
            return Array.Empty<string>();
        }

        private static string ResolveNasal(string nextPhone) {
            switch (nextPhone) {
                case "b":
                case "p":
                case "m":
                    return "m";
                case "g":
                case "k":
                    return "ng";
                default:
                    return "n";
            }
        }

        private static string ResolveSokuon(IReadOnlyList<string> nextPhones) {
            if (nextPhones == null || nextPhones.Count == 0) {
                return "q";
            }
            foreach (string phone in nextPhones) {
                if (!JapaneseArpaG2p.IsVowelSymbol(phone) && phone != "y" && phone != "w" && phone != "r" && phone != "l") {
                    return phone;
                }
            }
            return "q";
        }

        private static string[] ParseDirectSymbols(string text) {
            if (string.IsNullOrWhiteSpace(text) || !text.Any(char.IsLetter) ||
                text.Any(ch => ch >= '\u3040')) {
                return Array.Empty<string>();
            }
            string[] symbols = text.ToLowerInvariant()
                .Split(new[] { ' ', '\t', ',', '/' }, StringSplitOptions.RemoveEmptyEntries);
            return symbols.Length > 0 && symbols.All(symbol => new JapaneseArpaG2p().IsValidSymbol(symbol))
                ? symbols
                : Array.Empty<string>();
        }

        private static List<string> Tokenize(string normalized) {
            var result = new List<string>();
            for (int i = 0; i < normalized.Length;) {
                char current = normalized[i];
                if (IgnoredCharacters.Contains(current)) {
                    ++i;
                    continue;
                }
                if (current == 'ー' || current == 'ん' || current == 'っ') {
                    result.Add(current.ToString());
                    ++i;
                    continue;
                }

                string found = null;
                int maxLength = Math.Min(3, normalized.Length - i);
                for (int length = maxLength; length >= 1; --length) {
                    string candidate = normalized.Substring(i, length);
                    if (KanaMap.Entries.ContainsKey(candidate)) {
                        found = candidate;
                        break;
                    }
                }
                if (found != null) {
                    result.Add(found);
                    i += found.Length;
                } else {
                    ++i;
                }
            }
            return result;
        }

        private static string NormalizeKana(string input) {
            string value = input.Normalize(NormalizationForm.FormKC)
                .Replace("ヷ", "ヴァ")
                .Replace("ヸ", "ヴィ")
                .Replace("ヹ", "ヴェ")
                .Replace("ヺ", "ヴォ");
            var builder = new StringBuilder(value.Length);
            foreach (char ch in value) {
                if (ch >= '\u30A1' && ch <= '\u30F6') {
                    builder.Append((char)(ch - 0x60));
                } else {
                    builder.Append(ch);
                }
            }
            return builder.ToString();
        }
    }
}
