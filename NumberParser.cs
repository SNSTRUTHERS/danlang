using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;

public class NumberParser {
    private static Regex _BuildParseRegex(char b, string chars, bool balanced = false, bool ignoreCase = true) =>
        new Regex($"^#(?<dir>[<>])?(?<baseMod>[+-])?(?<base>[{char.ToLower(b)}{char.ToUpper(b)}])(?<value>[{chars.Replace("-", @"\-")}]+(\\.[{chars.Replace("-", @"\-")}]+)?)$", ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None);


    public class NumberBaseInfo {
        public NumberBaseInfo(char baseChar, string valueChars, bool le = false, bool balanced = false, bool negativeBase = false) {
            BaseChar = baseChar;
            Base = valueChars.Length;
            ValueChars = valueChars; // need to make sure the valueChars fit the valid set and are unique
            IsLittleEndian = le;
            IsBalanced = balanced;
            IsComplemented = false;
            IsNegative = negativeBase;
            if (IsNegative) Base *= -1;
            ParseRegex = _BuildParseRegex(baseChar, valueChars, balanced, IsCaseUnique(valueChars));
        }

        public char BaseChar {get; private set;}
        public string ValueChars {get; private set;}
        public bool IsLittleEndian {get; private set;}
        public bool IsBalanced {get; private set;}
        public bool IsComplemented {get; private set;}
        public bool IsNegative {get; private set;}
        public Regex ParseRegex {get; private set;}

        public int Base {get; private set;}
    }

    private const string InvalidCharsetCharacters = "<>[]() \t\r\n./_\\'\"";

    // SimonNchlasZptBvrue => acehilmnoprstuvBNSZ

    public static Dictionary<char, NumberBaseInfo> Bases = new Dictionary<char, NumberBaseInfo> {
            // LE, balance
        {'c', new NumberBaseInfo('c', "-0+", true, true)},
        {'e', new NumberBaseInfo('e', "=-0+#", true, true)},
        {'g', new NumberBaseInfo('g', "~=-0+#*", true, true)},
        {'i', new NumberBaseInfo('i', "UON", true, true)},
        {'j', new NumberBaseInfo('j', "WUONM", true, true)},
        {'m', new NumberBaseInfo('m', "DanielStphrus", true, true, true)},
        // BE, standard
        {'b', new NumberBaseInfo('b', "01")},
        {'t', new NumberBaseInfo('t', "012")},
        {'q', new NumberBaseInfo('q', "0123")},
        {'v', new NumberBaseInfo('v', "01234")},
        {'f', new NumberBaseInfo('f', "012345")},
        {'s', new NumberBaseInfo('s', "0123456")},
        {'o', new NumberBaseInfo('o', "01234567")},
        {'n', new NumberBaseInfo('n', "012345678")},
        {'d', new NumberBaseInfo('d', "0123456789")},
        {'x', new NumberBaseInfo('x', "0123456789ABCDEF")},
        {'z', new NumberBaseInfo('z', "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ")},
        // BE, balanced
        {'k', new NumberBaseInfo('k', "ZYXWVUTSRQPON0ABCDEFGHIJKLM", balanced: true)},
        {'y', new NumberBaseInfo('y', "zyxwvutsrqponmlkjihgfedcba0ABCDEFGHIJKLMNOPQRSTUVWXYZ", balanced: true)}
    };

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

