using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;

public class NumberParser {
    public static readonly Dictionary<char, int> NumBases =
        new Dictionary<char, int> {{'b', 2}, {'c', 3}, {'d', 10}, {'e', 5}, {'f', 6}, {'g', 7}, {'k', 27}, {'m', 13}, {'n', 9}, {'o', 8}, {'q', 4}, {'s', 7}, {'t', 3}, {'v', 5}, {'x', 16}, {'y', 53}, {'z', 36}};

    private const string NumChars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const string BalancedChars = "zyxwvutsrqponmlkjihgfedcba0ABCDEFGHIJKLMNOPQRSTUVWXYZ";   
    private const string BalancedTernQuinSeptChars = "~=-0+#*";
    private const string InvalidCharsetCharacters = "<>[]() \t\r\n._\\'\"";
    private const string BaseMChars = "nlieDaShprstu";

    private BigInteger? _den = null;
    private BigInteger _factor = 1;
    private int _base = 10;
    private int _zeroIndex = 0;
    private bool _le = false;
    private bool _balanced = false;
    private bool _caseSignificant = false;
    private bool _comp = false;

    public static int Log10(BigInteger den) {
        if (den < 0) den = -den;
        int log = 0;
        while (den >= 10) { ++log; den /= 10; }
        return log;
    }

    public string Chars;
    // public char NumberBase { get; private set; }
    public int Base { get => _base; }
    public bool IsBalanced { get => _balanced; }
    public bool IsLE { get => _le; }
    public bool IsCaseSignificant { get => _caseSignificant; }
    public BigInteger Val { get; private set; }

    public static bool IsUniqueSet(string? s = null) {
        if (string.IsNullOrEmpty(s)) return false;

        var set = new HashSet<char>();
        var unique = s.ToCharArray().All(c => set.Add(c));
        var allLegal = set.All(c => !InvalidCharsetCharacters.Contains(c) && (int)c > 32);
        return allLegal && unique;
    }

    public static bool IsCaseUnique(string? s = null) {
        if (string.IsNullOrEmpty(s)) return false;

        return IsUniqueSet(s.ToUpper());
    }

    public NumberParser(bool negativeBase, bool le, bool balanced, string chars) {
        _base = chars.Length;
        _balanced = balanced;
        _comp = false;
        Chars = chars;
        _caseSignificant = !IsCaseUnique(Chars);
        _le = le;
        _zeroIndex = _balanced ? Chars.Length / 2 : 0;
        if (negativeBase) _base = -_base;
        if (!_caseSignificant) Chars = Chars.ToUpper();
    }

    public NumberParser(char b = 'd', int mult = 1, bool le = false, bool comp = false, string? chars = null) {
        _base = NumBases[Char.ToLower(b)];
        _balanced = "cegkmy".Contains(b);
        _comp = comp && !_balanced && _base > 0;
        if ((chars?.Length ?? 0) < _base || !IsUniqueSet(chars)) chars = null;
        Chars = chars ?? b switch {
            'c' or 'e' or 'g' => BalancedTernQuinSeptChars,
            'k' or 'y' => BalancedChars,
            'm' => BaseMChars,
            _ => NumChars};

        _le = le;
        _zeroIndex = _balanced ? Chars.Length / 2 : 0;
        Chars = Chars.Substring(_balanced ? _zeroIndex - (_base / 2) : _zeroIndex, _base);
        _zeroIndex = _balanced ? Chars.Length / 2 : 0;
        _base = _base * mult;
        _caseSignificant = !IsCaseUnique(Chars);
        if (!_caseSignificant) Chars = Chars.ToUpper();
    }

    public void Reset() {
        _factor = 1; _den = null; Val = 0;
    }

    private char DigitOf(int i) => _caseSignificant ? (char)i : Char.ToUpper((char)i);
    
    private BigInteger DigitVal(int c) => Chars.IndexOf(DigitOf(c)) - _zeroIndex;

    public char CharFor(int v) => Chars[v + _zeroIndex];
    
    private static bool IsPlaceholder(int c) => c == '_' || c == '.';
    
    public bool IsDigit(int i) => i >= 0 && Chars.IndexOf(DigitOf(i)) >= 0;
    public int MinDigitVal { get => -_zeroIndex; }
    public int MaxDigitVal{ get => _balanced ? _zeroIndex : Chars.Length - 1; }
    
    public BigInteger NewVal(int c) => Val * _base + DigitVal(c);

    public int AddString(string s) {
        var consumed = 0;
        foreach (var c in (IsLE ? s.Reverse() : s)) {
            if (!Add(c)) break;
            ++consumed;
        }
        return consumed;
    }

    private bool Add(int c) {
        if (IsPlaceholder(c)) {
            if (c == '.') {
                if (_den != null) return false; // already consumed a '.'
                _den = 1;
            }
            return true;
        }
        if (!IsDigit(c)) return false;
        if (_den != null) _den = _den * _base;
        Val = NewVal(c);
        _factor *= _base;
        return true;
    }

