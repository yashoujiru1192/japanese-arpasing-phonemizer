namespace OpenUtau.Api {
    public interface IG2p {
        string[] Query(string grapheme);
        bool IsValidSymbol(string symbol);
        bool IsVowel(string symbol);
        bool IsGlide(string symbol);
        string[] UnpackHint(string hint, char separator = ' ');
    }
}
