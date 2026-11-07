// Copyright (c) DEMA Consulting
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

// cspell:ignore Titlecase

using System.Globalization;
using System.Text;

namespace DemaConsulting.TextLint.TextScanning;

/// <summary>
///     Translates the body of a JavaScript regular expression, as cspell patterns are written, into the
///     equivalent .NET pattern, or reports the construct that cannot be translated.
/// </summary>
/// <remarks>
///     cspell evaluates its patterns with the JavaScript engine, and a project migrated from cspell must find
///     the same text excluded. The two engines differ in ways that matter: in JavaScript <c>\w</c>, <c>\d</c>,
///     and <c>\b</c> are ASCII-only, <c>\s</c> has its own list of white space, <c>.</c> stops at all four
///     line terminators unless the <c>s</c> flag is set, <c>^</c> and <c>$</c> in multiline mode recognize
///     all four, and the numbering of
///     capture groups and the meaning of many escapes differ. A pattern passed through unchanged would
///     silently mean something else. The translator is therefore a strict recursive-descent reader of the
///     JavaScript syntax that writes the .NET form of every construct it understands and rejects everything
///     else with a reason, instead of passing text through that .NET might read differently. Rejected are
///     the constructs that have no safe equivalent (named Unicode properties other than general categories,
///     inline group options, atomic groups, conditionals, octal escapes, and so on), the escapes that
///     JavaScript treats as plain letters but .NET treats as anchors or classes (<c>\z</c>, <c>\A</c>,
///     <c>\e</c>), and a back-reference to a group that is not complete. The translator never throws for bad
///     input: every problem is returned as a reason. Its work is bounded by the length limit and by a
///     nesting limit, so no pattern can exhaust the stack or the processor while it is read. The class is
///     stateless and thread-safe; each call uses its own reader.
/// </remarks>
internal static class JavaScriptRegexTranslator
{
    /// <summary>
    ///     The longest pattern body, in UTF-16 code units, that is accepted.
    /// </summary>
    /// <remarks>
    ///     Real patterns are a few dozen characters long. The limit bounds the work of reading a pattern
    ///     and the size of the regular expression that is built from it.
    /// </remarks>
    internal const int MaxPatternLength = 4096;

    /// <summary>
    ///     The deepest nesting of groups that is accepted.
    /// </summary>
    /// <remarks>
    ///     The reader recurses once per level of nesting, so a limit keeps a hostile pattern from exhausting
    ///     the stack.
    /// </remarks>
    private const int MaxNestingDepth = 100;

    /// <summary>
    ///     The highest value of a UTF-16 code unit, which bounds every class of characters.
    /// </summary>
    private const int MaxCodeUnit = 0xFFFF;

    /// <summary>
    ///     The code point of the backspace character, which is what <c>\b</c> means inside a class.
    /// </summary>
    private const int Backspace = 8;

    /// <summary>
    ///     The number of hexadecimal digits of a <c>\x</c> escape.
    /// </summary>
    private const int HexEscapeDigits = 2;

    /// <summary>
    ///     The number of hexadecimal digits of a <c>\u</c> escape.
    /// </summary>
    private const int UnicodeEscapeDigits = 4;

    /// <summary>
    ///     The mask that turns a letter into the control character that <c>\cX</c> stands for.
    /// </summary>
    private const int ControlMask = 0x1F;

    /// <summary>
    ///     The pattern text of the class of the characters that <c>.</c> matches: everything except the four
    ///     JavaScript line terminators.
    /// </summary>
    private const string DotClass = "[^\\n\\r\\u2028\\u2029]";

    /// <summary>
    ///     The pattern text of the class of the characters that <c>.</c> matches when the <c>s</c> flag is set:
    ///     every UTF-16 code unit, line terminators included.
    /// </summary>
    private const string DotAllClass = "[\\u0000-\\uFFFF]";

    /// <summary>
    ///     The pattern text of the start of a line in multiline mode: the start of the text, or the place
    ///     after a line terminator.
    /// </summary>
    private const string LineStart = "(?<![^\\n\\r\\u2028\\u2029])";

    /// <summary>
    ///     The pattern text of the end of a line in multiline mode: the end of the text, or the place before
    ///     a line terminator.
    /// </summary>
    private const string LineEnd = "(?![^\\n\\r\\u2028\\u2029])";

    /// <summary>
    ///     The pattern text of an ASCII word boundary, which is what <c>\b</c> means in JavaScript.
    /// </summary>
    private const string WordBoundary = "(?:(?<=[A-Za-z0-9_])(?![A-Za-z0-9_])|(?<![A-Za-z0-9_])(?=[A-Za-z0-9_]))";

    /// <summary>
    ///     The pattern text of the place that is not an ASCII word boundary, which is what <c>\B</c> means.
    /// </summary>
    private const string NotWordBoundary = "(?:(?<=[A-Za-z0-9_])(?=[A-Za-z0-9_])|(?<![A-Za-z0-9_])(?![A-Za-z0-9_]))";

    /// <summary>
    ///     The pattern text of a word character when the expression ignores case and has the Unicode flag, which
    ///     adds the long s U+017F and the Kelvin sign U+212A to the ASCII word characters.
    /// </summary>
    private const string FoldedWordClass = "[A-Za-z0-9_\\u017F\\u212A]";

    /// <summary>
    ///     The pattern text of an ASCII word boundary for an expression with the Unicode and case flags.
    /// </summary>
    private const string FoldedWordBoundary =
        "(?:(?<=" + FoldedWordClass + ")(?!" + FoldedWordClass + ")|(?<!" + FoldedWordClass + ")(?=" + FoldedWordClass + "))";

    /// <summary>
    ///     The pattern text of the place that is not a word boundary for an expression with the Unicode and case flags.
    /// </summary>
    private const string FoldedNotWordBoundary =
        "(?:(?<=" + FoldedWordClass + ")(?=" + FoldedWordClass + ")|(?<!" + FoldedWordClass + ")(?!" + FoldedWordClass + "))";

    /// <summary>
    ///     The characters of ASCII punctuation and digits that are written as they are in the translated pattern.
    /// </summary>
    /// <remarks>
    ///     Every other character is written as a <c>\u</c> escape, so no character can be read by .NET as
    ///     anything but itself.
    /// </remarks>
    private const string PlainCharacters = "_/-,:;<>=!@%&'\"~`";

    /// <summary>
    ///     The body of a class of the characters that JavaScript counts as white space.
    /// </summary>
    private const string SpaceBody = JavaScriptSyntax.SpaceBody;

    /// <summary>
    ///     The general categories that <c>\p{...}</c> may name, which .NET writes the same way as JavaScript.
    /// </summary>
    private static readonly HashSet<string> GeneralCategories = new(StringComparer.Ordinal)
    {
        "L", "Lu", "Ll", "Lt", "Lm", "Lo",
        "M", "Mn", "Mc", "Me",
        "N", "Nd", "Nl", "No",
        "P", "Pc", "Pd", "Ps", "Pe", "Pi", "Pf", "Po",
        "S", "Sm", "Sc", "Sk", "So",
        "Z", "Zs", "Zl", "Zp",
        "C", "Cc", "Cf", "Cs", "Co", "Cn",
    };

    /// <summary>
    ///     The ranges of the ASCII digits, which is what <c>\d</c> means.
    /// </summary>
    private static readonly (int Low, int High)[] DigitRanges = [('0', '9')];

    /// <summary>
    ///     The body of a class of the ASCII digits, which is what <c>\d</c> means.
    /// </summary>
    private static readonly string DigitBody = ClassBody(DigitRanges);

