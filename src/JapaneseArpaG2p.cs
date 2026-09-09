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

        // Longest romaji entries are matched first. Both Hepburn and Kunrei-style
        // spellings are accepted where practical. l- spellings deliberately map to
        // katakana ラ行 sentinels so they can select ARPAbet /l/ rather than /r/.
        private static readonly IReadOnlyDictionary<string, string> RomajiToKana =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                { "kya", "きゃ" }, { "kyu", "きゅ" }, { "kye", "きぇ" }, { "kyo", "きょ" },
                { "gya", "ぎゃ" }, { "gyu", "ぎゅ" }, { "gye", "ぎぇ" }, { "gyo", "ぎょ" },
                { "sha", "しゃ" }, { "shu", "しゅ" }, { "sho", "しょ" },
                { "sya", "しゃ" }, { "syu", "しゅ" }, { "syo", "しょ" },
                { "ja", "じゃ" }, { "ju", "じゅ" }, { "jo", "じょ" },
                { "jya", "じゃ" }, { "jyu", "じゅ" }, { "jyo", "じょ" },
                { "zya", "じゃ" }, { "zyu", "じゅ" }, { "zyo", "じょ" },
                { "cha", "ちゃ" }, { "chu", "ちゅ" }, { "che", "ちぇ" }, { "cho", "ちょ" },
                { "cya", "ちゃ" }, { "cyu", "ちゅ" }, { "cye", "ちぇ" }, { "cyo", "ちょ" },
                { "tya", "ちゃ" }, { "tyu", "ちゅ" }, { "tye", "ちぇ" }, { "tyo", "ちょ" },
                { "dya", "ぢゃ" }, { "dyu", "ぢゅ" }, { "dye", "ぢぇ" }, { "dyo", "ぢょ" },
                { "nya", "にゃ" }, { "nyu", "にゅ" }, { "nye", "にぇ" }, { "nyo", "にょ" },
                { "hya", "ひゃ" }, { "hyu", "ひゅ" }, { "hye", "ひぇ" }, { "hyo", "ひょ" },
                { "bya", "びゃ" }, { "byu", "びゅ" }, { "bye", "びぇ" }, { "byo", "びょ" },
                { "pya", "ぴゃ" }, { "pyu", "ぴゅ" }, { "pye", "ぴぇ" }, { "pyo", "ぴょ" },
                { "mya", "みゃ" }, { "myu", "みゅ" }, { "mye", "みぇ" }, { "myo", "みょ" },
                { "rya", "りゃ" }, { "ryu", "りゅ" }, { "rye", "りぇ" }, { "ryo", "りょ" },
                { "lya", "リゃ" }, { "lyu", "リゅ" }, { "lye", "リぇ" }, { "lyo", "リょ" },
                { "fa", "ふぁ" }, { "fi", "ふぃ" }, { "fe", "ふぇ" }, { "fo", "ふぉ" },
                { "fya", "ふゃ" }, { "fyu", "ふゅ" }, { "fyo", "ふょ" },
                { "va", "ゔぁ" }, { "vi", "ゔぃ" }, { "vu", "ゔ" }, { "ve", "ゔぇ" }, { "vo", "ゔぉ" },
                { "vya", "ゔゃ" }, { "vyu", "ゔゅ" }, { "vyo", "ゔょ" },
                { "wi", "うぃ" }, { "we", "うぇ" }, { "wo", "を" }, { "ye", "いぇ" },
                { "kwa", "くぁ" }, { "kwi", "くぃ" }, { "kwe", "くぇ" }, { "kwo", "くぉ" },
                { "gwa", "ぐぁ" }, { "gwi", "ぐぃ" }, { "gwe", "ぐぇ" }, { "gwo", "ぐぉ" },
                { "she", "しぇ" }, { "je", "じぇ" },
                { "tsa", "つぁ" }, { "tsi", "つぃ" }, { "tsyu", "つゅ" }, { "tse", "つぇ" }, { "tso", "つぉ" },
                { "tha", "てゃ" }, { "thi", "てぃ" }, { "thu", "てゅ" }, { "the", "てぇ" }, { "tho", "てょ" },
                { "dha", "でゃ" }, { "dhu", "でゅ" }, { "dhe", "でぇ" }, { "dho", "でょ" },
                { "twa", "とぁ" }, { "twi", "とぃ" }, { "twu", "とぅ" }, { "twe", "とぇ" }, { "two", "とぉ" },
                { "dwa", "どぁ" }, { "dwi", "どぃ" }, { "dwu", "どぅ" }, { "dwe", "どぇ" }, { "dwo", "どぉ" },
                { "ti", "ち" }, { "tu", "つ" }, { "si", "し" }, { "zi", "じ" },
                { "hu", "ふ" }, { "shi", "し" }, { "chi", "ち" }, { "tsu", "つ" },
                { "ji", "じ" }, { "fu", "ふ" }, { "dhi", "でぃ" }, { "di", "ぢ" }, { "du", "づ" },
                { "ka", "か" }, { "ki", "き" }, { "ku", "く" }, { "ke", "け" }, { "ko", "こ" },
                { "ga", "が" }, { "gi", "ぎ" }, { "gu", "ぐ" }, { "ge", "げ" }, { "go", "ご" },
                { "sa", "さ" }, { "su", "す" }, { "se", "せ" }, { "so", "そ" },
                { "za", "ざ" }, { "zu", "ず" }, { "ze", "ぜ" }, { "zo", "ぞ" },
                { "ta", "た" }, { "te", "て" }, { "to", "と" },
                { "da", "だ" }, { "de", "で" }, { "do", "ど" },
                { "na", "な" }, { "ni", "に" }, { "nu", "ぬ" }, { "ne", "ね" }, { "no", "の" },
                { "ha", "は" }, { "hi", "ひ" }, { "he", "へ" }, { "ho", "ほ" },
                { "ba", "ば" }, { "bi", "び" }, { "bu", "ぶ" }, { "be", "べ" }, { "bo", "ぼ" },
                { "pa", "ぱ" }, { "pi", "ぴ" }, { "pu", "ぷ" }, { "pe", "ぺ" }, { "po", "ぽ" },
                { "ma", "ま" }, { "mi", "み" }, { "mu", "む" }, { "me", "め" }, { "mo", "も" },
                { "ya", "や" }, { "yu", "ゆ" }, { "yo", "よ" },
                { "ra", "ら" }, { "ri", "り" }, { "ru", "る" }, { "re", "れ" }, { "ro", "ろ" },
                { "la", "ラ" }, { "li", "リ" }, { "lu", "ル" }, { "le", "レ" }, { "lo", "ロ" },
                { "wa", "わ" },
                { "a", "あ" }, { "i", "い" }, { "u", "う" }, { "e", "え" }, { "o", "お" }
            };

        private static readonly string[] RomajiKeys = RomajiToKana.Keys
            .OrderByDescending(key => key.Length)
            .ToArray();

        public static string[] Resolve(string lyric, string nextLyric, string previousVowel) {
            if (string.IsNullOrWhiteSpace(lyric)) {
                return Array.Empty<string>();
            }

            string raw = lyric.Trim().Normalize(NormalizationForm.FormKC);
            var direct = ParseDirectSymbols(raw);
            if (direct.Length > 0) {
                return direct;
            }

            if (TryRomanizeToKana(raw, out string romanizedKana)) {
                raw = romanizedKana;
            }

            string normalized = NormalizeKana(raw);
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
                    // In the same lyric, a long-vowel mark extends the preceding vowel; it
                    // should not insert a second vowel transition. A standalone ー note still
                    // needs a symbol so the previous vowel can continue onto that note.
                    if (result.Count == 0) {
                        result.Add(string.IsNullOrEmpty(lastVowel) ? "uw" : lastVowel);
                    }
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
                    !JapaneseArpaG2p.IsVowelSymbol(phone) &&
                    phone != "n" && phone != "m" && phone != "ng") {
                    continue;
                }
                collapsed.Add(phone);
            }
            return collapsed.ToArray();
        }

        public static bool IsTailMarker(string lyric) {
            return string.Equals((lyric ?? string.Empty).Trim(), "R", StringComparison.Ordinal);
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
            string raw = lyric.Trim().Normalize(NormalizationForm.FormKC);
            var direct = ParseDirectSymbols(raw);
            if (direct.Length > 0) {
                return direct;
            }
            if (TryRomanizeToKana(raw, out string romanizedKana)) {
                raw = romanizedKana;
            }
            string normalized = NormalizeKana(raw);
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
            // Use canonical ARPAbet /n/ for Japanese moraic ん.
            // Contextual m/ng is phonetically possible, but many ARPAsing banks do not
            // contain the resulting m/ng CC transitions (for example m b or ng k).
            // Keeping ん as n makes standalone and in-word conversion deterministic.
            return "n";
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

        private static bool TryRomanizeToKana(string text, out string kana) {
            kana = null;
            if (string.IsNullOrWhiteSpace(text)) {
                return false;
            }

            string compact = new string(text
                .Where(ch => !char.IsWhiteSpace(ch) && ch != '_' && ch != '・')
                .ToArray())
                .ToLowerInvariant();

            if (compact.Length == 0 || compact.Any(ch => !(ch >= 'a' && ch <= 'z') && ch != '\'' && ch != '-')) {
                return false;
            }

            var builder = new StringBuilder();
            int i = 0;
            while (i < compact.Length) {
                char current = compact[i];

                if (current == '-') {
                    builder.Append('ー');
                    i++;
                    continue;
                }

                // n' explicitly marks the moraic nasal.
                if (current == 'n' && i + 1 < compact.Length && compact[i + 1] == '\'') {
                    builder.Append('ん');
                    i += 2;
                    continue;
                }

                // A doubled consonant produces a small っ. For nn, consume only the
                // first n so the second can still start na/ni/... (konnichiwa).
                if (i + 1 < compact.Length && current == compact[i + 1] &&
                    IsRomanConsonant(current) && current != 'n') {
                    builder.Append('っ');
                    i++;
                    continue;
                }
                if (current == 'n') {
                    if (i + 1 == compact.Length) {
                        builder.Append('ん');
                        i++;
                        continue;
                    }
                    char next = compact[i + 1];
                    if (next == 'n') {
                        builder.Append('ん');
                        i++;
                        continue;
                    }
                    if (!IsRomanVowel(next) && next != 'y') {
                        builder.Append('ん');
                        i++;
                        continue;
                    }
                }

                string matched = null;
                foreach (string key in RomajiKeys) {
                    if (i + key.Length <= compact.Length &&
                        string.Compare(compact, i, key, 0, key.Length, StringComparison.OrdinalIgnoreCase) == 0) {
                        matched = key;
                        break;
                    }
                }
                if (matched == null) {
                    return false;
                }
                builder.Append(RomajiToKana[matched]);
                i += matched.Length;
            }

            kana = builder.ToString();
            return kana.Length > 0;
        }

        private static bool IsRomanVowel(char ch) {
            return ch == 'a' || ch == 'i' || ch == 'u' || ch == 'e' || ch == 'o';
        }

        private static bool IsRomanConsonant(char ch) {
            return ch >= 'a' && ch <= 'z' && !IsRomanVowel(ch);
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
                // Preserve katakana ラ行 as an intentional /l/ selector.
                if (ch == 'ラ' || ch == 'リ' || ch == 'ル' || ch == 'レ' || ch == 'ロ') {
                    builder.Append(ch);
                } else if (ch >= '\u30A1' && ch <= '\u30F6') {
                    builder.Append((char)(ch - 0x60));
                } else {
                    builder.Append(ch);
                }
            }
            return builder.ToString();
        }
    }
}
