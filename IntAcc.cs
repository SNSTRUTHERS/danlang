using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;

public class IntAcc {
    public static readonly Dictionary<char, int> NumBases =
        new Dictionary<char, int> {{'b', 2}, {'c', 3}, {'d', 10}, {'e', 5}, {'f', 6}, {'g', 7}, {'k', 27}, {'n', 9}, {'o', 8}, {'q', 4}, {'s', 7}, {'t', 3}, {'v', 5}, {'x', 16}, {'y', 53}, {'z', 36}};

    private const string NumChars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const string BalancedChars = "zyxwvutsrqponmlkjihgfedcba0ABCDEFGHIJKLMNOPQRSTUVWXYZ";   
    private const string BalancedTernQuinSeptChars = "~=-0+#*";
    private const string InvalidCharsetCharacters = "[]() \t\r\n._\\'\"";

    private BigInteger? _den = null;
    private BigInteger _factor = 1;
    private int _base = 10;
    private int _zeroIndex = 0;
    private bool _le = false;
    private bool _balanced = false;
    private bool _caseSignificant = false;

    public static int Log10(BigInteger den) {
        if (den < 0) den = -den;
        int log = 0;
        while (den >= 10) { ++log; den /= 10; }
        return log;
    }

    public string Chars;
    public char NumberBase { get; private set; }
    public int Base { get => _base; }
    public bool IsBalanced { get => _balanced; }
    public bool IsLE { get => _le; }
    public bool IsCaseSignificant { get => _caseSignificant; }
    public BigInteger Val { get; private set; }
    public Num Num { get => (_den == null || _den == 1) ?
        new Int(Val) :
        (NumberBase == 'd' ?
            new Fix(_den > 0 ? Val : -Val, Log10(_den ?? 1)) :
            new Rat(Val, _den ?? 1)); }

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

    public IntAcc(char b = 'd', int mult = 1, bool le = false, string? chars = null) {
        b = Char.ToLower(b);
        NumberBase = b;
        _base = NumBases[b];
        _balanced = "cegky".Contains(b);
        if ((chars?.Length ?? 0) < _base || !IsUniqueSet(chars)) chars = null;
        Chars = chars ?? (_balanced ?
            (_base > BalancedTernQuinSeptChars.Length ?
                BalancedChars :
                BalancedTernQuinSeptChars) :
            (b == 'k' ?
                BalancedChars :
                NumChars));

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
    
    private bool IsPlaceholder(int c) => c == '_' || c == '.';
    
    public bool IsDigit(int i) => i >= 0 && Chars.IndexOf(DigitOf(i)) >= 0;
    public int MinDigitVal { get => -_zeroIndex; }
    public int MaxDigitVal{ get => _balanced ? _zeroIndex : Chars.Length - 1; }
    
    public BigInteger NewVal(int c) => _le ? Val + (_factor * DigitVal(c)) : Val * _base + DigitVal(c);

    public bool Add(int c) {
        if (IsPlaceholder(c)) {
            if (c == '.') {
                if (_den != null) return false; // already consumed a '.'
                _den = _le ? _factor : 1;
            }
            return true;
        }
        if (!IsDigit(c)) return false;
        if (!_le && _den != null) _den = _den * _base;
        Val = NewVal(c);
        _factor *= _base;
        return true;
    }

    public override string ToString() => 
        $"IntAcc: Base={_base}, Chars={Chars}, {(_le ? "Little" : "Big")} Endian{(_balanced ? ", Balanced" : "")}, Case {(IsCaseSignificant ? "Sensitive" : "Insensitive")}";

    public static string ToBase(Num n, string b) {
        var num = ToBase((n as Int)!.num, b);
        var den = "";
        if (n is Fix f && f.num != 0 && f.dec != 0) {
            den = ToBase(BigInteger.Pow(10, f.dec), b);
        } else if (n is Rat r && r.den != 1 && r.num != 0) {
            den = ToBase(r.den, b);
        }

        if (den != "") return num + "/" + den.Substring(den.IndexOf(b.ToLower().Last()) + 1);
        return num;
    }

    public static string ToBase(BigInteger n, string b) {
        // validate incoming b
        var reg = new Regex(@"^0([<>]|[+-~]|\[.*\]){0,3}[bcdefgknoqstvxyz]$", RegexOptions.IgnoreCase);
        if (!reg.IsMatch(b)) throw new FormatException($"Invalid base specifier {b}");

        // normalize b
        var charsStart = b.IndexOf('[');
        string? chars = null;
        var customCharset = false;
        if (charsStart > 0) {
            var charsEnd = b.LastIndexOf(']') - 1;
            var numChars = charsEnd - charsStart;
            if (numChars > 1) {
                chars = b.Substring(charsStart + 1, numChars);
                b = b.Remove(charsStart, numChars + 2);
                customCharset = true;
            }
        }

        b = b.Replace("+", "");
        var baseChar = Char.ToLower(b.Last());
        var isCEG = "ceg".Contains(baseChar);
        if (b.Contains('>') && isCEG) b = b.Replace(">", "");
        if (b.Contains('<') && !isCEG) b = b.Replace("<", "");
        if (b.Contains('~') && (!"cegky".Contains(baseChar) || b.Contains('-'))) b.Replace("~", "");

        var baseMult = b.Substring(1).Contains('-') ? -1 : 1;
        var le = b.Contains('>') || (isCEG && !b.Contains('<'));

        var acc = new IntAcc(baseChar, baseMult, le, chars);
        // Console.WriteLine($"Acc: {acc}");
        customCharset = acc.Chars != new IntAcc(baseChar, baseMult, le).Chars;

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
            var m = 0;
            if (scratch > max) m = (scratch > p ? -1 : 1); // too big case
            else if (scratch < min) m = (scratch > p ? 1 : -1); // too small case

            d = d + m; // adjust digit
            // if (m != 0) Console.WriteLine($"***** {scratch}, m={m}, d={d}, p={p} min={min} max={max}");
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
        if (customCharset) {
            if (b.Length == 0) b = "0d";
            b = b.Insert(b.Length - 1, $"[{acc.Chars}]");
        }

        str.Insert(0, b);
        if (mult < 0) str.Insert(0, '-');
        return str.ToString();
    }

    public static IntAcc Create(char b = 'd', int mult = 1, bool? le = null, string? charset = null) =>
        Char.ToLower(b) switch {
            'c' or 'e' or 'g' => new IntAcc(b, mult, le ?? true, charset),
            _ => new IntAcc(b, mult, le ?? false, charset)
        };
}