    /// <summary>
    ///     The body of a class of every code unit except the ASCII digits, which is what <c>\D</c> means.
    /// </summary>
    private static readonly string NotDigitBody = ClassBody(Complement(DigitRanges));

    /// <summary>
    ///     The ranges of the ASCII word characters, which is what <c>\w</c> means.
    /// </summary>
    private static readonly (int Low, int High)[] WordRanges = [('0', '9'), ('A', 'Z'), ('_', '_'), ('a', 'z')];

    /// <summary>
    ///     The body of a class of the ASCII word characters.
    /// </summary>
    private static readonly string WordBody = ClassBody(WordRanges);

    /// <summary>
    ///     The body of a class of every code unit except the ASCII word characters.
    /// </summary>
    private static readonly string NotWordBody = ClassBody(Complement(WordRanges));

    /// <summary>
    ///     The ranges of the characters that JavaScript counts as white space, which is what <c>\s</c> means.
    /// </summary>
    private static readonly (int Low, int High)[] SpaceRanges =
    [
        (0x09, 0x0D), (0x20, 0x20), (0xA0, 0xA0), (0x1680, 0x1680), (0x2000, 0x200A),
        (0x2028, 0x2029), (0x202F, 0x202F), (0x205F, 0x205F), (0x3000, 0x3000), (0xFEFF, 0xFEFF),
    ];

    /// <summary>
    ///     The body of a class of every code unit that JavaScript does not count as white space.
    /// </summary>
    private static readonly string NotSpaceBody = ClassBody(Complement(SpaceRanges));

    /// <summary>
    ///     Translates the body of a JavaScript regular expression to a .NET pattern.
    /// </summary>
    /// <remarks>
    ///     Failure is data: nothing is thrown for a bad pattern, so the caller can report the reason with
    ///     the position of the entry it came from. The translated pattern must be built without the case
    ///     option and without the multiline option, because the translation already
    ///     spells out what the <c>i</c> flag, <c>^</c>, and <c>$</c> mean. Stateless and thread-safe.
    /// </remarks>
    /// <param name="source">The pattern body, without the slashes and flags. Must not be null.</param>
    /// <param name="multiline"><see langword="true"/> when the <c>m</c> flag is set.</param>
    /// <param name="unicode"><see langword="true"/> when the <c>u</c> flag is set.</param>
    /// <param name="dotAll"><see langword="true"/> when the <c>s</c> flag is set, so that <c>.</c> also matches line terminators.</param>
    /// <param name="ignoreCase">
    ///     <see langword="true"/> when the <c>i</c> flag is set; the translation then spells out which characters
    ///     count as the same letter, and the .NET case-insensitive option must not be used.
    /// </param>
    /// <param name="translated">The .NET pattern when the method returns true; otherwise an empty string.</param>
    /// <param name="problem">The reason, as a sentence, when the method returns false; otherwise null.</param>
    /// <returns><see langword="true"/> when the pattern was translated.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is null.</exception>
    internal static bool TryTranslate(
        string source,
        bool multiline,
        bool unicode,
        bool dotAll,
        bool ignoreCase,
        out string translated,
        out string? problem)
    {
        ArgumentNullException.ThrowIfNull(source);
        translated = string.Empty;
        problem = null;

        // Reject what cannot be a pattern before reading any of it
        if (source.Length == 0)
        {
            problem = "the pattern is empty.";
            return false;
        }

        if (source.Length > MaxPatternLength)
        {
            problem = $"the pattern is longer than {MaxPatternLength} characters.";
            return false;
        }

        // Read the pattern; the reader records the reason itself when it stops
        var reader = new Reader(source, multiline, unicode, dotAll, ignoreCase);
        if (!reader.Run())
        {
            problem = reader.Problem;
            return false;
        }

        translated = reader.Result;
        return true;
    }

    /// <summary>
    ///     Writes a list of inclusive ranges of code units as the body of a .NET character class.
    /// </summary>
    /// <remarks>
    ///     Every code unit is written as a <c>\u</c> escape so that no character can be read as class
    ///     syntax. Stateless and thread-safe.
    /// </remarks>
    /// <param name="ranges">The inclusive ranges, in ascending order. Must not be null.</param>
    /// <returns>The class body, without the brackets.</returns>
    private static string ClassBody(IEnumerable<(int Low, int High)> ranges)
    {
        var body = new StringBuilder();
        foreach (var (low, high) in ranges)
        {
            AppendCodeUnit(body, low);
            if (high != low)
            {
                body.Append('-');
                AppendCodeUnit(body, high);
            }
        }

        return body.ToString();
    }

    /// <summary>
    ///     Computes the ranges of the code units that are not in the given ranges.
    /// </summary>
    /// <remarks>
    ///     Needed because a negated shorthand class such as <c>\W</c> written inside a bracketed class has
    ///     no .NET spelling with the JavaScript meaning, so it is written out as explicit ranges. Stateless
    ///     and thread-safe.
    /// </remarks>
    /// <param name="ranges">The inclusive ranges, in ascending order and not overlapping. Must not be null.</param>
    /// <returns>The inclusive ranges between and around them, within the range of a UTF-16 code unit.</returns>
    private static List<(int Low, int High)> Complement(IEnumerable<(int Low, int High)> ranges)
    {
        var result = new List<(int Low, int High)>();
        var next = 0;
        foreach (var (low, high) in ranges)
        {
            if (low > next)
            {
                result.Add((next, low - 1));
            }

            next = high + 1;
        }

        if (next <= MaxCodeUnit)
        {
            result.Add((next, MaxCodeUnit));
        }

        return result;
    }

    /// <summary>
    ///     Appends one UTF-16 code unit as a <c>\u</c> escape.
    /// </summary>
    /// <remarks>
    ///     Stateless and thread-safe.
    /// </remarks>
    /// <param name="builder">The builder to append to. Must not be null.</param>
    /// <param name="codeUnit">The code unit, from 0 to 0xFFFF.</param>
    private static void AppendCodeUnit(StringBuilder builder, int codeUnit) =>
        builder.Append("\\u").Append(codeUnit.ToString("X4", CultureInfo.InvariantCulture));

    /// <summary>
    ///     Reads one pattern and writes its translation.
    /// </summary>
    /// <remarks>
    ///     A reader is created per call and never shared, which is what makes the translator thread-safe.
    ///     Every method that reads returns whether it succeeded; the first failure records its reason in
    ///     <see cref="Problem"/> and every caller stops, so the reason is always the first problem.
    /// </remarks>
    private sealed class Reader
    {
        /// <summary>
        ///     The pattern being read.
        /// </summary>
        private readonly string _source;

        /// <summary>
        ///     Whether the multiline flag is set.
        /// </summary>
        private readonly bool _multiline;

        /// <summary>
        ///     Whether the Unicode flag is set.
        /// </summary>
        private readonly bool _unicode;

        /// <summary>
        ///     Whether the dot-all flag is set.
        /// </summary>
        private readonly bool _dotAll;

        /// <summary>
        ///     Whether the case flag is set.
        /// </summary>
        private readonly bool _ignoreCase;

        /// <summary>
        ///     The ranges of the class being read, as sets of code units, when the case flag is set.
        /// </summary>
        private readonly List<(int Low, int High)> _classSet = [];

        /// <summary>
        ///     The ranges of the set that the atom just read stands for, when the case flag is set; null for a single character.
        /// </summary>
        private List<(int Low, int High)>? _atomSet;

        /// <summary>
        ///     The set that the property escape just read stands for, when the case flag is set.
        /// </summary>
        private List<(int Low, int High)> _propertySet = [];

