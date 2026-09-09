using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using OpenUtau.Api;
using OpenUtau.Plugin.Builtin;

namespace JapaneseArpasingPhonemizer {
    [Phonemizer("Japanese ARPAsing Phonemizer", "JA ARPA", "Yasoujiru", "JA")]
    public sealed class JapaneseArpasingPhonemizer : LatinDiphonePhonemizer {
        private readonly JapaneseArpaG2p japaneseG2p = new JapaneseArpaG2p();

        protected override IG2p LoadG2p() {
            return new JapaneseArpaG2p();
        }

        protected override Dictionary<string, string[]> LoadVowelFallbacks() {
            return new Dictionary<string, string[]> {
                { "aa", new[] { "ah", "ae" } },
                { "ae", new[] { "aa", "ah" } },
                { "ah", new[] { "aa", "ax" } },
                { "ao", new[] { "ow", "aa" } },
                { "eh", new[] { "ae", "ey" } },
                { "ih", new[] { "iy" } },
                { "iy", new[] { "ih" } },
                { "ow", new[] { "ao" } },
                { "uh", new[] { "uw" } },
                { "uw", new[] { "uh" } }
            };
        }

        public override Result Process(
            Note[] notes,
            Note? prev,
            Note? next,
            Note? prevNeighbour,
            Note? nextNeighbour,
            Note[] prevs) {

            if (notes == null || notes.Length == 0) {
                return new Result { phonemes = Array.Empty<Phoneme>() };
            }

            Note note = notes[0];

            // Uppercase R is treated as a rest/tail marker. It emits the previous
            // symbol-to-rest alias (for example, "ow -") at the start of this note.
            // Lowercase r remains available as the normal ARPAbet consonant.
            if (KanaConverter.IsTailMarker(note.lyric) && string.IsNullOrWhiteSpace(note.phoneticHint)) {
                string[] tailPreviousSymbols = ResolvePreviousSymbols(prevNeighbour, null);
                string tailPreviousSymbol = tailPreviousSymbols.Length > 0
                    ? tailPreviousSymbols[tailPreviousSymbols.Length - 1]
                    : null;
                return MakeTailResult(note, tailPreviousSymbol);
            }

            if (string.IsNullOrEmpty(note.lyric) || note.lyric.StartsWith("?") ||
                note.lyric == "-" || !string.IsNullOrWhiteSpace(note.phoneticHint)) {
                return base.Process(notes, prev, next, prevNeighbour, nextNeighbour, prevs);
            }

            string currentLyric = note.lyric;
            string nextLyric = nextNeighbour.HasValue ? nextNeighbour.Value.lyric : null;
            string[] previousSymbols = ResolvePreviousSymbols(prevNeighbour, currentLyric);
            string previousSymbol = previousSymbols.Length > 0 ? previousSymbols[previousSymbols.Length - 1] : "-";
            string previousVowel = KanaConverter.LastVowel(previousSymbols);
            string[] currentSymbols = KanaConverter.Resolve(currentLyric, nextLyric, previousVowel);

            if (currentSymbols.Length == 0) {
                return base.Process(notes, prev, next, prevNeighbour, nextNeighbour, prevs);
            }

            // A note containing only ん or っ has no vowel. Emit its VC transition directly at
            // the start of the note. This avoids the generic diphone aligner placing it late.
            if (KanaConverter.IsSyllabicNasal(currentLyric) || KanaConverter.IsSokuon(currentLyric)) {
                return MakeStandaloneConsonantResult(note, previousSymbol, currentSymbols[0], nextNeighbour == null);
            }

            var localNotes = (Note[])notes.Clone();
            Note localCurrent = localNotes[0];

            // If the previous note is っ, that note already emitted the vowel-to-consonant VC.
            // Remove the same leading consonant here so the current note begins with consonant-to-vowel.
            if (prevNeighbour.HasValue && KanaConverter.IsSokuon(prevNeighbour.Value.lyric) &&
                string.IsNullOrWhiteSpace(prevNeighbour.Value.phoneticHint)) {
                string stop = KanaConverter.ResolveSokuonForNext(currentLyric);
                if (currentSymbols.Length > 1 && currentSymbols[0] == stop) {
                    currentSymbols = currentSymbols.Skip(1).ToArray();
                }
                previousSymbols = new[] { stop };
            }

            localCurrent.phoneticHint = string.Join(" ", currentSymbols);
            localNotes[0] = localCurrent;

            Note? localPrevNeighbour = ApplyHint(prevNeighbour, previousSymbols);
            Note? localPrev = prev;
            if (prev.HasValue && prevNeighbour.HasValue &&
                prev.Value.position == prevNeighbour.Value.position) {
                localPrev = ApplyHint(prev, previousSymbols);
            }

            Result result = base.Process(localNotes, localPrev, next, localPrevNeighbour, nextNeighbour, prevs);
            return AdjustYGlideTiming(result, note, currentSymbols);
        }

