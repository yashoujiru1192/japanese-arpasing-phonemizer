using System.Text;

namespace JAtoKOPhonemizer;

internal enum MoraKind {
    Syllable,
    Nasal,
    Sokuon,
    LongVowel,
    Ending,
}

internal sealed record KoreanSyllable(string Cv, string Onset, string Vowel) {
    public string? Hangul => KanaMapper.ComposeHangul(Onset, Vowel);
}

internal sealed record Mora(MoraKind Kind, IReadOnlyList<KoreanSyllable> Variants);

internal static class KanaMapper {
    private static readonly string[] Vowels = {
        "eui", "weo", "yeo", "yae", "wae", "ya", "ye", "yo", "yu", "wa", "we", "wi", "wo", "ae", "oe", "eo", "eu", "a", "e", "i", "o", "u",
    };

    private static readonly Dictionary<string, int> Initials = new(StringComparer.OrdinalIgnoreCase) {
        [""] = 11, ["g"] = 0, ["gg"] = 1, ["n"] = 2, ["d"] = 3, ["dd"] = 4,
        ["r"] = 5, ["l"] = 5, ["m"] = 6, ["b"] = 7, ["bb"] = 8, ["s"] = 9,
        ["ss"] = 10, ["j"] = 12, ["jj"] = 13, ["ch"] = 14, ["k"] = 15,
        ["t"] = 16, ["p"] = 17, ["h"] = 18,
    };

    private static readonly Dictionary<string, int> Medials = new(StringComparer.OrdinalIgnoreCase) {
        ["a"] = 0, ["ae"] = 1, ["ya"] = 2, ["yae"] = 3, ["eo"] = 4, ["e"] = 5,
        ["yeo"] = 6, ["ye"] = 7, ["o"] = 8, ["wa"] = 9, ["wae"] = 10, ["oe"] = 11,
        ["yo"] = 12, ["u"] = 13, ["weo"] = 14, ["wo"] = 14, ["we"] = 15,
        ["wi"] = 16, ["yu"] = 17, ["eu"] = 18, ["eui"] = 19, ["i"] = 20,
    };

    private static readonly Dictionary<string, string[]> DefaultMap = BuildDefaultMap();

    public static bool IsPassThrough(string lyric) =>
        lyric is "-" or "br" or "bre" or "息" or "吸";

    public static Mora? Parse(string? lyric, IReadOnlyDictionary<string, string[]> customMap) {
        if (string.IsNullOrWhiteSpace(lyric)) {
            return null;
        }
        var key = ToHiragana(lyric.Trim()).ToLowerInvariant();
        if (key is "ん" or "n") {
            return new Mora(MoraKind.Nasal, Array.Empty<KoreanSyllable>());
        }
        if (key is "っ" or "cl" or "q") {
            return new Mora(MoraKind.Sokuon, Array.Empty<KoreanSyllable>());
        }
        if (key is "ー" or "ｰ" or "-") {
            return new Mora(MoraKind.LongVowel, Array.Empty<KoreanSyllable>());
        }
        if (key is "r" or "r2") {
            return new Mora(MoraKind.Ending, Array.Empty<KoreanSyllable>());
        }

        if (!customMap.TryGetValue(key, out var values) && !DefaultMap.TryGetValue(key, out values)) {
            values = IsRomanCv(key) ? new[] { key } : null;
        }
        if (values is null) {
            return null;
        }

        var variants = values
            .Select(ParseKoreanSyllable)
            .Where(value => value is not null)
            .Cast<KoreanSyllable>()
            .DistinctBy(value => value.Cv)
            .ToArray();
        return variants.Length == 0 ? null : new Mora(MoraKind.Syllable, variants);
    }