        /// <summary>
        ///     The translation written so far.
        /// </summary>
        private readonly StringBuilder _output = new();

        /// <summary>
        ///     The groups that have been closed, by number.
        /// </summary>
        private readonly HashSet<int> _closedGroups = [];

        /// <summary>
        ///     The names of the named groups opened so far, with their numbers.
        /// </summary>
        private readonly Dictionary<string, int> _groupNames = new(StringComparer.Ordinal);

        /// <summary>
        ///     The offset of the next character to read.
        /// </summary>
        private int _position;

        /// <summary>
        ///     The number of capturing groups opened so far.
        /// </summary>
        private int _openedGroups;

        /// <summary>
        ///     The number of capturing groups in the whole pattern.
        /// </summary>
        private int _totalGroups;

        /// <summary>
        ///     Whether the pattern has a named group anywhere.
        /// </summary>
        private bool _hasNamedGroups;

        /// <summary>
        ///     The current depth of nested groups.
        /// </summary>
        private int _depth;

        /// <summary>
        ///     Initializes a new instance of the <see cref="Reader"/> class.
        /// </summary>
        /// <remarks>
        ///     Cheap: nothing is read until <see cref="Run"/>.
        /// </remarks>
        /// <param name="source">The pattern body. Must not be null.</param>
        /// <param name="multiline"><see langword="true"/> when the <c>m</c> flag is set.</param>
        /// <param name="unicode"><see langword="true"/> when the <c>u</c> flag is set.</param>
        /// <param name="dotAll"><see langword="true"/> when the <c>s</c> flag is set.</param>
        /// <param name="ignoreCase"><see langword="true"/> when the <c>i</c> flag is set.</param>
        internal Reader(string source, bool multiline, bool unicode, bool dotAll, bool ignoreCase)
        {
            _source = source;
            _multiline = multiline;
            _unicode = unicode;
            _dotAll = dotAll;
            _ignoreCase = ignoreCase;
        }

        /// <summary>
        ///     Gets the reason the pattern was rejected.
        /// </summary>
        /// <value>The reason as a sentence; null while no problem has been found.</value>
        internal string? Problem { get; private set; }

        /// <summary>
        ///     Gets the translation.
        /// </summary>
        /// <value>The .NET pattern; meaningful only when <see cref="Run"/> returned true.</value>
        internal string Result => _output.ToString();

        /// <summary>
        ///     Reads the whole pattern.
        /// </summary>
        /// <remarks>
        ///     First counts the capturing groups, because the meaning of a decimal escape depends on how
        ///     many exist, then reads the pattern and requires that nothing is left over.
        /// </remarks>
        /// <returns><see langword="true"/> when the pattern was read and translated.</returns>
        internal bool Run()
        {
            CountGroups();
            if (!ReadDisjunction())
            {
                return false;
            }

            // The only thing that can stop the reading before the end is a closing parenthesis with no opening one
            return _position >= _source.Length || Fail("a ')' has no matching '('.");
        }

        /// <summary>
        ///     Records the reason the pattern is rejected and reports failure.
        /// </summary>
        /// <remarks>
        ///     Only the first reason is kept. Side effect: sets <see cref="Problem"/>.
        /// </remarks>
        /// <param name="reason">The reason as a sentence. Must not be null.</param>
        /// <returns>Always <see langword="false"/>, so a caller can write <c>return Fail(...)</c>.</returns>
        private bool Fail(string reason)
        {
            Problem ??= reason;
            return false;
        }

        /// <summary>
        ///     Counts the capturing groups of the whole pattern and notes whether any is named.
        /// </summary>
        /// <remarks>
        ///     A group opens at a parenthesis that is not escaped, not inside a bracketed class, and is not
        ///     a non-capturing or look-around group. Side effects: sets <see cref="_totalGroups"/> and
        ///     <see cref="_hasNamedGroups"/>.
        /// </remarks>
        private void CountGroups()
        {
            var index = 0;
            while (index < _source.Length)
            {
                var character = _source[index];
                if (character == '\\')
                {
                    // An escape covers the next character, which may be a parenthesis or a bracket
                    index += 2;
                }
                else if (character == '[')
                {
                    // A class ends at the first closing bracket that is not escaped
                    index++;
                    while (index < _source.Length && _source[index] != ']')
                    {
                        index += _source[index] == '\\' ? 2 : 1;
                    }

                    index++;
                }
                else
                {
                    if (character == '(')
                    {
                        CountGroupAt(index);
                    }

                    index++;
                }
            }
        }

        /// <summary>
        ///     Counts the group that opens at a parenthesis, if it is a capturing group.
        /// </summary>
        /// <remarks>
        ///     Side effects: updates <see cref="_totalGroups"/> and <see cref="_hasNamedGroups"/>.
        /// </remarks>
        /// <param name="index">The offset of the opening parenthesis.</param>
        private void CountGroupAt(int index)
        {
            var isPlain = index + 1 >= _source.Length || _source[index + 1] != '?';
            var isNamed = !isPlain &&
                          index + 3 < _source.Length &&
                          _source[index + 2] == '<' &&
                          _source[index + 3] is not ('=' or '!');
            if (isPlain || isNamed)
            {
                _totalGroups++;
            }

            if (isNamed)
            {
                _hasNamedGroups = true;
            }
        }

        /// <summary>
        ///     Reads alternatives separated by <c>|</c> until a closing parenthesis or the end.
        /// </summary>
        /// <returns><see langword="true"/> when the alternatives were read.</returns>
        private bool ReadDisjunction()
        {
            while (true)
            {
                if (!ReadAlternative())
                {
                    return false;
                }

                if (_position < _source.Length && _source[_position] == '|')
                {
                    _output.Append('|');
                    _position++;
                    continue;
                }

                return true;
            }
        }