        private string[] ResolvePreviousSymbols(Note? previous, string currentLyric) {
            if (!previous.HasValue) {
                return Array.Empty<string>();
            }
            Note note = previous.Value;
            if (KanaConverter.IsTailMarker(note.lyric)) {
                return Array.Empty<string>();
            }
            if (!string.IsNullOrWhiteSpace(note.phoneticHint)) {
                return japaneseG2p.UnpackHint(note.phoneticHint);
            }
            return KanaConverter.Resolve(note.lyric, currentLyric, null);
        }

        private static Note? ApplyHint(Note? source, string[] symbols) {
            if (!source.HasValue || symbols == null || symbols.Length == 0 ||
                !string.IsNullOrWhiteSpace(source.Value.phoneticHint)) {
                return source;
            }
            Note note = source.Value;
            note.phoneticHint = string.Join(" ", symbols);
            return note;
        }

        private static Result AdjustYGlideTiming(Result result, Note note, string[] currentSymbols) {
            if (result.phonemes == null || result.phonemes.Length < 2 ||
                currentSymbols == null || currentSymbols.Length < 3 ||
                !currentSymbols.Contains("y")) {
                return result;
            }

            // Only move the actual internal transitions of the current y-glide syllable.
            // This deliberately excludes outgoing transitions and tail aliases such as
            // "aa -", "uw -" and "ow -", which must stay near the note end.
            var targetIndices = new List<int>();
            int searchFrom = 0;
            for (int pair = 0; pair < currentSymbols.Length - 1; ++pair) {
                string expected = currentSymbols[pair] + " " + currentSymbols[pair + 1];
                int found = -1;
                for (int i = searchFrom; i < result.phonemes.Length; ++i) {
                    string alias = result.phonemes[i].phoneme ?? string.Empty;
                    if (IsAliasForTransition(alias, expected)) {
                        found = i;
                        break;
                    }
                }
                if (found >= 0) {
                    targetIndices.Add(found);
                    searchFrom = found + 1;
                }
            }

            if (targetIndices.Count < 2) {
                return result;
            }

            int span = Math.Min(96, Math.Max(24, note.duration / 5));
            for (int i = 0; i < targetIndices.Count; ++i) {
                double ratio;
                if (targetIndices.Count == 1) {
                    ratio = 0.0;
                } else if (targetIndices.Count == 3 && i == 1) {
                    ratio = 0.60;
                } else {
                    ratio = (double)i / (targetIndices.Count - 1);
                }
                Phoneme phoneme = result.phonemes[targetIndices[i]];
                phoneme.position = (int)Math.Round(span * ratio);
                result.phonemes[targetIndices[i]] = phoneme;
            }
            return result;
        }

        private static bool IsAliasForTransition(string alias, string expected) {
            if (string.IsNullOrEmpty(alias)) {
                return false;
            }
            return string.Equals(alias, expected, StringComparison.Ordinal) ||
                alias.StartsWith(expected + "_", StringComparison.Ordinal);
        }