    public NumberParser(char b = 'd', bool? negativeBase = null, bool? le = null) {
        if (!Bases.ContainsKey(b)) throw new Exception($"Invalid number base '{b}'");
        b = Char.ToLower(b);

        var bi = Bases[b];
        _base = bi.Base;
        _balanced = bi.IsBalanced;
        _comp = bi.IsComplemented;
        Chars = bi.ValueChars;

        _le = le ?? bi.IsLittleEndian;

        bool negBase = negativeBase ?? bi.IsNegative;
        if (negBase) _base = -Math.Abs(_base);
        else _base = Math.Abs(_base);

        _zeroIndex = _balanced ? Chars.Length / 2 : 0;
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
    public int MaxDigitVal{ get => _balanced ? Chars.Length - 1 - _zeroIndex : Chars.Length - 1; }   // (balanced of an even size: one less above 0 than below)
    
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


    // ---- Writing a number in a base

    // A number as text in a base (a base string, as to-str takes): with its prefix when the string starts with #
    // ("#x": #xFF), without it when it doesn't ("x": FF)
    public static string ToBase(Num n, string b) => Display(n, NumberFormat.Of(b));

    // A number as text in a format (prefix: whether its prefix is written; null, as the format says).  Decimal with no
    // prefix is as ToString writes it.  In any other base an integer is its digits; a fraction or a fixed decimal is
    // written with a radix point when it ends in that base (0.5 in binary 0.1), else as a fraction, its parts in the
    // base; a complex number is its parts, joined by its imaginary part's sign, then i
    public static string Display(Num n, NumberFormat f, bool? prefix = null) {
        var pre = prefix ?? f.Prefix;
        if (f.IsDecimal && !pre) return n.ToString();
        if (n is Comp c) return DisplayComplex(c, f, pre);
        return DisplayReal(n, f, pre);
    }

    private static string DisplayComplex(Comp c, NumberFormat f, bool pre) {
        // A base whose digits include +, - or i would make the parts ambiguous: its complex numbers are written in
        // decimal, each part with #d, which reads back in any base
        if (f.HasDigit('+') || f.HasDigit('-') || f.HasDigit('i')) { f = NumberFormat.Decimal; pre = true; }
        var im = DisplayReal(c.im, f, pre);
        if (c.r.IsZero) return im + "i";
        var re = DisplayReal(c.r, f, pre);
        return re + (im.StartsWith('-') ? "" : "+") + im + "i";
    }

    private static string DisplayReal(Num n, NumberFormat f, bool pre) {
        var p = pre ? f.PrefixText : "";
        if (f.IsDecimal) {
            // decimal with its prefix (#d): danlang's forms, the prefix after the sign
            var s = n.ToString();
            if (n is Rat r && r.den != 1) return Display(new Int(r.num), f, true) + "/" + Display(new Int(r.den), f, true);
            return s.StartsWith('-') ? "-" + p + s.Substring(1) : p + s;
        }
        var q = Rat.ToRat(n);
        if (q.den == 1) return Digits(q.num, f, p, 0);
        var k = PlacesIn(q.den, f.Base);
        if (k < 0) return Digits(q.num, f, p, 0) + "/" + Digits(q.den, f, p, 0);
        return Digits(q.num * BigInteger.Pow(f.Base, k) / q.den, f, p, k);
    }

    // The places after the radix point a fraction with this denominator ends in, in a base (its least k for which
    // den divides base^k), or -1 if it doesn't end there
    private static int PlacesIn(BigInteger den, int b) {
        BigInteger ab = Math.Abs(b);
        var k = 0;
        while (den != 1) {
            var g = BigInteger.GreatestCommonDivisor(den, ab);
            if (g == 1) return -1;
            den /= g;
            ++k;
        }
        return k;
    }

    // An integer's digits in a format, with its prefix p and a radix point before its last k digits: the sign first
    // in a positive base that isn't balanced (-#b101); a balanced or negative base's digits hold the sign themselves
    private static string Digits(BigInteger n, NumberFormat f, string p, int k) {
        var acc = f.NewParser();
        var neg = acc.Base > 0 && !acc.IsBalanced && n < 0;
        var be = BigEndianDigits(neg ? -n : n, acc);
        if (k > 0) {
            if (be.Length <= k) be = new string(acc.CharFor(0), k + 1 - be.Length) + be;
            be = be.Insert(be.Length - k, ".");
        }
        if (acc.IsLE) be = new string(be.Reverse().ToArray());
        return (neg ? "-" : "") + p + be;
    }

    // An integer's digits, the most significant first (a negative one in a positive base that isn't balanced: its
    // magnitude's, as the caller writes the sign)
    private static string BigEndianDigits(BigInteger scratch, NumberParser acc) {
        var str = new StringBuilder();
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
            str.Append(acc.CharFor(d));
        }
        return str.ToString();
    }

    public Num Num { get => (_den == null || _den == 1) ?
        new Int(Val) :
        (_base == 10 ?
            new Fix(_den > 0 ? Val : -Val, Log10(_den ?? 1)) :
            Num.Norm(new Rat(Val, _den ?? 1))); }    // (a rational whose denominator is 1: an integer)

    public const string CustomBaseStandardCharset = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz-=+`~!@#$%^&*,;:|?";
    private static Regex _customBaseRegex = new Regex(@"^#(?<dir>[<>]?)(?<bal>=?)(?<baseMod>[+-]?)((?<numberBase>([1-7]?[0-9]|80))[rR]|\[(?<chars>[0-9a-zA-Z`~!@#$%^&*\-=+|;:,?]{2,80})\])(?<value>[0-9a-zA-Z`~!@#$%^&*\-=+|;:,?]+(\.[0-9a-zA-Z`~!@#$%^&*\-=+|;:,?]+)?)$");

    // ---- Reading a number

    // A number from text in the current base (NumberFormat.Current), or null when it isn't one
    public static Num? ParseString(string? s) => ParseString(s, NumberFormat.Current, false);

