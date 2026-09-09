using System;
using System.Linq;
using JapaneseArpasingPhonemizer;
using OpenUtau.Api;

internal static class Program {
    private static int passed;
    private static int failed;

    private static void Check(string label, Phonemizer.Result result, params string[] expected) {
        string[] actual = result.phonemes?.Select(p => p.phoneme).ToArray() ?? Array.Empty<string>();
        if (actual.SequenceEqual(expected)) {
            passed++;
        } else {
            failed++;
            Console.WriteLine($"FAIL {label}: [{string.Join(" | ", actual)}] expected [{string.Join(" | ", expected)}]");
        }
    }

    public static int Main() {
        var phonemizer = new JapaneseArpasingPhonemizer.JapaneseArpasingPhonemizer();

        // Standalone moraic nasal: use start n and n-tail aliases.
        var standalone = new Phonemizer.Note {
            lyric = "ん", tone = 60, position = 0, duration = 480,
            phonemeAttributes = Array.Empty<Phonemizer.PhonemeAttributes>(),
        };
        Check("standalone ん", phonemizer.Process(
            new[] { standalone }, null, null, null, null, Array.Empty<Phonemizer.Note>()),
            "- n", "n -");

        // In context, ん must remain canonical n (not m/ng) and start as vowel->n.
        var previousA = new Phonemizer.Note {
            lyric = "あ", tone = 60, position = -480, duration = 480,
            phonemeAttributes = Array.Empty<Phonemizer.PhonemeAttributes>(),
        };
        var nextKa = new Phonemizer.Note {
            lyric = "か", tone = 60, position = 480, duration = 480,
            phonemeAttributes = Array.Empty<Phonemizer.PhonemeAttributes>(),
        };
        Check("あ + ん + か", phonemizer.Process(
            new[] { standalone }, previousA, nextKa, previousA, nextKa, new[] { previousA }),
            "aa n");

        var kataN = standalone;
        kataN.lyric = "ン";
        Check("standalone ン", phonemizer.Process(
            new[] { kataN }, null, null, null, null, Array.Empty<Phonemizer.Note>()),
            "- n", "n -");

        Console.WriteLine($"API569-style runtime stub PASS: {passed}");
        Console.WriteLine($"API569-style runtime stub FAIL: {failed}");
        return failed == 0 ? 0 : 1;
    }
}