        private Result MakeTailResult(Note note, string previousSymbol) {
            if (string.IsNullOrEmpty(previousSymbol) || previousSymbol == "-") {
                return new Result { phonemes = Array.Empty<Phoneme>() };
            }

            PhonemeAttributes attr = note.phonemeAttributes != null && note.phonemeAttributes.Length > 0
                ? note.phonemeAttributes[0]
                : default(PhonemeAttributes);
            string alternate = GetAlternateCompat(attr);
            int toneShift = GetToneShiftCompat(attr);
            string voiceColor = GetVoiceColorCompat(attr);
            string tail = GetPhonemeOrFallback(
                previousSymbol,
                "-",
                note.tone + toneShift,
                voiceColor,
                alternate);

            return new Result {
                phonemes = new[] {
                    new Phoneme { phoneme = tail, position = 0 }
                }
            };
        }

        private Result MakeStandaloneConsonantResult(
            Note note,
            string previousSymbol,
            string symbol,
            bool addTail) {

            PhonemeAttributes attr = note.phonemeAttributes != null && note.phonemeAttributes.Length > 0
                ? note.phonemeAttributes[0]
                : default(PhonemeAttributes);
            string alternate = GetAlternateCompat(attr);
            int toneShift = GetToneShiftCompat(attr);
            string voiceColor = GetVoiceColorCompat(attr);
            string alias = GetPhonemeOrFallback(
                previousSymbol,
                symbol,
                note.tone + toneShift,
                voiceColor,
                alternate);

            var phonemes = new List<Phoneme> {
                new Phoneme { phoneme = alias, position = 0 }
            };
            if (addTail) {
                string tail = GetPhonemeOrFallback(
                    symbol,
                    "-",
                    note.tone + toneShift,
                    voiceColor,
                    alternate);
                phonemes.Add(new Phoneme {
                    phoneme = tail,
                    position = Math.Max(0, note.duration - 60)
                });
            }
            return new Result { phonemes = phonemes.ToArray() };
        }

        // OpenUtau 0.1.569 changed track-level phonemizer settings so some
        // PhonemeAttributes fields can be nullable. Accessing those fields directly
        // would bake their old CLR field signatures into this DLL and can make a
        // plugin compiled against 0.1.568 fail on 0.1.569. Reflection keeps this
        // small compatibility layer tolerant of both layouts.
        private int GetToneShiftCompat(PhonemeAttributes attr) {
            object value = ReadAttributeField(attr, "toneShift");
            if (value is int shift) {
                return shift;
            }
            object parent = InvokeParentSetting("GetParentToneShift");
            return parent is int parentShift ? parentShift : 0;
        }

        private string GetVoiceColorCompat(PhonemeAttributes attr) {
            object value = ReadAttributeField(attr, "voiceColor");
            if (value is string color && color != null) {
                return color;
            }
            object parent = InvokeParentSetting("GetParentVoiceColor");
            return parent as string ?? string.Empty;
        }

        private string GetAlternateCompat(PhonemeAttributes attr) {
            object value = ReadAttributeField(attr, "alternate");
            if (value is int alt) {
                return alt.ToString();
            }
            object parent = InvokeParentSetting("GetParentAlternate");
            return parent is int parentAlt ? parentAlt.ToString() : string.Empty;
        }

        private static object ReadAttributeField(PhonemeAttributes attr, string name) {
            FieldInfo field = typeof(PhonemeAttributes).GetField(name, BindingFlags.Instance | BindingFlags.Public);
            return field == null ? null : field.GetValue((object)attr);
        }

        private object InvokeParentSetting(string name) {
            MethodInfo method = typeof(Phonemizer).GetMethod(name, BindingFlags.Instance | BindingFlags.Public);
            if (method == null) {
                return null;
            }
            try {
                return method.Invoke(this, null);
            } catch {
                return null;
            }
        }
    }
}