    // A number from text in a base: a number with a # of its own in its own (#xFF), a bare one in f.  source: the text
    // is a program's (the reader's), where a bare number must start with a digit 0-9 so that names stay names (FF is
    // a name in hexadecimal, 0FF a number).  A complex number is a real part, + or -, an imaginary part and i (1+2i,
    // 0.5-1/3i), or an imaginary part and i alone (2i)
    public static Num? ParseString(string? s, NumberFormat f, bool source) {
        s = s?.Trim().Replace("_", "");
        if (string.IsNullOrWhiteSpace(s)) return null;
        var real = ParseReal(s, f, source);
        if (real != null || !s.EndsWith('i')) return real;

        // a complex number: the imaginary part alone, or the last sign at which both parts are real numbers
        var body = s.Substring(0, s.Length - 1);
        if (body.Length == 0) return null;
        var im = ParseReal(body, f, source);
        if (im != null) return Num.Norm(new Comp(new Int(0), (Int)im));
        for (var p = body.Length - 1; p > 0; --p) {
            if (body[p] != '+' && body[p] != '-') continue;
            var re = ParseReal(body.Substring(0, p), f, source);
            if (re == null) continue;
            var ip = ParseReal(body.Substring(p), f, false);    // (its sign first: the rule is the real part's)
            if (ip != null) return Num.Norm(new Comp((Int)re, (Int)ip));
        }
        return null;
    }

    private static Num? ParseReal(string s, NumberFormat f, bool source) {
        if (s.Contains('/')) { // is a rational
            var splits = s.Split('/');
            if (splits.Length != 2 || splits.Any(sp => sp.Length == 0 || sp.Contains('.'))) return null;

            // each part an integer, or it isn't a number (1/x is a symbol)
            var num = ParseReal(splits[0], f, source);
            var den = ParseReal(splits[1], f, false);
            if (num == null || den == null || num is Rat || den is Rat || num is Comp || den is Comp) return null;
            if (den.IsZero) throw new DivideByZeroException();

            return Num.Norm(new Rat(((Int)num).num, ((Int)den).num));
        }

        // the sign: before a # (-#x10), or a bare number's, unless the base has + or - among its digits (a balanced
        // base's numbers have no sign: #c+-0)
        var sign = '+';
        var bare = !s.TrimStart('+', '-').StartsWith('#');
        if ("+-".Contains(s[0]) && !(bare && f.HasDigit(s[0]))) {
            sign = s[0];
            s = s.Substring(1);
        }
        if (s == "") return null;

        Num? v;
        if (s[0] == '#') v = ParseBased(s);
        else {
            if (source && !(s[0] >= '0' && s[0] <= '9')) return null;
            if (f.IsDecimal) {
                // a decimal number: an integer, or a fixed one (1.5)
                if (!Regex.IsMatch(s, @"^[0-9]+(\.[0-9]+)?$")) return null;
                var splits = s.Split('.');
                v = splits.Length > 1 ? new Fix(BigInteger.Parse(splits[0] + splits[1]), splits[1].Length)
                    : new Int(BigInteger.Parse(splits[0]));
            }
            else {
                // in the base: its digits, and a radix point (once, with digits on both sides)
                var dot = s.IndexOf('.');
                if (dot == 0 || dot == s.Length - 1) return null;
                var i = f.NewParser();
                if (i.AddString(s) != s.Length) return null;
                v = i.Num;
            }
        }
        if (v == null) return null;
        return sign == '-' ? -v : v;
    }

    // A number with a # of its own: a radix (#16rFF), digits of its own (#[01]101), or a named base (#xFF)
    private static Num? ParseBased(string s) {
        NumberParser? i = null;
        if (_customBaseRegex.IsMatch(s)) {
            var m = _customBaseRegex.Match(s);
            bool bal = false;
            bool le = false;
            bool negBase = false;

            var leg = m.Groups["dir"].Captures;
            if (leg.Count > 0 && leg[0].Length > 0) {
                if (leg[0].Value == "<") le = true;
                if (leg[0].Value == ">") le = false;
            }

            var bmg = m.Groups["baseMod"].Captures;
            if (bmg.Count > 0 && bmg[0].Length > 0) {
                if (bmg[0].Value == "-") negBase = true;
            }

            var bg = m.Groups["bal"].Captures;
            if (bg.Count > 0 && bg[0].Length > 0) {
                if (bg[0].Value == "=") bal = true;
            }

            string valueChars = "";
            var nbg = m.Groups["numberBase"].Captures;
            if (nbg.Count > 0 && nbg[0].Length > 0) {
                var numberBase = int.Parse(nbg[0].Value);
                if (numberBase < 2) return null;
                valueChars = CustomBaseStandardCharset.Substring(0, numberBase);
            }
            else {
                // a digit set of its own: each digit once (#[0101]... isn't a number)
                valueChars = m.Groups["chars"].Captures[0].Value;
                if (!IsUniqueSet(valueChars)) return null;
            }

            i = new NumberParser(negBase, le, bal, valueChars);

            // (every character of the value a digit of the set: AddString stops at one that isn't)
            var value = m.Groups["value"].Captures[0].Value;
            var consumed = i.AddString(value);
            if (consumed != value.Length) return null;
            return i.Num;
        }

        foreach (var b in Bases.Values) {
            if (b.ParseRegex.IsMatch(s)) {
                var m = b.ParseRegex.Match(s);
                var bal = b.IsBalanced;
                var le = b.IsLittleEndian;
                var negBase = b.IsNegative;

                var leg = m.Groups["dir"].Captures;
                if (leg.Count > 0 && leg[0].Length > 0) {
                    if (leg[0].Value == "<") le = true;
                    if (leg[0].Value == ">") le = false;
                }

                var bmg = m.Groups["baseMod"].Captures;
                if (bmg.Count > 0 && bmg[0].Length > 0) {
                    if (bmg[0].Value == "-") negBase = true;
                    if (bmg[0].Value == "+") negBase = false;
                }

                i = new NumberParser(negBase, le, bal, b.ValueChars);

                var value = m.Groups["value"].Captures[0].Value;
                var consumed = i.AddString(value);
                if (consumed != value.Length) return null;
                return i.Num;
            }
        }

        return null;    // (not a number: the reader makes it a symbol)
    }
}

