using OpenUtau.Api;
using OpenUtau.Core.Ustx;

namespace JAtoKOPhonemizer;

[Phonemizer("Japanese to Korean Auto Phonemizer", "JA to KO AUTO", "ヤソウ汁", language: "JA")]
public sealed class JapaneseToKoreanPhonemizer : Phonemizer {
    private USinger? singer;
    private Dictionary<string, string[]> customMap = new(StringComparer.OrdinalIgnoreCase);

    public override void SetSinger(USinger singer) {
        this.singer = singer;
        customMap = KanaMapper.LoadCustomMap(Path.Combine(singer.Location, "ja-to-ko.map.txt"));
    }

    public override Result Process(
        Note[] notes,
        Note? prev,
        Note? next,
        Note? prevNeighbour,
        Note? nextNeighbour,
        Note[] prevNeighbours) {
        var note = notes[0];
        var duration = notes.Sum(item => item.duration);
        var attributes0 = Attributes(note, 0);

        if (!string.IsNullOrWhiteSpace(note.phoneticHint)) {
            var hinted = PickAlias(
                note.phoneticHint.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                note,
                attributes0);
            return One(hinted ?? note.phoneticHint);
        }

        if (KanaMapper.IsPassThrough(note.lyric)) {
            return One(MapExact(note.lyric, note, attributes0) ?? note.lyric);
        }

        var current = KanaMapper.Parse(note.lyric, customMap);
        var previous = prevNeighbour.HasValue
            ? KanaMapper.Parse(prevNeighbour.Value.lyric, customMap)
            : null;
        var following = nextNeighbour.HasValue
            ? KanaMapper.Parse(nextNeighbour.Value.lyric, customMap)
            : null;

        if (current is null) {
            return One(MapExact(note.lyric, note, attributes0) ?? note.lyric);
        }

        if (current.Kind == MoraKind.Nasal) {
            return One(PickAlias(KanaMapper.NasalCandidates(previous), note, attributes0) ?? "N");
        }

        if (current.Kind == MoraKind.Sokuon) {
            return One(PickAlias(KanaMapper.SokuonCandidates(previous, following), note, attributes0) ?? "R");
        }

        if (current.Kind == MoraKind.LongVowel) {
            var longVowel = KanaMapper.LongVowelCandidates(previous);
            return One(PickAlias(longVowel, note, attributes0) ?? longVowel.FirstOrDefault() ?? note.lyric);
        }

        if (current.Kind == MoraKind.Ending) {
            var ending = note.lyric.Equals("R2", StringComparison.OrdinalIgnoreCase) ? "R2" : "R";
            var endingCandidates = KanaMapper.EndingCandidates(previous, ending).ToArray();
            return One(PickAlias(endingCandidates, note, attributes0)
                ?? endingCandidates.FirstOrDefault()
                ?? ending);
        }

        var mainCandidates = KanaMapper.MainAliasCandidates(
            note.lyric,
            current,
            previous,
            prevNeighbour is null);
        var main = PickAlias(mainCandidates, note, attributes0);
        if (main is null) {
            var cPlusV = TryCPlusV(current, note, attributes0, duration, prevNeighbour is null);
            if (cPlusV is not null) {
                return cPlusV.Value;
            }
            main = current.Variants[0].Cv;
        }

        if (following is null || following.Kind != MoraKind.Syllable ||
            string.IsNullOrEmpty(following.Variants[0].Onset)) {
            return One(main);
        }

        var attributes1 = Attributes(note, 1);
        var vc = PickAlias(KanaMapper.VcCandidates(current, following), note, attributes1);
        if (vc is null) {
            return One(main);
        }

        return new Result {
            phonemes = new[] {
                new Phoneme { phoneme = main },
                new Phoneme { phoneme = vc, position = KanaMapper.VcPosition(duration) },
            },
        };
    }

    private PhonemeAttributes Attributes(Note note, int index) =>
        note.phonemeAttributes?
            .FirstOrDefault(item => PhonemeAttributeCompat.GetIndex(item) == index)
            ?? default;

    private string? PickAlias(IEnumerable<string> candidates, Note note, PhonemeAttributes attributes) {
        foreach (var candidate in candidates.Where(item => !string.IsNullOrWhiteSpace(item)).Distinct()) {
            var mapped = MapExact(candidate, note, attributes);
            if (mapped is not null) {
                return mapped;
            }
        }
        return null;
    }

    private string? MapExact(string alias, Note note, PhonemeAttributes attributes) {
        if (singer is null) {
            return null;
        }
        // Do not bind directly to PhonemeAttributes fields. Their signatures
        // differ between OpenUtau 0.1.568 and 0.1.569.
        var color = PhonemeAttributeCompat.GetVoiceColor(attributes, this);
        var shift = PhonemeAttributeCompat.GetToneShift(attributes, this);
        var alternate = PhonemeAttributeCompat.GetAlternate(attributes, this);
        if (alternate is int alternateValue && alternateValue != 0 &&
            TryGetMappedOtoWithSubbankFallback(
                alias + alternateValue,
                note.tone + shift,
                color,
                out var alternateOto)) {
            return alternateOto.Alias;
        }
        return TryGetMappedOtoWithSubbankFallback(alias, note.tone + shift, color, out var oto)
            ? oto.Alias
            : null;
    }

    private bool TryGetMappedOtoWithSubbankFallback(
        string alias,
        int tone,
        string color,
        out UOto oto) {
        oto = default!;
        if (singer is null) {
            return false;
        }
        if (singer.TryGetMappedOto(alias, tone, color, out oto)) {
            return true;
        }

        var matchingColor = singer.Subbanks
            .Where(subbank => subbank.Color == color && subbank.toneSet.Count > 0)
            .OrderBy(subbank => subbank.toneSet.Min(candidate => Math.Abs(candidate - tone)));
        IEnumerable<USubbank> mainColor = string.IsNullOrEmpty(color)
            ? Array.Empty<USubbank>()
            : singer.Subbanks
                .Where(subbank => string.IsNullOrEmpty(subbank.Color) && subbank.toneSet.Count > 0)
                .OrderBy(subbank => subbank.toneSet.Min(candidate => Math.Abs(candidate - tone)));

        foreach (var subbank in matchingColor.Concat(mainColor).Distinct()) {
            if (singer.TryGetOto($"{subbank.Prefix}{alias}{subbank.Suffix}", out oto)) {
                return true;
            }
        }
        return false;
    }

    private Result? TryCPlusV(
        Mora current,
        Note note,
        PhonemeAttributes consonantAttributes,
        int duration,
        bool phraseStart) {
        foreach (var variant in current.Variants) {
            var vowelAttributes = Attributes(note, string.IsNullOrEmpty(variant.Onset) ? 0 : 1);
            var vowel = PickAlias(
                KanaMapper.CPlusVVowelCandidates(variant, phraseStart),
                note,
                vowelAttributes);
            if (vowel is null) {
                continue;
            }
            if (string.IsNullOrEmpty(variant.Onset)) {
                return One(vowel);
            }
            var consonant = PickAlias(
                KanaMapper.CPlusVConsonantCandidates(variant, phraseStart),
                note,
                consonantAttributes);
            if (consonant is null) {
                continue;
            }
            var consonantLength = Math.Clamp(duration / 6, 30, Math.Max(30, duration / 3));
            return new Result {
                phonemes = new[] {
                    new Phoneme { phoneme = consonant },
                    new Phoneme { phoneme = vowel, position = consonantLength },
                },
            };
        }
        return null;
    }

    private static Result One(string phoneme) => new() {
        phonemes = new[] { new Phoneme { phoneme = phoneme } },
    };
}