        /// <summary>
        ///     Reads terms until an alternative ends.
        /// </summary>
        /// <returns><see langword="true"/> when the terms were read.</returns>
        private bool ReadAlternative()
        {
            while (_position < _source.Length && _source[_position] is not ('|' or ')'))
            {
                if (!ReadTerm())
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        ///     Reads one term: an assertion or an atom, with its quantifier.
        /// </summary>
        /// <remarks>
        ///     Every branch advances the position, so the reading always ends.
        /// </remarks>
        /// <returns><see langword="true"/> when the term was read.</returns>
        private bool ReadTerm()
        {
            var character = _source[_position];
            var canRepeat = true;
            switch (character)
            {
                case '^':
                    _position++;
                    _output.Append(_multiline ? LineStart : "\\A");
                    canRepeat = false;
                    break;

                case '$':
                    _position++;
                    _output.Append(_multiline ? LineEnd : "\\z");
                    canRepeat = false;
                    break;

                case '(':
                    if (!ReadGroup(out canRepeat))
                    {
                        return false;
                    }

                    break;

                case '.':
                    _position++;
                    _output.Append(_dotAll ? DotAllClass : DotClass);
                    break;

                case '[':
                    if (!ReadClass())
                    {
                        return false;
                    }

                    break;

                case '\\':
                    if (!ReadEscape(out canRepeat))
                    {
                        return false;
                    }

                    break;

                case '*' or '+' or '?':
                    return Fail($"nothing comes before the quantifier '{character}'.");

                case '{' or '}' or ']':
                    if (!ReadBraceOrBracketLiteral(character))
                    {
                        return false;
                    }

                    break;

                default:
                    AppendLiteralAtPosition();
                    break;
            }

            return ReadQuantifier(canRepeat);
        }

        /// <summary>
        ///     Reads a brace or closing bracket where an atom is expected, which is a literal character.
        /// </summary>
        /// <remarks>
        ///     JavaScript accepts these as plain characters without the Unicode flag, unless an opening brace
        ///     starts a quantifier with nothing before it.
        /// </remarks>
        /// <param name="character">The character at the position: <c>{</c>, <c>}</c>, or <c>]</c>.</param>
        /// <returns><see langword="true"/> when the character was read as a literal.</returns>
        private bool ReadBraceOrBracketLiteral(char character)
        {
            if (character == '{' && TryReadBraces(_position, out _, out _, out _))
            {
                return Fail("nothing comes before the quantifier '{'.");
            }

            if (_unicode)
            {
                return Fail($"a lone '{character}' must be escaped when the u flag is set.");
            }

            AppendLiteral(character);
            _position++;
            return true;
        }

        /// <summary>
        ///     Reads a quantifier that follows an atom, if there is one, and checks that it may be there.
        /// </summary>
        /// <param name="canRepeat"><see langword="false"/> when the atom is an assertion that cannot be repeated.</param>
        /// <returns><see langword="true"/> when there is no quantifier or it was read.</returns>
        private bool ReadQuantifier(bool canRepeat)
        {
            if (_position >= _source.Length)
            {
                return true;
            }

            // Recognize the three single-character quantifiers and the brace form
            string quantifier;
            var character = _source[_position];
            if (character is '*' or '+' or '?')
            {
                quantifier = character.ToString();
                _position++;
            }
            else if (character == '{' && TryReadBraces(_position, out var end, out var text, out var problem))
            {
                if (problem is not null)
                {
                    return Fail(problem);
                }

                quantifier = text;
                _position = end;
            }
            else
            {
                return true;
            }

            if (!canRepeat)
            {
                return Fail("an assertion cannot be repeated.");
            }

            // A trailing question mark makes the quantifier lazy
            _output.Append(quantifier);
            if (_position < _source.Length && _source[_position] == '?')
            {
                _output.Append('?');
                _position++;
            }

            // A second quantifier in a row has nothing to repeat
            if (_position < _source.Length &&
                (_source[_position] is '*' or '+' or '?' ||
                 (_source[_position] == '{' && TryReadBraces(_position, out _, out _, out _))))
            {
                return Fail($"nothing comes before the quantifier '{_source[_position]}'.");
            }

            return true;
        }

        /// <summary>
        ///     Tries to read a brace quantifier (<c>{n}</c>, <c>{n,}</c>, or <c>{n,m}</c>) at an offset.
        /// </summary>
        /// <remarks>
        ///     A brace that does not form a quantifier is a literal character without the Unicode flag, so a
        ///     failure to match is not an error. A quantifier whose numbers are out of order or too large is
        ///     matched, with a problem. Stateless.
        /// </remarks>
        /// <param name="start">The offset of the opening brace.</param>
        /// <param name="end">The offset just past the closing brace when the method returns true.</param>
        /// <param name="text">The quantifier as written when the method returns true.</param>
        /// <param name="problem">The reason the numbers are unusable, or null when they are fine.</param>
        /// <returns><see langword="true"/> when a brace quantifier starts at the offset.</returns>
        private bool TryReadBraces(int start, out int end, out string text, out string? problem)
        {
            end = start;
            text = string.Empty;
            problem = null;

            // The minimum is one or more digits
            var index = start + 1;
            var minimumStart = index;
            while (index < _source.Length && char.IsAsciiDigit(_source[index]))
            {
                index++;
            }

            if (index == minimumStart || index >= _source.Length)
            {
                return false;
            }

            var minimumText = _source[minimumStart..index];
            string? maximumText = null;
            var hasComma = false;
            if (_source[index] == ',')
            {
                hasComma = true;
                index++;
                var maximumStart = index;
                while (index < _source.Length && char.IsAsciiDigit(_source[index]))
                {
                    index++;
                }

                maximumText = _source[maximumStart..index];
                if (index >= _source.Length)
                {
                    return false;
                }
            }

            if (_source[index] != '}')
            {
                return false;
            }

            end = index + 1;
            text = _source[start..end];

            // Check the numbers: they must fit and be in order
            var minimumFits = int.TryParse(minimumText, NumberStyles.None, CultureInfo.InvariantCulture, out var minimum);
            var maximumFits = true;
            var maximum = int.MaxValue;
            if (hasComma && !string.IsNullOrEmpty(maximumText))
            {
                maximumFits = int.TryParse(maximumText, NumberStyles.None, CultureInfo.InvariantCulture, out maximum);
            }

            if (!minimumFits || !maximumFits)
            {
                problem = $"the quantifier '{text}' has a number that is too large.";
            }
            else if (minimum > maximum)
            {
                problem = $"the numbers in the quantifier '{text}' are out of order.";
            }

            return true;
        }

        /// <summary>
        ///     Reads a group, from its opening parenthesis to its closing one.
        /// </summary>
        /// <remarks>
        ///     Supported are capturing groups, non-capturing groups, named groups, and the four look-around
        ///     assertions. Any other group syntax is rejected, because .NET gives <c>(?...)</c> forms meanings
        ///     that JavaScript does not have.
        /// </remarks>
        /// <param name="canRepeat">
        ///     Receives whether a quantifier may follow: always for a group, for a look-ahead only without
        ///     the Unicode flag, and never for a look-behind.
        /// </param>
        /// <returns><see langword="true"/> when the group was read.</returns>
        private bool ReadGroup(out bool canRepeat)
        {
            canRepeat = true;
            _position++;
            if (++_depth > MaxNestingDepth)
            {
                return Fail($"groups are nested more than {MaxNestingDepth} levels deep.");
            }

            // Decide what kind of group it is and write its opening
            var captureNumber = 0;
            if (Consume("?:"))
            {
                _output.Append("(?:");
            }
            else if (Consume("?="))
            {
                _output.Append("(?=");
                canRepeat = !_unicode;
            }
            else if (Consume("?!"))
            {
                _output.Append("(?!");
                canRepeat = !_unicode;
            }
            else if (Consume("?<="))
            {
                _output.Append("(?<=");
                canRepeat = false;
            }
            else if (Consume("?<!"))
            {
                _output.Append("(?<!");
                canRepeat = false;
            }
            else if (Consume("?<"))
            {
                if (!ReadGroupName(out captureNumber))
                {
                    return false;
                }

                _output.Append('(');
            }
            else if (_position < _source.Length && _source[_position] == '?')
            {
                var shown = _source[_position..Math.Min(_source.Length, _position + 3)];
                return Fail($"the group syntax '({shown}' is not supported.");
            }
            else
            {
                captureNumber = ++_openedGroups;
                _output.Append('(');
            }

            // Read the content, then the closing parenthesis
            if (!ReadDisjunction())
            {
                return false;
            }

            if (_position >= _source.Length)
            {
                return Fail("a '(' is not closed.");
            }

            _position++;
            _output.Append(')');
            if (captureNumber > 0)
            {
                _closedGroups.Add(captureNumber);
            }

            _depth--;
            return true;
        }

        /// <summary>
        ///     Reads the name of a named group, after the opening <c>(?&lt;</c>, and numbers the group.
        /// </summary>
        /// <remarks>
        ///     Names are limited to ASCII letters, digits, <c>_</c>, and <c>$</c>, and may not start with a
        ///     digit. Side effects: counts the group and records its name.
        /// </remarks>
        /// <param name="number">Receives the number of the group.</param>
        /// <returns><see langword="true"/> when the name is acceptable.</returns>
        private bool ReadGroupName(out int number)
        {
            number = 0;
            var close = _source.IndexOf('>', _position);
            if (close < 0)
            {
                return Fail("a group name is not closed with '>'.");
            }

            var name = _source[_position..close];
            var isValid = name.Length > 0 &&
                          !char.IsAsciiDigit(name[0]) &&
                          name.All(static character => char.IsAsciiLetterOrDigit(character) || character is '_' or '$');
            if (!isValid)
            {
                return Fail($"the group name '{name}' is not supported; use ASCII letters, digits, '_' and '$'.");
            }

            if (_groupNames.ContainsKey(name))
            {
                return Fail($"the group name '{name}' is used twice.");
            }

            number = ++_openedGroups;
            _groupNames[name] = number;
            _position = close + 1;
            return true;
        }

        /// <summary>
        ///     Consumes a fixed text at the position if it is there.
        /// </summary>
        /// <remarks>
        ///     Side effect: advances the position on success.
        /// </remarks>
        /// <param name="text">The text to consume. Must not be null.</param>
        /// <returns><see langword="true"/> when the text was there and was consumed.</returns>
        private bool Consume(string text)
        {
            if (string.CompareOrdinal(_source, _position, text, 0, text.Length) != 0)
            {
                return false;
            }

            _position += text.Length;
            return true;
        }

        /// <summary>
        ///     Reads an escape outside a class: a position assertion, a class, a character, or a back-reference.
        /// </summary>
        /// <param name="canRepeat">Receives whether a quantifier may follow: false for <c>\b</c> and <c>\B</c>.</param>
        /// <returns><see langword="true"/> when the escape was read.</returns>
        private bool ReadEscape(out bool canRepeat)
        {
            canRepeat = true;
            _position++;
            if (_position >= _source.Length)
            {
                return Fail("the pattern ends with a backslash.");
            }

            var character = _source[_position];
            switch (character)
            {
                case 'b':
                    _position++;
                    _output.Append(_ignoreCase && _unicode ? FoldedWordBoundary : WordBoundary);
                    canRepeat = false;
                    return true;

                case 'B':
                    _position++;
                    _output.Append(_ignoreCase && _unicode ? FoldedNotWordBoundary : NotWordBoundary);
                    canRepeat = false;
                    return true;

                case 'd' or 'D' or 'w' or 'W' or 's' or 'S':
                    _position++;
                    if (_ignoreCase)
                    {
                        AppendSet(ShorthandSet(character));
                        return true;
                    }

                    _output.Append(character is 'd' or 'w' or 's' ? "[" : "[^");
                    _output.Append(ShorthandBody(character));
                    _output.Append(']');
                    return true;

                case 'k':
                    return ReadNamedReference();

                case >= '1' and <= '9':
                    return ReadNumberedReference();

                case 'p' or 'P':
                    return ReadProperty();

                default:
                    return ReadCharacterEscape(inClass: false, out _);
            }
        }

        /// <summary>
        ///     Gets the class body that a shorthand escape stands for.
        /// </summary>
        /// <remarks>
        ///     The body of a negated shorthand (<c>D</c>, <c>W</c>, <c>S</c>) is that of the positive class
        ///     as the caller wraps it in a negated class, so this returns the positive body for both.
        ///     Stateless.
        /// </remarks>
        /// <param name="shorthand">One of <c>d</c>, <c>D</c>, <c>w</c>, <c>W</c>, <c>s</c>, <c>S</c>.</param>
        /// <returns>The class body of the positive class.</returns>
        private static string ShorthandBody(char shorthand) => AsciiLower(shorthand) switch
        {
            'd' => DigitBody,
            'w' => WordBody,
            _ => SpaceBody,
        };

        /// <summary>
        ///     Gets the lower-case form of an ASCII letter, by arithmetic and not by a casing function.
        /// </summary>
        /// <remarks>
        ///     The casing functions of .NET follow the Unicode data of the runtime; the shorthand letters are ASCII, so
        ///     the translation path does not need them. Stateless.
        /// </remarks>
        /// <param name="letter">An ASCII letter.</param>
        /// <returns>The lower-case letter.</returns>
        private static char AsciiLower(char letter) => char.IsAsciiLetterUpper(letter) ? (char)(letter + ('a' - 'A')) : letter;

        /// <summary>
        ///     Gets the set of code units that a shorthand escape matches when the expression ignores case.
        /// </summary>
        /// <remarks>
        ///     The set is closed under the letters that JavaScript treats as the same. For <c>\w</c> that adds the
        ///     long s and the Kelvin sign with the Unicode flag. A negated shorthand is the complement of the
        ///     closed positive set, as the specification defines it, so <c>\W</c> does not match <c>s</c> through
        ///     the long s. Stateless apart from the flag.
        /// </remarks>
        /// <param name="shorthand">One of <c>d</c>, <c>D</c>, <c>w</c>, <c>W</c>, <c>s</c>, <c>S</c>.</param>
        /// <returns>The ranges of the set, in ascending order and not overlapping.</returns>
        private List<(int Low, int High)> ShorthandSet(char shorthand)
        {
            var positive = JavaScriptCaseFolding.Close(
                AsciiLower(shorthand) switch
                {
                    'd' => DigitRanges,
                    'w' => WordRanges,
                    _ => SpaceRanges,
                },
                _unicode);
            return char.IsAsciiLetterLower(shorthand) ? positive : JavaScriptCaseFolding.Close(Complement(positive), _unicode);
        }

        /// <summary>
        ///     Writes a set of code units as a class, or as a pattern that matches nothing when the set is empty.
        /// </summary>
        /// <remarks>
        ///     Side effect: appends to the output.
        /// </remarks>
        /// <param name="ranges">The ranges of the set, in ascending order and not overlapping. Must not be null.</param>
        private void AppendSet(IReadOnlyCollection<(int Low, int High)> ranges)
        {
            if (ranges.Count == 0)
            {
                _output.Append("(?!)");
                return;
            }

            _output.Append('[').Append(ClassBody(ranges)).Append(']');
        }

        /// <summary>
        ///     Reads a back-reference to a numbered group, such as <c>\1</c>.
        /// </summary>
        /// <remarks>
        ///     A reference to a group that does not exist is reported, where JavaScript would read an octal
        ///     escape. A reference to a group that is not complete yet is reported too, because the two
        ///     engines disagree on what it matches.
        /// </remarks>
        /// <returns><see langword="true"/> when the reference was read.</returns>
        private bool ReadNumberedReference()
        {
            var start = _position;
            while (_position < _source.Length && char.IsAsciiDigit(_source[_position]))
            {
                _position++;
            }

            var text = _source[start.._position];
            if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number > _totalGroups)
            {
                return Fail($"the escape '\\{text}' refers to a group that does not exist.");
            }

            return AppendReference(number, $"\\{text}");
        }

        /// <summary>
        ///     Reads a back-reference to a named group, <c>\k&lt;name&gt;</c>.
        /// </summary>
        /// <remarks>
        ///     Without any named group in the pattern JavaScript reads <c>\k</c> as a plain letter, which is
        ///     almost certainly not what the author meant, so it is reported.
        /// </remarks>
        /// <returns><see langword="true"/> when the reference was read.</returns>
        private bool ReadNamedReference()
        {
            _position++;
            if (!_hasNamedGroups)
            {
                return Fail("the escape '\\k' is only supported to refer to a named group.");
            }

            if (_position >= _source.Length || _source[_position] != '<' || _source.IndexOf('>', _position) < 0)
            {
                return Fail("the escape '\\k' must be followed by a group name in angle brackets.");
            }

            var close = _source.IndexOf('>', _position);
            var name = _source[(_position + 1)..close];
            _position = close + 1;
            if (!_groupNames.TryGetValue(name, out var number))
            {
                return Fail($"the escape '\\k<{name}>' refers to a group that is not defined before it.");
            }

            return AppendReference(number, $"\\k<{name}>");
        }

        /// <summary>
        ///     Writes a back-reference to a group, after checking that the group is complete.
        /// </summary>
        /// <remarks>
        ///     The reference is written as <c>\k&lt;n&gt;</c> inside a non-capturing group so that a digit that
        ///     follows it cannot become part of the number. Every capturing group is written as a plain
        ///     group, so .NET numbers the groups exactly as JavaScript does. Side effect: appends to the output.
        /// </remarks>
        /// <param name="number">The number of the group.</param>
        /// <param name="shown">The escape as written, for the message.</param>
        /// <returns><see langword="true"/> when the reference was written.</returns>
        private bool AppendReference(int number, string shown)
        {
            if (!_closedGroups.Contains(number))
            {
                return Fail($"the escape '{shown}' refers to a group that is not complete at that point.");
            }

            if (_ignoreCase)
            {
                // .NET could only compare the text with its own case rules, which depend on the runtime and differ from
                // JavaScript, so the combination is rejected instead of being given a meaning that varies
                return Fail($"the escape '{shown}' cannot be used with the flag 'i', because a back-reference would " +
                            "compare letters by the case rules of .NET instead of those of JavaScript; remove the flag " +
                            "or the back-reference.");
            }

            _output.Append("(?:\\k<").Append(number.ToString(CultureInfo.InvariantCulture)).Append(">)");
            return true;
        }

        /// <summary>
        ///     Reads the escape of a Unicode property, <c>\p{L}</c> or <c>\P{L}</c>.
        /// </summary>
        /// <remarks>
        ///     Without the Unicode flag JavaScript reads <c>\p</c> as a plain letter, which is not what the
        ///     author meant, so it is reported. With the flag only the general categories are supported,
        ///     because .NET has no equivalent of the other properties such as scripts.
        /// </remarks>
        /// <returns><see langword="true"/> when the escape was read and written.</returns>
        private bool ReadProperty()
        {
            var negated = _source[_position] == 'P';
            _position++;
            if (!_unicode)
            {
                return Fail($"the escape '\\{(negated ? 'P' : 'p')}' needs the u flag; write the pattern as /.../u.");
            }

            var close = _position < _source.Length && _source[_position] == '{' ? _source.IndexOf('}', _position) : -1;
            if (close < 0)
            {
                return Fail("a Unicode property must be written like \\p{L}.");
            }

            var name = _source[(_position + 1)..close];
            if (!GeneralCategories.Contains(name))
            {
                return Fail($"the Unicode property '{name}' is not supported; only general categories such as \\p{{L}} are.");
            }

            _position = close + 1;
            if (_ignoreCase)
            {
                // The class is written out, so that its letters follow the JavaScript rules for ignoring case
                _propertySet = JavaScriptCaseFolding.Close(
                    negated ? Complement(CategoryRanges(name)) : CategoryRanges(name),
                    _unicode);
                AppendSet(_propertySet);
                return true;
            }

            _output.Append(negated ? "\\P{" : "\\p{").Append(name).Append('}');
            return true;
        }

        /// <summary>
        ///     Gets the ranges of the code units that belong to a general category, or to a group of categories.
        /// </summary>
        /// <remarks>
        ///     Taken from the character data of .NET, which is also what <c>\p{...}</c> means in .NET, so the set
        ///     is the one the pattern would match without the case flag. Only the basic plane is covered, as in
        ///     .NET, where a code unit of a surrogate pair is a character of the category <c>Cs</c>. Stateless.
        /// </remarks>
        /// <param name="name">The name of the category, such as <c>L</c> or <c>Lu</c>.</param>
        /// <returns>The ranges, in ascending order and not overlapping.</returns>
        private static List<(int Low, int High)> CategoryRanges(string name)
        {
            var ranges = new List<(int Low, int High)>();
            var start = -1;
            for (var unit = 0; unit <= MaxCodeUnit; unit++)
            {
                var code = CategoryCode(char.GetUnicodeCategory((char)unit));
                var inside = code == name || (name.Length == 1 && code[0] == name[0]);
                if (inside && start < 0)
                {
                    start = unit;
                }
                else if (!inside && start >= 0)
                {
                    ranges.Add((start, unit - 1));
                    start = -1;
                }
            }

            if (start >= 0)
            {
                ranges.Add((start, MaxCodeUnit));
            }

            return ranges;
        }

        /// <summary>
        ///     Gets the two-letter code of a general category, as <c>\p{...}</c> writes it.
        /// </summary>
        /// <remarks>
        ///     Stateless.
        /// </remarks>
        /// <param name="category">The category.</param>
        /// <returns>The code, such as <c>Lu</c>.</returns>
        private static string CategoryCode(UnicodeCategory category) => category switch
        {
            UnicodeCategory.UppercaseLetter => "Lu",
            UnicodeCategory.LowercaseLetter => "Ll",
            UnicodeCategory.TitlecaseLetter => "Lt",
            UnicodeCategory.ModifierLetter => "Lm",
            UnicodeCategory.OtherLetter => "Lo",
            UnicodeCategory.NonSpacingMark => "Mn",
            UnicodeCategory.SpacingCombiningMark => "Mc",
            UnicodeCategory.EnclosingMark => "Me",
            UnicodeCategory.DecimalDigitNumber => "Nd",
            UnicodeCategory.LetterNumber => "Nl",
            UnicodeCategory.OtherNumber => "No",
            UnicodeCategory.SpaceSeparator => "Zs",
            UnicodeCategory.LineSeparator => "Zl",
            UnicodeCategory.ParagraphSeparator => "Zp",
            UnicodeCategory.Control => "Cc",
            UnicodeCategory.Format => "Cf",
            UnicodeCategory.Surrogate => "Cs",
            UnicodeCategory.PrivateUse => "Co",
            UnicodeCategory.ConnectorPunctuation => "Pc",
            UnicodeCategory.DashPunctuation => "Pd",
            UnicodeCategory.OpenPunctuation => "Ps",
            UnicodeCategory.ClosePunctuation => "Pe",
            UnicodeCategory.InitialQuotePunctuation => "Pi",
            UnicodeCategory.FinalQuotePunctuation => "Pf",
            UnicodeCategory.OtherPunctuation => "Po",
            UnicodeCategory.MathSymbol => "Sm",
            UnicodeCategory.CurrencySymbol => "Sc",
            UnicodeCategory.ModifierSymbol => "Sk",
            UnicodeCategory.OtherSymbol => "So",
            _ => "Cn",
        };

        /// <summary>
        ///     Reads an escape that stands for one character, in or outside a class, and writes it.
        /// </summary>
        /// <remarks>
        ///     Handles the control escapes, <c>\0</c>, <c>\cX</c>, <c>\xHH</c>, <c>\uHHHH</c>, <c>\u{H...}</c>
        ///     with the Unicode flag, and the escaped punctuation that stands for itself. A letter or digit
        ///     that is not an escape is rejected: JavaScript reads it as the plain letter, but .NET gives
        ///     many of them a meaning, so the author's intent cannot be known.
        /// </remarks>
        /// <param name="inClass"><see langword="true"/> when the escape is inside a bracketed class.</param>
        /// <param name="value">Receives the code unit that the escape stands for.</param>
        /// <returns><see langword="true"/> when the escape was read; its translation is appended to the output.</returns>
        private bool ReadCharacterEscape(bool inClass, out int value)
        {
            value = 0;
            var character = _source[_position];
            switch (character)
            {
                case 't' or 'n' or 'r' or 'f' or 'v':
                    _position++;
                    value = character switch { 't' => '\t', 'n' => '\n', 'r' => '\r', 'f' => '\f', _ => '\v' };
                    break;

                case '0':
                    _position++;
                    if (_position < _source.Length && char.IsAsciiDigit(_source[_position]))
                    {
                        return Fail("octal escapes are not supported.");
                    }

                    break;

                case 'c':
                    if (_position + 1 >= _source.Length || !char.IsAsciiLetter(_source[_position + 1]))
                    {
                        return Fail("the escape '\\c' must be followed by a letter.");
                    }

                    value = _source[_position + 1] & ControlMask;
                    _position += 2;
                    break;

                case 'x':
                    if (!ReadHexDigits(_position + 1, HexEscapeDigits, out value))
                    {
                        return Fail("the escape '\\x' must be followed by two hexadecimal digits.");
                    }

                    _position += 1 + HexEscapeDigits;
                    break;

                case 'u':
                    if (!ReadUnicodeEscape(out value, out var isAstral))
                    {
                        return false;
                    }

                    if (isAstral)
                    {
                        // A code point outside the basic plane is two code units, written as a group so a quantifier covers both
                        if (inClass)
                        {
                            return Fail("a character outside the Basic Multilingual Plane is not supported inside [...].");
                        }

                        return true;
                    }

                    break;

                default:
                    if (char.IsAsciiLetterOrDigit(character))
                    {
                        return Fail($"the escape '\\{character}' is not supported.");
                    }

                    // An escaped punctuation character, or any other character, stands for itself
                    if (char.IsHighSurrogate(character))
                    {
                        if (inClass)
                        {
                            return Fail("a character outside the Basic Multilingual Plane is not supported inside [...].");
                        }

                        AppendLiteralAtPosition();
                        return true;
                    }

                    value = character;
                    _position++;
                    break;
            }

            if (!inClass)
            {
                AppendLiteral((char)value);
            }

            return true;
        }

        /// <summary>
        ///     Reads a <c>\u</c> escape: four hexadecimal digits, or braces with the Unicode flag.
        /// </summary>
        /// <remarks>
        ///     A braced code point outside the basic plane is written to the output as its two code units in a
        ///     group, and reported through <paramref name="isAstral"/> so the caller does not write it again.
        ///     Side effects: advances the position, and for an astral code point appends to the output.
        /// </remarks>
        /// <param name="value">Receives the code unit when the escape is one code unit.</param>
        /// <param name="isAstral">Receives whether the escape was an astral code point, already written.</param>
        /// <returns><see langword="true"/> when the escape was read.</returns>
        private bool ReadUnicodeEscape(out int value, out bool isAstral)
        {
            value = 0;
            isAstral = false;

            // The braced form is only an escape with the Unicode flag
            if (_unicode && _position + 1 < _source.Length && _source[_position + 1] == '{')
            {
                var close = _source.IndexOf('}', _position);
                var digits = close < 0 ? string.Empty : _source[(_position + 2)..close];
                if (digits.Length == 0 ||
                    !int.TryParse(digits, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var codePoint) ||
                    codePoint > 0x10FFFF)
                {
                    return Fail("the escape '\\u{...}' must hold a code point of up to six hexadecimal digits.");
                }

                _position = close + 1;
                if (codePoint <= MaxCodeUnit)
                {
                    value = codePoint;
                    return true;
                }

                isAstral = true;
                AppendAstral(char.ConvertFromUtf32(codePoint), wrap: true);
                return true;
            }

            if (!ReadHexDigits(_position + 1, UnicodeEscapeDigits, out value))
            {
                return Fail("the escape '\\u' must be followed by four hexadecimal digits.");
            }

            _position += 1 + UnicodeEscapeDigits;
            return true;
        }

        /// <summary>
        ///     Reads a fixed number of hexadecimal digits at an offset.
        /// </summary>
        /// <remarks>
        ///     Stateless.
        /// </remarks>
        /// <param name="start">The offset of the first digit.</param>
        /// <param name="count">The number of digits.</param>
        /// <param name="value">Receives the value when the method returns true.</param>
        /// <returns><see langword="true"/> when that many hexadecimal digits are there.</returns>
        private bool ReadHexDigits(int start, int count, out int value)
        {
            value = 0;
            if (start + count > _source.Length)
            {
                return false;
            }

            // The hexadecimal number style allows nothing but hexadecimal digits, so no sign or space can slip in
            return int.TryParse(
                _source.AsSpan(start, count),
                NumberStyles.AllowHexSpecifier,
                CultureInfo.InvariantCulture,
                out value);
        }

        /// <summary>
        ///     Reads a bracketed class, <c>[...]</c> or <c>[^...]</c>.
        /// </summary>
        /// <remarks>
        ///     Every member is written as an escape, and the shorthand classes are written out as ranges, so
        ///     the class means in .NET exactly what it means in JavaScript. An empty class matches nothing
        ///     and <c>[^]</c> matches everything, as in JavaScript.
        /// </remarks>
        /// <returns><see langword="true"/> when the class was read and written.</returns>
        private bool ReadClass()
        {
            _position++;
            var negated = _position < _source.Length && _source[_position] == '^';
            if (negated)
            {
                _position++;
            }

            _classSet.Clear();
            var body = new StringBuilder();
            while (true)
            {
                if (_position >= _source.Length)
                {
                    return Fail("a '[' is not closed.");
                }

                if (_source[_position] == ']')
                {
                    _position++;
                    break;
                }

                if (!ReadClassItem(body))
                {
                    return false;
                }
            }

            // When the case flag is set the class is the set of characters that are the same letter as a member
            if (_ignoreCase)
            {
                var closed = JavaScriptCaseFolding.Close(_classSet, _unicode);
                AppendSet(negated ? Complement(closed) : closed);
                return true;
            }

            // Write the class; an empty one matches nothing, and a negated empty one matches everything
            if (body.Length == 0)
            {
                _output.Append(negated ? "[\\u0000-\\uFFFF]" : "(?!)");
            }
            else
            {
                _output.Append(negated ? "[^" : "[").Append(body).Append(']');
            }

            return true;
        }

        /// <summary>
        ///     Reads one member of a class: a character, a range, or a shorthand class.
        /// </summary>
        /// <remarks>
        ///     A hyphen next to a shorthand class is a plain hyphen without the Unicode flag, as in
        ///     JavaScript. Side effect: appends the member to <paramref name="body"/>.
        /// </remarks>
        /// <param name="body">The body of the class so far. Must not be null.</param>
        /// <returns><see langword="true"/> when the member was read.</returns>
        private bool ReadClassItem(StringBuilder body)
        {
            if (!ReadClassAtom(out var first, out var firstText))
            {
                return false;
            }

            var firstSet = _atomSet;

            // A hyphen followed by something other than the end of the class makes a range
            var startsRange = _position + 1 < _source.Length && _source[_position] == '-' && _source[_position + 1] != ']';
            if (!startsRange)
            {
                body.Append(firstText);
                AddToClassSet(first, firstSet);
                return true;
            }

            _position++;
            if (!ReadClassAtom(out var second, out var secondText))
            {
                return false;
            }

            var secondSet = _atomSet;
            if (first < 0 || second < 0)
            {
                // A shorthand class cannot end or start a range
                if (_unicode)
                {
                    return Fail("a character class cannot be an end of a range when the u flag is set.");
                }

                body.Append(firstText);
                AppendCodeUnit(body, '-');
                body.Append(secondText);
                AddToClassSet(first, firstSet);
                AddToClassSet('-', null);
                AddToClassSet(second, secondSet);
                return true;
            }

            if (first > second)
            {
                return Fail("a range in [...] is out of order.");
            }

            body.Append(firstText).Append('-').Append(secondText);
            if (_ignoreCase)
            {
                _classSet.Add((first, second));
            }

            return true;
        }

        /// <summary>
        ///     Adds the members of a class atom to the set of the class, when the case flag is set.
        /// </summary>
        /// <remarks>
        ///     Side effect: changes the set of the class.
        /// </remarks>
        /// <param name="value">The code unit of a single character, or -1 for a set.</param>
        /// <param name="set">The ranges of the set when the atom is a set; otherwise null.</param>
        private void AddToClassSet(int value, List<(int Low, int High)>? set)
        {
            if (!_ignoreCase)
            {
                return;
            }

            if (set is not null)
            {
                _classSet.AddRange(set);
            }
            else
            {
                _classSet.Add((value, value));
            }
        }

        /// <summary>
        ///     Reads one atom of a class: a single character or a shorthand class.
        /// </summary>
        /// <remarks>
        ///     Side effect: advances the position.
        /// </remarks>
        /// <param name="value">
        ///     Receives the code unit of a single character, or -1 when the atom is a set of characters.
        /// </param>
        /// <param name="text">Receives the text to write into the body of the class for this atom.</param>
        /// <returns><see langword="true"/> when the atom was read.</returns>
        private bool ReadClassAtom(out int value, out string text)
        {
            value = -1;
            text = string.Empty;
            _atomSet = null;
            var character = _source[_position];
            if (character != '\\')
            {
                // A character outside the basic plane cannot be a member of a class
                if (char.IsSurrogate(character))
                {
                    return Fail("a character outside the Basic Multilingual Plane is not supported inside [...].");
                }

                _position++;
                value = character;
                text = CodeUnitText(character);
                return true;
            }

            _position++;
            if (_position >= _source.Length)
            {
                return Fail("the pattern ends with a backslash.");
            }

            var escaped = _source[_position];
            switch (escaped)
            {
                case 'b':
                    _position++;
                    value = Backspace;
                    text = CodeUnitText(Backspace);
                    return true;

                case 'd' or 'D' or 'w' or 'W' or 's' or 'S':
                    _position++;
                    text = escaped switch
                    {
                        'd' => DigitBody,
                        'D' => NotDigitBody,
                        'w' => WordBody,
                        'W' => NotWordBody,
                        's' => SpaceBody,
                        _ => NotSpaceBody,
                    };
                    _atomSet = _ignoreCase ? ShorthandSet(escaped) : null;
                    return true;

                case 'p' or 'P':
                    return ReadClassProperty(out text);

                case 'B' or 'k' or >= '1' and <= '9':
                    return Fail($"the escape '\\{escaped}' is not supported inside [...].");

                default:
                    // Reading a character escape appends nothing inside a class and reports the value
                    if (!ReadCharacterEscape(inClass: true, out value))
                    {
                        return false;
                    }

                    text = CodeUnitText(value);
                    return true;
            }
        }

        /// <summary>
        ///     Reads a Unicode property escape inside a class and returns its text for the class body.
        /// </summary>
        /// <remarks>
        ///     Reuses <see cref="ReadProperty"/>, which writes the escape to the output; the written text is
        ///     taken back from the output so that it lands in the class body instead.
        /// </remarks>
        /// <param name="text">Receives the escape text, for example <c>\p{L}</c>.</param>
        /// <returns><see langword="true"/> when the property was read.</returns>
        private bool ReadClassProperty(out string text)
        {
            text = string.Empty;
            var before = _output.Length;
            if (!ReadProperty())
            {
                return false;
            }

            text = _output.ToString(before, _output.Length - before);
            _output.Length = before;
            _atomSet = _ignoreCase ? _propertySet : null;
            return true;
        }

        /// <summary>
        ///     Writes a code unit as the text used inside a class body.
        /// </summary>
        /// <remarks>
        ///     Stateless.
        /// </remarks>
        /// <param name="codeUnit">The code unit.</param>
        /// <returns>The <c>\u</c> escape of the code unit.</returns>
        private static string CodeUnitText(int codeUnit)
        {
            var builder = new StringBuilder();
            AppendCodeUnit(builder, codeUnit);
            return builder.ToString();
        }

        /// <summary>
        ///     Writes the literal character at the position, which may be one code unit or a surrogate pair.
        /// </summary>
        /// <remarks>
        ///     With the Unicode flag a surrogate pair is one character, so it is written as a group and a
        ///     quantifier covers both code units; without the flag JavaScript repeats only the second one,
        ///     and so does the translation. Side effect: advances the position.
        /// </remarks>
        private void AppendLiteralAtPosition()
        {
            var character = _source[_position];
            if (char.IsHighSurrogate(character) && _position + 1 < _source.Length && char.IsLowSurrogate(_source[_position + 1]))
            {
                AppendAstral(_source.Substring(_position, 2), _unicode);
                _position += 2;
                return;
            }

            AppendLiteral(character);
            _position++;
        }

        /// <summary>
        ///     Writes a character outside the basic plane, which is two code units.
        /// </summary>
        /// <remarks>
        ///     When the expression ignores case and has the Unicode flag, the character is written as the choice
        ///     between its forms, because with the flag it is one character that has a case. Otherwise it is the
        ///     two code units, in a group when a quantifier must cover both. Side effect: appends to the output.
        /// </remarks>
        /// <param name="pair">The character as a surrogate pair. Must not be null.</param>
        /// <param name="wrap"><see langword="true"/> to write the two code units in a group.</param>
        private void AppendAstral(string pair, bool wrap)
        {
            if (_ignoreCase && _unicode)
            {
                var forms = JavaScriptCaseFolding.AstralForms(pair);
                if (forms.Count > 1)
                {
                    _output.Append("(?:");
                    for (var index = 0; index < forms.Count; index++)
                    {
                        _output.Append(index > 0 ? "|" : string.Empty);
                        AppendCodeUnit(_output, forms[index][0]);
                        AppendCodeUnit(_output, forms[index][1]);
                    }

                    _output.Append(')');
                    return;
                }
            }

            _output.Append(wrap ? "(?:" : string.Empty);
            AppendCodeUnit(_output, pair[0]);
            AppendCodeUnit(_output, pair[1]);
            _output.Append(wrap ? ")" : string.Empty);
        }

        /// <summary>
        ///     Writes one literal code unit so that .NET can read it only as itself.
        /// </summary>
        /// <remarks>
        ///     ASCII letters, digits, and a short list of harmless punctuation are written as they are;
        ///     everything else is written as a <c>\u</c> escape. When the expression ignores case, a code unit
        ///     that has other forms is written as the class of all the code units that are the same letter.
        ///     Side effect: appends to the output.
        /// </remarks>
        /// <param name="character">The code unit to write.</param>
        private void AppendLiteral(char character)
        {
            if (_ignoreCase)
            {
                var forms = JavaScriptCaseFolding.Equivalents(character, _unicode);
                if (forms.Count > 1)
                {
                    _output.Append('[');
                    foreach (var form in forms)
                    {
                        AppendCodeUnit(_output, form);
                    }

                    _output.Append(']');
                    return;
                }
            }

            if (char.IsAsciiLetterOrDigit(character) || PlainCharacters.Contains(character, StringComparison.Ordinal))
            {
                _output.Append(character);
            }
            else
            {
                AppendCodeUnit(_output, character);
            }
        }
    }
}
