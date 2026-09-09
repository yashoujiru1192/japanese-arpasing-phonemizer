using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JapaneseArpasingPhonemizer;

internal static class Program {
    private static int passed = 0;
    private static int failed = 0;

    private static void AssertPhones(string label, string lyric, params string[] expected) {
        string[] actual = KanaConverter.Resolve(lyric, null, null);
        if (actual.SequenceEqual(expected)) {
            passed++;
        } else {
            failed++;
            Console.WriteLine($"FAIL {label}: {lyric} => [{string.Join(" ", actual)}], expected [{string.Join(" ", expected)}]");
        }
    }

    private static string ToKatakana(string hiragana) {
        var chars = hiragana.Select(ch => ch >= '\u3041' && ch <= '\u3096' ? (char)(ch + 0x60) : ch).ToArray();
        return new string(chars);
    }

    private static bool BeginsWithRRow(string key) {
        return key.StartsWith("ら") || key.StartsWith("り") || key.StartsWith("る") ||
               key.StartsWith("れ") || key.StartsWith("ろ");
    }

    public static int Main() {
        // 1) Exhaustively verify every KanaMap entry resolves to its declared ARPAbet sequence.
        foreach (var entry in KanaMap.Entries) {
            AssertPhones("KanaMap", entry.Key, entry.Value);
        }

        // 2) Verify katakana normalization for every hiragana entry except ラ-row,
        // which is intentionally preserved as the explicit /l/ selector.
        int katakanaChecks = 0;
        foreach (var entry in KanaMap.Entries) {
            if (entry.Key.Any(ch => ch < '\u3041' || ch > '\u3096') || BeginsWithRRow(entry.Key)) {
                continue;
            }
            string kata = ToKatakana(entry.Key);
            AssertPhones("Katakana", kata, entry.Value);
            katakanaChecks++;
        }

        // 3) Every romaji dictionary entry must resolve identically to the kana it points at.
        var field = typeof(KanaConverter).GetField("RomajiToKana", BindingFlags.Static | BindingFlags.NonPublic);
        var romaji = (IReadOnlyDictionary<string, string>)field.GetValue(null);
        int romajiChecks = 0;
        foreach (var entry in romaji) {
            string[] expected = KanaConverter.Resolve(entry.Value, null, null);
            AssertPhones("Romaji", entry.Key, expected);
            romajiChecks++;
        }

        // 4) Moraic nasal ん/ン. v0.2.4 deliberately keeps it canonical 'n' in all contexts.
        AssertPhones("Nasal", "ん", "n");
        AssertPhones("Nasal", "ン", "n");
        AssertPhones("Nasal", "あん", "aa", "n");
        AssertPhones("Nasal", "あんな", "aa", "n", "n", "aa");
        AssertPhones("Nasal", "さんぽ", "s", "aa", "n", "p", "ow");
        AssertPhones("Nasal", "りんご", "r", "iy", "n", "g", "ow");
        AssertPhones("Nasal", "かんき", "k", "aa", "n", "k", "iy");
        AssertPhones("Nasal", "あんか", "aa", "n", "k", "aa");
        AssertPhones("Nasal", "いんき", "iy", "n", "k", "iy");
        AssertPhones("Nasal", "うんす", "uw", "n", "s", "uw");
        AssertPhones("Nasal", "えんて", "eh", "n", "t", "eh");
        AssertPhones("Nasal", "おんぱ", "ow", "n", "p", "aa");
        AssertPhones("Nasal", "んあ", "n", "aa");
        AssertPhones("Nasal", "んな", "n", "n", "aa");
        AssertPhones("Nasal", "んん", "n", "n");
        AssertPhones("Nasal/Katakana", "アンカ", "aa", "n", "k", "aa");
        AssertPhones("Nasal/Katakana", "サンポ", "s", "aa", "n", "p", "ow");
        AssertPhones("Nasal/Romaji", "konnichiwa", "k", "ow", "n", "n", "iy", "ch", "iy", "w", "aa");
        AssertPhones("Nasal/Romaji", "kanpai", "k", "aa", "n", "p", "aa", "iy");
        AssertPhones("Nasal/Romaji", "shin'ya", "sh", "iy", "n", "y", "aa");
        AssertPhones("Nasal/Romaji", "nn", "n", "n");
        AssertPhones("Nasal/Romaji", "n'", "n");

        // 5) Explicit extended-Japanese/loanword mora regression set.
        var extended = new Dictionary<string, string[]> {
            { "いぇ", new[]{"y","eh"} }, { "うぃ", new[]{"w","iy"} }, { "うぇ", new[]{"w","eh"} }, { "うぉ", new[]{"w","ow"} },
            { "きぇ", new[]{"k","y","eh"} }, { "ぎぇ", new[]{"g","iy","y","eh"} },
            { "くぁ", new[]{"k","w","aa"} }, { "くぃ", new[]{"k","w","iy"} }, { "くぇ", new[]{"k","w","eh"} }, { "くぉ", new[]{"k","w","ow"} }, { "くゎ", new[]{"k","w","aa"} },
            { "ぐぁ", new[]{"g","w","aa"} }, { "ぐぃ", new[]{"g","w","iy"} }, { "ぐぇ", new[]{"g","w","eh"} }, { "ぐぉ", new[]{"g","w","ow"} }, { "ぐゎ", new[]{"g","w","aa"} },
            { "しぇ", new[]{"sh","eh"} }, { "じぇ", new[]{"zh","eh"} }, { "ちぇ", new[]{"ch","eh"} }, { "ぢぇ", new[]{"jh","eh"} },
            { "すぃ", new[]{"s","iy"} }, { "ずぃ", new[]{"z","iy"} },
            { "つぁ", new[]{"t","s","aa"} }, { "つぃ", new[]{"t","s","iy"} }, { "つゅ", new[]{"t","s","iy","y","uw"} }, { "つぇ", new[]{"t","s","eh"} }, { "つぉ", new[]{"t","s","ow"} },
            { "てゃ", new[]{"t","iy","y","aa"} }, { "てぃ", new[]{"t","iy"} }, { "てゅ", new[]{"t","iy","y","uw"} }, { "てぇ", new[]{"t","iy","y","eh"} }, { "てょ", new[]{"t","iy","y","ow"} },
            { "でゃ", new[]{"d","iy","y","aa"} }, { "でぃ", new[]{"d","iy"} }, { "でゅ", new[]{"d","iy","y","uw"} }, { "でぇ", new[]{"d","iy","y","eh"} }, { "でょ", new[]{"d","iy","y","ow"} },
            { "とぁ", new[]{"t","w","aa"} }, { "とぃ", new[]{"t","w","iy"} }, { "とぅ", new[]{"t","uw"} }, { "とぇ", new[]{"t","w","eh"} }, { "とぉ", new[]{"t","w","ow"} },
            { "どぁ", new[]{"d","w","aa"} }, { "どぃ", new[]{"d","w","iy"} }, { "どぅ", new[]{"d","uw"} }, { "どぇ", new[]{"d","w","eh"} }, { "どぉ", new[]{"d","w","ow"} },
            { "にぇ", new[]{"n","iy","y","eh"} }, { "ひぇ", new[]{"hh","iy","y","eh"} }, { "びぇ", new[]{"b","y","eh"} }, { "ぴぇ", new[]{"p","iy","y","eh"} },
            { "ふぁ", new[]{"f","aa"} }, { "ふぃ", new[]{"f","iy"} }, { "ふぇ", new[]{"f","eh"} }, { "ふぉ", new[]{"f","ow"} }, { "ふゃ", new[]{"f","iy","y","aa"} }, { "ふゅ", new[]{"f","iy","y","uw"} }, { "ふょ", new[]{"f","iy","y","ow"} },
            { "みぇ", new[]{"m","y","eh"} }, { "りぇ", new[]{"r","iy","y","eh"} },
            { "ゔぁ", new[]{"v","aa"} }, { "ゔぃ", new[]{"v","iy"} }, { "ゔ", new[]{"v","uw"} }, { "ゔぇ", new[]{"v","eh"} }, { "ゔぉ", new[]{"v","ow"} },
            { "ゔゃ", new[]{"v","y","aa"} }, { "ゔゅ", new[]{"v","y","uw"} }, { "ゔょ", new[]{"v","y","ow"} },
        };
        foreach (var entry in extended) {
            AssertPhones("Extended", entry.Key, entry.Value);
            if (BeginsWithRRow(entry.Key)) {
                // Katakana ラ-row is intentionally the /l/ selector, so リェ is checked separately.
                AssertPhones("Extended/Katakana-L", ToKatakana(entry.Key), "l", "iy", "y", "eh");
            } else {
                AssertPhones("Extended/Katakana", ToKatakana(entry.Key), entry.Value);
            }
        }

        // 6) Regression checks from earlier versions.
        AssertPhones("Four-kana", "じ", "zh", "iy");
        AssertPhones("Four-kana", "ぢ", "jh", "iy");
        AssertPhones("Four-kana", "ず", "z", "uw");
        AssertPhones("Four-kana", "づ", "d", "uw");
        AssertPhones("Romaji", "dhi", "d", "iy");
        AssertPhones("Sokuon", "がっこう", "g", "aa", "k", "ow", "uw");
        AssertPhones("LongMark", "こー", "k", "ow");
        AssertPhones("Direct", "ch eh", "ch", "eh");
        AssertPhones("R-row", "ら", "r", "aa");
        AssertPhones("L-row", "ラ", "l", "aa");
        AssertPhones("R-yoon", "りゃ", "r", "iy", "y", "aa");
        AssertPhones("L-yoon", "リゃ", "l", "iy", "y", "aa");

        // 7) All declared mapping output symbols must be valid ARPAbet symbols.
        var g2p = new JapaneseArpaG2p();
        foreach (var entry in KanaMap.Entries) {
            foreach (var phone in entry.Value) {
                if (!g2p.IsValidSymbol(phone)) {
                    failed++;
                    Console.WriteLine($"FAIL Invalid symbol: {entry.Key} -> {phone}");
                } else {
                    passed++;
                }
            }
        }

        Console.WriteLine($"KanaMap entries: {KanaMap.Entries.Count}");
        Console.WriteLine($"Katakana normalization checks: {katakanaChecks}");
        Console.WriteLine($"Romaji dictionary checks: {romajiChecks}");
        Console.WriteLine($"Extended mora explicit checks: {extended.Count * 2}");
        Console.WriteLine($"PASS assertions: {passed}");
        Console.WriteLine($"FAIL assertions: {failed}");
        return failed == 0 ? 0 : 1;
    }
}
