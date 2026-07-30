using System;
using System.Collections.Generic;
using System.Linq;
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

            return base.Process(localNotes, localPrev, next, localPrevNeighbour, nextNeighbour, prevs);
        }

        private string[] ResolvePreviousSymbols(Note? previous, string currentLyric) {
            if (!previous.HasValue) {
                return Array.Empty<string>();
            }
            Note note = previous.Value;
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

        private Result MakeStandaloneConsonantResult(
            Note note,
            string previousSymbol,
            string symbol,
            bool addTail) {

            PhonemeAttributes attr = note.phonemeAttributes != null && note.phonemeAttributes.Length > 0
                ? note.phonemeAttributes[0]
                : default(PhonemeAttributes);
            string alternate = attr.alternate.HasValue ? attr.alternate.Value.ToString() : string.Empty;
            string alias = GetPhonemeOrFallback(
                previousSymbol,
                symbol,
                note.tone + attr.toneShift,
                attr.voiceColor,
                alternate);

            var phonemes = new List<Phoneme> {
                new Phoneme { phoneme = alias, position = 0 }
            };
            if (addTail) {
                string tail = GetPhonemeOrFallback(
                    symbol,
                    "-",
                    note.tone + attr.toneShift,
                    attr.voiceColor,
                    alternate);
                phonemes.Add(new Phoneme {
                    phoneme = tail,
                    position = Math.Max(0, note.duration - 60)
                });
            }
            return new Result { phonemes = phonemes.ToArray() };
        }
    }
}
