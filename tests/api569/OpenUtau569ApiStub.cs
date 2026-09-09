using System;
using System.Collections.Generic;

namespace OpenUtau.Api {
    public sealed class PhonemizerAttribute : Attribute {
        public PhonemizerAttribute(string name, string tag, string author = null, string language = null) { }
    }

    public interface IG2p {
        string[] Query(string grapheme);
        bool IsValidSymbol(string symbol);
        bool IsVowel(string symbol);
        bool IsGlide(string symbol);
        string[] UnpackHint(string hint, char separator = ' ');
    }

    public abstract class Phonemizer {
        public struct Note {
            public string lyric;
            public string phoneticHint;
            public int tone;
            public int position;
            public int duration;
            public PhonemeAttributes[] phonemeAttributes;
        }
        public struct PhonemeAttributes {
            public int index;
            public double? consonantStretchRatio;
            public int? toneShift;
            public int? alternate;
            public string voiceColor;
        }
        public struct Phoneme {
            public int? index;
            public string phoneme;
            public int position;
        }
        public struct Result {
            public Phoneme[] phonemes;
        }
        public abstract Result Process(Note[] notes, Note? prev, Note? next, Note? prevNeighbour, Note? nextNeighbour, Note[] prevs);
        public int GetParentToneShift() => 0;
        public int? GetParentAlternate() => null;
        public string GetParentVoiceColor() => string.Empty;
    }
}

namespace OpenUtau.Plugin.Builtin {
    using OpenUtau.Api;
    public abstract class LatinDiphonePhonemizer : Phonemizer {
        protected abstract IG2p LoadG2p();
        protected abstract Dictionary<string, string[]> LoadVowelFallbacks();
        protected virtual string GetPhonemeOrFallback(string prevSymbol, string symbol, int tone, string color, string alt) => $"{prevSymbol} {symbol}";
        public override Result Process(Note[] notes, Note? prev, Note? next, Note? prevNeighbour, Note? nextNeighbour, Note[] prevs) => new Result { phonemes = Array.Empty<Phoneme>() };
    }
}
