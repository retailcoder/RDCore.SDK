namespace RDCore.SDK.Model.Source;

// a blank is a space or a tab: the white space the parser's lexer reads between tokens (TextLines' remarks say what MS-VBAL §3.2.2 has besides).
internal static class Blanks
{
    public static readonly char[] Characters = [' ', '\t'];

    public static bool Is(char character) => character is ' ' or '\t';

    // whether a text is empty or has only blanks.
    public static bool IsAll(string text) => text.AsSpan().TrimStart(Characters).IsEmpty;
}