    public override string ToString() => 
        $"NumberParser: Base={_base}, Chars={Chars}, {(_le ? "Little" : "Big")} Endian{(_balanced ? ", Balanced" : "")}, Case {(IsCaseSignificant ? "Sensitive" : "Insensitive")}";

    public static string ToBase(Num n, string b) {
        var num = ToBase((n as Int)!.num, b);
        var den = "";
        if (n is Fix f && f.num != 0 && f.dec != 0) {
            den = ToBase(BigInteger.Pow(10, f.dec), b);
        } else if (n is Rat r && r.den != 1 && r.num != 0) {
            den = ToBase(r.den, b);
        }

        if (den != "") return num + "/" + den;
        return num;
    }

    public static string ToBase(BigInteger n, string b) {
        // validate incoming b
        var reg = new Regex(@"^0[<>]?-?[" + string.Join("", NumBases.Keys.Select(c => c.ToString())) +"]$", RegexOptions.IgnoreCase);
        if (!reg.IsMatch(b)) throw new FormatException($"Invalid base specifier {b}");

        // normalize b
        var charsStart = b.IndexOf('[');
        /*
        string? chars = null;
        var customCharset = false;
        if (charsStart > 0) {
            var charsEnd = b.IndexOf(']') - 1;
            var numChars = charsEnd - charsStart;
            if (numChars > 1) {
                chars = b.Substring(charsStart + 1, numChars);
                b = b.Remove(charsStart, numChars + 2);
                customCharset = true;
            }
        }
        */

        b = b.Replace("+", "");
        var baseChar = Char.ToLower(b.Last());
        var isCEGM = "cegm".Contains(baseChar);
        if (b.Contains('>') && isCEGM) b = b.Replace(">", "");
        if (b.Contains('<') && !isCEGM) b = b.Replace("<", "");
        if (b.Contains('~') && (!"cegmky".Contains(baseChar) || b.Contains('-'))) b = b.Replace("~", "");

        var baseMult = b.Substring(1).Contains('-') ? -1 : 1;
        var le = b.Contains('<') || (isCEGM && !b.Contains('>'));
        var comp = b.Contains('~');

        var acc = new NumberParser(baseChar, baseMult, le, comp);
        // Console.WriteLine($"Acc: {acc}");
        // customCharset = acc.Chars != new NumberParser(baseChar, baseMult, le, comp).Chars;

        // final normalize
        if (b == "0d") b = "";

        var str = new StringBuilder();

        // adjuster for negative numbers when the base is positive and not balanced
        // in that case, we parse the positive value and then insert a '-' at the front when done
        var mult = (acc.Base > 0 && !acc.IsBalanced && n < 0) ? -1 : 1;
        var scratch = n * mult;

        BigInteger p = 1;
        BigInteger min = acc.MinDigitVal;
        BigInteger max = acc.MaxDigitVal;

        // find our starting place
        while (scratch > max || scratch < min) {
            p = p * acc.Base;
            min = min + (p * ((p > 0) ? acc.MinDigitVal : acc.MaxDigitVal));
            max = max + (p * ((p > 0) ? acc.MaxDigitVal : acc.MinDigitVal));
        }

        while (p != 0) {
            // adjust the min/max in anticipation of the next place
            min = min - (p * ((p > 0) ? acc.MinDigitVal : acc.MaxDigitVal));
            max = max - (p * ((p > 0) ? acc.MaxDigitVal : acc.MinDigitVal));

            // get the digit for the current place
            var d = (int)(scratch / p);
            scratch = scratch % p;

            // fix up for when the current p is too big/small for the remaining value,
            //   but the remaining places cannot possibly cover the remaining value
            var m = (scratch > max ? (scratch > p ? -1 : 1) : (scratch < min ? (scratch < p ? -1 : 1) : 0));
            d = d + m; // adjust digit
            scratch = scratch - (m * p); // adjust scratch

            // adjust p for the next place
            p = p / acc.Base;

            try {
                if (acc.IsLE) str.Insert(0, acc.CharFor(d));
                else str.Append(acc.CharFor(d));
            } catch {
                Console.WriteLine($"*** Exception: (d:{d} b:{acc.Base} p:{p} scratch:{scratch} max:{max} min:{min})");
            }
        }

        /*
        if (customCharset) {
            if (b.Length == 0) b = "0d";
            b = b.Insert(b.Length - 1, $"[{acc.Chars}]");
        }
        */

        str.Insert(0, b);
        if (mult < 0) str.Insert(0, '-');
        return str.ToString();
    }
        public Num Num { get => (_den == null || _den == 1) ?
            new Int(Val) :
            (_base == 10 ?
                new Fix(_den > 0 ? Val : -Val, Log10(_den ?? 1)) :
                new Rat(Val, _den ?? 1)); }

    public static NumberParser Create(char b = 'd', int mult = 1, bool? le = null, bool? comp = null, string? charset = null) =>
        Char.ToLower(b) switch {
            'c' or 'e' or 'g' => new NumberParser(b, mult, le ?? true, comp ?? false, charset),
            _ => new NumberParser(b, mult, le ?? false, comp ?? false, charset)
        };

