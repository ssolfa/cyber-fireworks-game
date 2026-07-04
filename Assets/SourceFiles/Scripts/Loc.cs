// Tiny localization helper. Loc.T(korean, english) returns the string for the
// currently selected language. Toggle Loc.EN to switch the whole game's text.
public static class Loc
{
    public static bool EN = false;
    public static string T(string ko, string en) => EN ? en : ko;
}