    public static Dictionary<string, string[]> LoadCustomMap(string path) {
        var result = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(path)) {
            return result;
        }
        foreach (var rawLine in File.ReadLines(path, Encoding.UTF8)) {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#')) {
                continue;
            }
            var separator = line.IndexOf('=');
            if (separator <= 0 || separator == line.Length - 1) {
                continue;
            }
            var key = ToHiragana(line[..separator].Trim()).ToLowerInvariant();
            var values = line[(separator + 1)..]
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(value => value.ToLowerInvariant())
                .Where(IsRomanCv)
                .ToArray();
            if (values.Length > 0) {
                result[key] = values;
            }
        }
        return result;
    }

    public static IEnumerable<string> MainAliasCandidates(
        string rawLyric,
        Mora current,
        Mora? previous,
        bool phraseStart) {
        foreach (var variant in current.Variants) {
            if (phraseStart) {
                yield return $"- {variant.Cv}";
                yield return $"-{variant.Cv}";
            }
            var previousVowel = PreviousConnectionVowel(previous);
            if (previousVowel is not null) {
                yield return $"{previousVowel} {variant.Cv}";
                var rawPreviousVowel = PreviousVowel(previous);
                if (rawPreviousVowel is not null &&
                    !rawPreviousVowel.Equals(previousVowel, StringComparison.OrdinalIgnoreCase)) {
                    yield return $"{rawPreviousVowel} {variant.Cv}";
                }
            }
            yield return variant.Cv;
            if (variant.Hangul is not null) {
                yield return variant.Hangul;
            }
        }
        yield return rawLyric;
        yield return ToHiragana(rawLyric);
    }

    public static IEnumerable<string> VcCandidates(Mora current, Mora next) {
        var rawVowel = current.Variants[0].Vowel;
        var vowel = ConnectionVowel(rawVowel);
        foreach (var consonant in next.Variants
            .Select(variant => SimplifyVcConsonant(variant.Onset))
            .Where(value => value.Length > 0)
            .Distinct()) {
            yield return $"{vowel} {consonant}";
            yield return vowel + consonant;
            if (!rawVowel.Equals(vowel, StringComparison.OrdinalIgnoreCase)) {
                yield return $"{rawVowel} {consonant}";
                yield return rawVowel + consonant;
            }
        }
    }

    public static IEnumerable<string> CPlusVConsonantCandidates(
        KoreanSyllable syllable,
        bool phraseStart) {
        var onset = syllable.Onset switch {
            "gg" => "kk",
            "dd" => "tt",
            "bb" => "pp",
            _ => syllable.Onset,
        };
        if (string.IsNullOrEmpty(onset)) {
            yield break;
        }
        if (phraseStart) {
            yield return $"- {onset}";
            yield return $"-{onset}";
            yield return onset;
        } else {
            yield return onset;
            yield return $"- {onset}";
            yield return $"-{onset}";
        }
    }

    public static IEnumerable<string> CPlusVVowelCandidates(
        KoreanSyllable syllable,
        bool phraseStart) {
        if (phraseStart && string.IsNullOrEmpty(syllable.Onset)) {
            yield return $"- {syllable.Vowel}";
            yield return $"-{syllable.Vowel}";
        }
        yield return syllable.Vowel;
    }

    public static IEnumerable<string> NasalCandidates(Mora? previous) {
        var vowel = PreviousConnectionVowel(previous);
        if (vowel is not null) {
            yield return $"{vowel} N";
            yield return $"{vowel} n";
            yield return vowel + "n";
            yield return $"{vowel} NG";
        }
        yield return "N";
        yield return "n";
        yield return "NG";
        yield return "ng";
    }

    public static IEnumerable<string> SokuonCandidates(Mora? previous, Mora? next) {
        var vowel = PreviousConnectionVowel(previous);
        var onset = next?.Kind == MoraKind.Syllable ? next.Variants[0].Onset : string.Empty;
        var closure = onset.StartsWith('p') || onset.StartsWith('b') ? "P"
            : onset.StartsWith('k') || onset.StartsWith('g') ? "K"
            : "T";
        if (vowel is not null) {
            yield return $"{vowel} {closure}";
            yield return vowel + closure;
            yield return $"{vowel} {closure.ToLowerInvariant()}";
            yield return vowel + closure.ToLowerInvariant();
        }
        yield return closure;
        yield return closure.ToLowerInvariant();
        yield return "R";
    }

    public static IEnumerable<string> LongVowelCandidates(Mora? previous) {
        var vowel = PreviousConnectionVowel(previous);
        if (vowel is not null) {
            yield return $"{vowel} {vowel}";
            yield return vowel;
        }
        yield return "ー";
    }

    public static IEnumerable<string> EndingCandidates(Mora? previous, string ending = "R") {
        var vowel = PreviousVowel(previous);
        if (vowel is not null) {
            var tailVowel = ConnectionVowel(vowel);
            yield return $"{tailVowel} {ending}";
            yield return tailVowel + ending;
            if (!tailVowel.Equals(vowel, StringComparison.OrdinalIgnoreCase)) {
                yield return $"{vowel} {ending}";
                yield return vowel + ending;
            }
        }
        yield return ending;
    }

    public static int VcPosition(int duration) =>
        Math.Max(0, duration - Math.Min(duration / 3, 120));

    public static string? ComposeHangul(string onset, string vowel) {
        if (!Initials.TryGetValue(onset, out var initial) || !Medials.TryGetValue(vowel, out var medial)) {
            return null;
        }
        return char.ConvertFromUtf32(0xAC00 + ((initial * 21) + medial) * 28);
    }

    private static string? PreviousVowel(Mora? previous) =>
        previous?.Kind == MoraKind.Syllable ? previous.Variants[0].Vowel : null;

    private static string? PreviousConnectionVowel(Mora? previous) {
        var vowel = PreviousVowel(previous);
        return vowel is null ? null : ConnectionVowel(vowel);
    }

    private static string ConnectionVowel(string vowel) => vowel switch {
        "eui" or "ui" => "i",
        "oe" => "e",
        _ when vowel.StartsWith('w') || vowel.StartsWith('y') => vowel[1..],
        _ => vowel,
    };

    private static string SimplifyVcConsonant(string onset) => onset switch {
        "gg" => "k", "dd" => "t", "bb" => "p", "ss" => "s", "jj" => "j",
        "r" or "l" => "r", _ => onset,
    };

    private static KoreanSyllable? ParseKoreanSyllable(string cv) {
        cv = cv.Trim().ToLowerInvariant();
        foreach (var vowel in Vowels) {
            if (!cv.EndsWith(vowel, StringComparison.Ordinal)) {
                continue;
            }
            var onset = cv[..^vowel.Length];
            if (Initials.ContainsKey(onset) || onset is "f" or "v" or "z" or "sh" or "ts" or "th") {
                return new KoreanSyllable(cv, onset, vowel);
            }
        }
        return null;
    }

    private static bool IsRomanCv(string value) => ParseKoreanSyllable(value) is not null;

    private static string ToHiragana(string value) {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value.Normalize(NormalizationForm.FormKC)) {
            builder.Append(character is >= '\u30A1' and <= '\u30F6'
                ? (char)(character - 0x60)
                : character);
        }
        return builder.ToString();
    }

    private static Dictionary<string, string[]> BuildDefaultMap() {
        var map = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        void Add(string keys, params string[] values) {
            foreach (var key in keys.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) {
                map[key] = values;
            }
        }

        Add("あ,a", "a"); Add("い,i", "i"); Add("う,u", "eu", "u"); Add("え,e", "e"); Add("お,o,を,wo", "o");
        Add("か,ka", "ka", "ga"); Add("き,ki", "ki", "gi"); Add("く,ku", "keu", "ku", "geu"); Add("け,ke", "ke", "ge"); Add("こ,ko", "ko", "go");
        Add("さ,sa", "sa"); Add("し,shi,si", "si", "shi"); Add("す,su", "seu", "su"); Add("せ,se", "se"); Add("そ,so", "so");
        Add("た,ta", "ta", "da"); Add("ち,chi,ti", "chi", "ji"); Add("つ,tsu,tu", "cheu", "jeu", "seu"); Add("て,te", "te", "de"); Add("と,to", "to", "do");
        Add("な,na", "na"); Add("に,ni", "ni"); Add("ぬ,nu", "neu", "nu"); Add("ね,ne", "ne"); Add("の,no", "no");
        Add("は,ha", "ha"); Add("ひ,hi", "hi"); Add("ふ,fu,hu", "hu", "pu"); Add("へ,he", "he"); Add("ほ,ho", "ho");
        Add("ま,ma", "ma"); Add("み,mi", "mi"); Add("む,mu", "meu", "mu"); Add("め,me", "me"); Add("も,mo", "mo");
        Add("や,ya", "ya"); Add("ゆ,yu", "yu"); Add("よ,yo", "yo");
        Add("ら,ra", "ra"); Add("り,ri", "ri"); Add("る,ru", "reu", "ru"); Add("れ,re", "re"); Add("ろ,ro", "ro");
        Add("わ,wa", "wa"); Add("ゐ,wi", "wi"); Add("ゑ,we", "we");

        Add("が,ga", "ga"); Add("ぎ,gi", "gi"); Add("ぐ,gu", "geu", "gu"); Add("げ,ge", "ge"); Add("ご,go", "go");
        Add("ざ,za", "za", "ja"); Add("じ,ji,zi", "ji", "zi"); Add("ず,zu", "jeu", "zu"); Add("ぜ,ze", "ze", "je"); Add("ぞ,zo", "zo", "jo");
        Add("だ,da", "da"); Add("ぢ,di", "ji", "di"); Add("づ,du", "jeu", "du"); Add("で,de", "de"); Add("ど,do", "do");
        Add("ば,ba", "ba"); Add("び,bi", "bi"); Add("ぶ,bu", "beu", "bu"); Add("べ,be", "be"); Add("ぼ,bo", "bo");
        Add("ぱ,pa", "pa"); Add("ぴ,pi", "pi"); Add("ぷ,pu", "peu", "pu"); Add("ぺ,pe", "pe"); Add("ぽ,po", "po");
        Add("ゔ,vu", "vu", "bu");

        Add("きゃ,kya", "kya", "gya"); Add("きゅ,kyu", "kyu", "gyu"); Add("きょ,kyo", "kyo", "gyo");
        Add("しゃ,sha,sya", "sya", "sha"); Add("しゅ,shu,syu", "syu", "shu"); Add("しょ,sho,syo", "syo", "sho");
        Add("ちゃ,cha,tya", "cha"); Add("ちゅ,chu,tyu", "chu"); Add("ちょ,cho,tyo", "cho");
        Add("にゃ,nya", "nya"); Add("にゅ,nyu", "nyu"); Add("にょ,nyo", "nyo");
        Add("ひゃ,hya", "hya"); Add("ひゅ,hyu", "hyu"); Add("ひょ,hyo", "hyo");
        Add("みゃ,mya", "mya"); Add("みゅ,myu", "myu"); Add("みょ,myo", "myo");
        Add("りゃ,rya", "rya"); Add("りゅ,ryu", "ryu"); Add("りょ,ryo", "ryo");
        Add("ぎゃ,gya", "gya"); Add("ぎゅ,gyu", "gyu"); Add("ぎょ,gyo", "gyo");
        Add("じゃ,ja,jya,zya", "ja"); Add("じゅ,ju,jyu,zyu", "ju"); Add("じょ,jo,jyo,zyo", "jo");
        Add("びゃ,bya", "bya"); Add("びゅ,byu", "byu"); Add("びょ,byo", "byo");
        Add("ぴゃ,pya", "pya"); Add("ぴゅ,pyu", "pyu"); Add("ぴょ,pyo", "pyo");
        Add("ぢゃ,dya", "ja", "dya"); Add("ぢゅ,dyu", "ju", "dyu"); Add("ぢょ,dyo", "jo", "dyo");

        Add("いぃ", "i"); Add("いぇ,ye", "ye");
        Add("うぁ", "wa", "a"); Add("うぃ", "wi"); Add("うぅ", "u", "eu"); Add("うぇ", "we"); Add("うぉ", "wo", "weo", "o");
        Add("うゃ", "ya"); Add("うゅ", "yu"); Add("うぃぇ", "we", "ye"); Add("うょ", "yo");
        Add("きぇ", "kye", "ke"); Add("ぎぇ", "gye", "ge");
        Add("しぇ", "sye", "she"); Add("じぇ", "je", "ze"); Add("すぃ", "si"); Add("ずぃ", "ji", "zi");
        Add("ちぇ", "che"); Add("ぢぇ", "je", "de");
        Add("てぃ", "ti", "di"); Add("でぃ", "di"); Add("とぅ", "tu", "du"); Add("どぅ", "du");
        Add("とぃ", "twi", "ti"); Add("てゅ", "tyu", "chu"); Add("どぃ", "dwi", "di"); Add("でゅ", "dyu", "ju");
        Add("つぁ", "cha", "tsa"); Add("つぃ", "chi", "tsi"); Add("つぇ", "che", "tse"); Add("つぉ", "cho", "tso");
        Add("つゃ", "cha", "tsya"); Add("つゅ", "chu", "tsyu"); Add("つぃぇ", "che", "tsye"); Add("つょ", "cho", "tsyo");
        Add("づぁ", "ja", "za"); Add("づぃ", "ji", "zi"); Add("づぇ", "je", "ze"); Add("づぉ", "jo", "zo");
        Add("づゃ", "ja", "zya"); Add("づゅ", "ju", "zyu"); Add("づぃぇ", "je", "zye"); Add("づょ", "jo", "zyo");
        Add("ふぁ", "fa", "hwa", "pa"); Add("ふぃ", "fi", "hwi", "pi"); Add("ふぇ", "fe", "hwe", "pe"); Add("ふぉ", "fo", "ho", "po");
        Add("ふゃ", "fya", "hya", "pya"); Add("ふゅ", "fyu", "hyu", "pyu"); Add("ふぃぇ", "fye", "hye", "pye"); Add("ふょ", "fyo", "hyo", "pyo");
        Add("ゔぁ", "va", "ba"); Add("ゔぃ", "vi", "bi"); Add("ゔぇ", "ve", "be"); Add("ゔぉ", "vo", "bo");
        Add("ゔゃ", "vya", "bya"); Add("ゔゅ", "vyu", "byu"); Add("ゔぃぇ", "vye", "bye"); Add("ゔょ", "vyo", "byo");
        Add("くぁ,くゎ", "kwa", "gwa"); Add("くぃ", "kwi", "gwi"); Add("くぇ", "kwe", "gwe"); Add("くぉ", "kwo", "kweo", "ko");
        Add("ぐぁ,ぐゎ", "gwa"); Add("ぐぃ", "gwi"); Add("ぐぇ", "gwe"); Add("ぐぉ", "gwo", "gweo", "go");
        Add("ぬぃ", "nwi", "ni"); Add("にぇ", "nye", "ne"); Add("ひぇ", "hye", "he");
        Add("ぶぃ", "bwi", "bi"); Add("びぇ", "bye", "be"); Add("ぷぃ", "pwi", "pi"); Add("ぴぇ", "pye", "pe");
        Add("ほぅ", "hu", "ho"); Add("むぃ", "mwi", "mi"); Add("みぇ", "mye", "me"); Add("るぃ", "rwi", "ri"); Add("りぇ", "rye", "re");

        Add("ぁ", "a"); Add("ぃ", "i"); Add("ぅ", "eu", "u"); Add("ぇ", "e"); Add("ぉ", "o");
        Add("ゃ", "ya"); Add("ゅ", "yu"); Add("ょ", "yo");
        return map;
    }
}