// A base numbers are written and read in, as a base string names it ("x", "#x", "#16r", "c", "<x", "=16r", "[01]",
// "d" ...): a named base's letter, a radix (2 to 80, "r"), or digits of its own ("[...]"), after its modifiers (<
// least digit first, > most digit first, = balanced, + a positive base, - a negative one).  A string that starts with
// # shows its prefix when a number is written in it (#xFF); one that doesn't, not (FF).  The current base (base's) is
// what numbers are shown in and read in, everywhere: decimal at the start
public class NumberFormat {
    public string Spec { get; private set; } = "d";
    public bool Prefix { get; private set; }
    public string PrefixText { get; private set; } = "#d";   // what's written before the digits when the prefix is
    public bool IsDecimal { get; private set; }
    public int Base => NewParser().Base;

    private string _chars = "0123456789";
    private bool _le, _bal, _neg;

    private static readonly Regex _spec = new Regex(@"^(?<pre>#?)(?<dir>[<>]?)(?<bal>=?)(?<sign>[+-]?)((?<radix>[0-9]{1,2})[rR]|\[(?<chars>[^\]]{2,80})\]|(?<letter>[A-Za-z]))$");

    public static readonly NumberFormat Decimal = Of("d");
    public static NumberFormat Current = Decimal;

    // The format a base string names; an error (FormatException) if it names none
    public static NumberFormat Of(string spec) {
        var m = _spec.Match(spec ?? "");
        if (!m.Success) throw new FormatException($"Invalid base specifier {spec}");
        var f = new NumberFormat { Spec = spec!, Prefix = m.Groups["pre"].Value == "#" };
        string dir = m.Groups["dir"].Value, sign = m.Groups["sign"].Value;
        f.PrefixText = "#" + spec!.TrimStart('#');
        if (m.Groups["letter"].Success) {
            var c = char.ToLower(m.Groups["letter"].Value[0]);
            if (!NumberParser.Bases.TryGetValue(c, out var bi)) throw new FormatException($"Invalid base specifier {spec}");
            if (m.Groups["bal"].Value == "=") throw new FormatException($"Invalid base specifier {spec}: a named base is balanced or not already");
            f._chars = bi.ValueChars;
            f._bal = bi.IsBalanced;
            f._le = dir == "" ? bi.IsLittleEndian : dir == "<";
            f._neg = sign == "" ? bi.IsNegative : sign == "-";
        }
        else {
            if (m.Groups["radix"].Success) {
                var r = int.Parse(m.Groups["radix"].Value);
                if (r < 2 || r > 80) throw new FormatException($"Invalid base specifier {spec}: a radix is 2 to 80");
                f._chars = NumberParser.CustomBaseStandardCharset.Substring(0, r);
            }
            else {
                f._chars = m.Groups["chars"].Value;
                if (!NumberParser.IsUniqueSet(f._chars)) throw new FormatException($"Invalid base specifier {spec}: each digit once");
            }
            f._bal = m.Groups["bal"].Value == "=";
            f._le = dir == "<";
            f._neg = sign == "-";
        }
        f.IsDecimal = f._chars == "0123456789" && !f._bal && !f._le && !f._neg;
        return f;
    }

    // A parser for numbers in this base (each read needs its own: it keeps its value as it goes)
    public NumberParser NewParser() => new NumberParser(_neg, _le, _bal, _chars);

    // Whether a character is one of the base's digits (in either case, unless its digits' case matters)
    public bool HasDigit(char c) => NewParser().IsDigit(c);
}