    private static (Regex, string) BuildParseRegex(char b, string chars, bool balanced = false, bool ignoreCase = true) =>
        (new Regex($"^0(?<dir>[<>])?(?<baseMod>-)?(?<base>[{char.ToLower(b)}{char.ToUpper(b)}])(?<value>[{chars.Replace("-", @"\-")}]+(\\.[{chars.Replace("-", @"\-")}]+)?)", ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None), chars);

    private static ((Regex, string), bool, bool)[] Bases = new [] { // Item2 is whether the base is, by default, balanced.  Item3 is whether the base is little-endian (least-significant place on the left) by default.
        // LE, balance
        (BuildParseRegex('c', "-0+",   true), true, true),
        (BuildParseRegex('e', "=-0+#",  true), true, true),
        (BuildParseRegex('g', "~=-0+#*", true), true, true),
        (BuildParseRegex('m', "nlieDaShprstu", true), true, true),
        // BE, standard
        (BuildParseRegex('b', "01"), false, false),
        (BuildParseRegex('t', "012"), false, false),
        (BuildParseRegex('q', "0123"), false, false),
        (BuildParseRegex('v', "01234"), false, false),
        (BuildParseRegex('f', "012345"), false, false),
        (BuildParseRegex('s', "0123456"), false, false),
        (BuildParseRegex('o', "01234567"), false, false),
        (BuildParseRegex('n', "012345678"), false, false),
        (BuildParseRegex('d', "0123456789"), false, false),
        (BuildParseRegex('x', "0123456789ABCDEF"), false, false),
        (BuildParseRegex('z', "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ"), false, false),
        // BE, balanced
        (BuildParseRegex('k', "mlkjihgfedcba0ABCDEFGHIJKLM", balanced: true, ignoreCase: false), true, false),
        (BuildParseRegex('y', "zyxwvutsrqponmlkjihgfedcba0ABCDEFGHIJKLMNOPQRSTUVWXYZ", true, false), true, false)
        //(new Regex(@"^0(?<dir>[<>])?(?<baseMod>[-=])?\[(?<chars>[^\<\>\[\]\{\}\(\)\t\r\n \\'""\./_]+)\](?<value>[\k<chars>]+(\.[\k<chars>]+)?)"), false, false)
    };

    public static Num? ParseString(string? s) {
        // read base
        // create new IntAcc from base
        // add until Add returns false or end-of-input
        // if not EOI, see if next char is /, +- or i and if so, change to either rational or complex, respectively and repeat
        // if rational consumed and still more input, must be +- or i.  If +-, repeat from top changing over to complex
        NumberParser? i = null;
        s = s?.Trim().Replace("_", "");
        if (string.IsNullOrWhiteSpace(s)) return null;

        if (s.Contains('/')) { // is a rational
            var splits = s.Split('/');
            if (splits.Any(sp => sp.Length == 0 || sp.Contains('.'))) return null;  // TODO: throw or have this return an LVal.Err() instead!

            var num = ParseString(splits[0]) as Int;
            var den = ParseString(splits[1]) as Int;
            // TODO: fix this up to better handle null num/den
            if (den?.num == BigInteger.Zero) throw new DivideByZeroException();

            return new Rat(num?.num ?? BigInteger.Zero, den?.num ?? BigInteger.One);
        }

        // simple case first (it is just a normal, decimal number in while, fixed, or fractional notation, i.e. 1, 1.4, or 1/3)
        if (Regex.IsMatch(s, @"^[+-]?(0|[1-9]\d*)(\.\d+)?$")) {
            BigInteger _ParseInt(string p) => BigInteger.Parse(p);

            // is it a fixed?
            var splits = s.Split('.');
            if (splits.Length > 1 && splits[1].Length > 0) {
                var wp = _ParseInt(splits[0] + splits[1]);
                return new Fix(wp, splits[1].Length);
            }
            return new Int(_ParseInt(splits[0]));
        }

        // otherwise
        var sign = '+';
        if ("+-".Contains(s[0])) {
            sign = s[0];
            s = s.Substring(1);
        }

        if (s == "") return null;

        foreach (var b in Bases) {
            if (b.Item1.Item1.IsMatch(s)) {
                var m = b.Item1.Item1.Match(s);
                var bal =b.Item2;
                var le = b.Item3;
                var negBase = false;
                var bmg = m.Groups["baseMod"].Captures;
                if (bmg.Count > 0 && bmg[0].Length > 0) {
                    negBase = bmg[0].Value == "-";
                    bal = bal || bmg[0].Value == "=";
                }

                var leg = m.Groups["dir"].Captures;
                if (leg.Count > 0 && leg[0].Length > 0) le = leg[0].Value == ">";

                i = new NumberParser(negBase, le, bal, b.Item1.Item2);

                var value = m.Groups["value"].Captures[0].Value;
                Console.WriteLine($"Number match found.  Parser: {i.ToString()}, Value: {value}");
                var consumed = i.AddString(value);
                if (consumed != value.Length) return null;

                var v = i.Num;
                if (sign == '-') v = -v;
                return v;
            }
        }

        return null; // TODO: 
    }
}
