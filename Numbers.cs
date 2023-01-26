using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography;
using System.Numerics;
using System.Text.RegularExpressions;

public static class NumExtensions
{
    public static Num? ToNum(this string s) {
        return Num.Parse(s);
    }
}

public class Num : IComparable<Num>, IComparable<int> {
    private const string _binRegex = "[+-]?0b[01]+";
    private const string _balTernRegex = @"0c[-0+]+(\.[-0+]+)?";
    private const string _decRegex = @"[+-]?(0d)?\d+([\./]\d+)?(e\d+)?([+-]\d+([\./]\d+)?(e\d+)?i)?|[+-]?(0d)?\d+([\./]\d+)?(e\d+)?i([+-]\d+([\./]\d+)?(e\d+)?)?";
    private const string _octRegex = "[+-]?0o[0-7]+";
    private const string _quatRegex = "[+-]?0q[0-3]+";
    private const string _terRegex = "[+-]?0t[012]+";
    private const string _hexRegex = "[+-]?0x[0-9a-f]+";
    private const string _b36Regex = "[+-]?0z[0-9a-z]+";
    private static readonly Regex _numRegex = new Regex(
        $"^({_binRegex}|{_decRegex}|{_octRegex}|{_quatRegex}|{_terRegex}|{_balTernRegex}|{_hexRegex}|{_b36Regex})$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    protected Num() {}

    public static Num operator-(Num n) {
        return n switch {
            Comp c => -c,
            Rat r => -r,
            Fix f => -f,
            Int i => -i,
            _ => throw new Exception("Unknown number type")
        };
    }

    public Int ToInt() {
        return this switch {
            Comp c => c.r.ToInt(),
            Rat r => new Int(r.num / r.den),
            Fix f => new Int(f.num / BigInteger.Pow(10, f.dec)),
            _ => (this as Int)!
        };
    }


    public static Num operator*(Num n, BigInteger m) {
        if (n is Rat r) return new Rat(r.num * m, r.den);
        if (n is Fix f) return new Fix(f.num * m, f.dec);
        return new Int(((Int)n).num * m);
    }

    public static Num operator+(Num n, BigInteger m) {
        if (n is Rat r) return new Rat(r.num + (m * r.den), r.den);
        if (n is Fix f) return new Fix(f.num + (m * BigInteger.Pow(10, f.dec)), f.dec);
        if (n is Int i) return new Int(i.num + m);
        else return n;
    }

    public static Num operator/(Num n, BigInteger m) {
        if (n is Rat r) return new Rat(r.num, r.den * m);
        if (n is Fix f) return Rat.ToRat(f) / m;
        else return new Rat(((Int)n).num, m);
    }

    public static Num operator-(Num n, BigInteger m) {
        return n + (-m);
    }

    public static Num operator*(Num n, Num m) {
        if (m is Rat r) return Rat.ToRat(n) * r;
        if (m is Fix f) return n switch {
            Rat r2 => r2 * Rat.ToRat(m),
            Fix f2 => new Fix(f.num * f2.num, f.dec + f2.dec),
            Int i => new Fix(f.num * i.num, f.dec),
            _ => new Fix() // Complex?
        };
        return n * ((Int)m).num;
    }

    public static Num operator+(Num n, Num m) {
        if (n is Rat r) return r + Rat.ToRat(m);
        if (m is Rat r2) return Rat.ToRat(n) + r2;
        if (m is Fix f) return Rat.ToRat(m) + Rat.ToRat(n);
        return n + ((Int)m).num;
    }

    public static Num operator/(Num n, Num m) {
        if (n is Rat r) return r / Rat.ToRat(m);
        if (m is Rat r2) return n * new Rat(r2.den, r2.num);
        if (m is Fix f) return Rat.ToRat(n) / Rat.ToRat(m);
        return n / ((Int)m).num;
    }

    public static Num operator-(Num n, Num m) {
        if (m is Rat r) return -r + n;
        if (m is Fix f) return -f + n;
        if (m is Int i) return n - i.num;
        return -n;
    }

    public static Num? Parse(string? s) {
        if (s == null) return null;

        var s2 = s.Trim().Replace("_", ""); // trim and remove any underscores
        if (!_numRegex.IsMatch(s2)) throw new FormatException($"String `{s2}` does not match the format for a Num type");

        if (s2.Contains('i')) {
            return Comp.Parse(s2);
        }

        var splits = s2.Split('/');
        if (splits.Length > 1) { // rational
            var t0 = Parser.Tokenize(new StringReader(splits[0])).First();
            if (t0.type == Parser.Token.Type.Number && t0.num is Int) {
                var t1 = Parser.Tokenize(new StringReader(splits[1])).First();
                if (t1.type == Parser.Token.Type.Number && t1.num is Int) {
                    return new Rat((t0.num as Int)!.num, (t1.num as Int)!.num);
                }
            }
            throw new FormatException($"Failed to parse rational {s2}");
        }

        if (s2.Contains('.')) {
            return Fix.Parse(s2);
        }

        var t = Parser.Tokenize(new StringReader(s2)).First();
        return t.num;
    }

    public int CompareTo(Num? obj) { // TODO: do a proper implementation!
        if (this is Int i && obj is Int i2) return i.num.CompareTo(i2.num);
        return 0;
    }

    public int CompareTo(int other) => this.CompareTo(new Int(other));
}

public class Int : Num {
    public BigInteger num { get; protected set; } = 0;
    public Int() : base() {}
    public Int(BigInteger num) => this.num = num;

    public static explicit operator double(Int r) => (double)r.num;
    
    public static explicit operator long(Int r) => (long)r.num;

    public override string ToString() => num.ToString();

    public new static Num? Parse(string? s) => s == null ? null : new Int(BigInteger.Parse(s));

    public static Int operator-(Int i) => new Int(-i.num);
}

public class Fix : Int {
    public int dec { get; private set; } = 0;

    public Fix() {}
    public Fix(Fix f) : this(f.num, f.dec) {}
    public Fix(BigInteger num) : this(num, 0) {}
    public Fix(BigInteger num, int dec) : base(num) { this.dec = dec; Normalize(); }

    private void Normalize() {
        while (dec < 0) {
            ++dec;
            num *= 10;
        }
        while (dec > 0 && (num % 10) == 0) {
            --dec;
            num /= 10;
        }
    }

    public override string ToString() {
        var str = base.ToString();
        if (dec == 0) return str;

        var sign = string.Empty;
        if (str.StartsWith('-')) {
            sign = "-";
            str = str.Substring(1);
        }
        var p = str.Length - dec;
        if (p <= 0) { // leading zeros required
            return $"{sign}0.{new String('0', -p)}{str}";
        }
        return $"{sign}{str.Insert(p, ".")}";
    }

    public new static Fix? Parse(string? val) {
        if (val == null) return null;

        val = val.Replace("_", "");
        if (val.StartsWith('.')) throw new FormatException($"Fix Nums cannot start with a '.'");
        if (val.EndsWith('.')) throw new FormatException($"Fix Nums cannot end with a '.'");
        if (val.Count(c => c == '.') > 1) throw new FormatException("Fix Nums cannot have > 1 '.'");
        
        var dotOffset = val.IndexOf('.');
        if (dotOffset < 0) return new Fix(BigInteger.Parse(val));
        var dp = val.Length - dotOffset - 1;
        return new Fix(BigInteger.Parse(val.Remove(dotOffset, 1)), dp);
    }

    public static explicit operator double(Fix r) => (double)r.num / Math.Pow(10, r.dec);
    
    public static explicit operator long(Fix r) => (long)(double)r;

    public static Fix operator-(Fix f) {
        return new Fix(-f.num, f.dec);
    }
}

public enum Rounding {
    Truncate, RoundUp, RoundDown, RoundAwayFromZero
}

public class Rat: Int {
    public BigInteger den { get; private set; }
    public Rat() : base() => this.den = 1;

    public Rat(Int? r) : this(r?.num ?? 0) {
        if (r is Rat) den = ((Rat)r)?.den ?? 1;
        else if (r is Fix) {
            den = 1;
            var dec = ((Fix)r)?.dec ?? 0;
            while (dec-- > 0) den *= 10;
        }
    }

    public Rat(BigInteger num) : base(num) => this.den = 1;
    public Rat(BigInteger num, BigInteger den) : base(num) { this.den = den; Normalize(); }
    private void Normalize() {
        BigInteger g;
        if (den < 0) {
            num = -num;
            den = -den;
        }
        
        while ((g = BigInteger.GreatestCommonDivisor(num, den)) > 1) {
            den /= g;
            num /= g;
        }
    }

    public override string ToString() {
        if (den != 1) {
            return $"{base.ToString()}/{den}";
        }
        return base.ToString();
    }

    public string ToString(string format) {
        var w = num / den;
        if (format.ToUpper() != "M" || w == 0) return ToString();
        var r = num % den;
        return $"{w} {BigInteger.Abs(r)}/{den}";
    }

    public static explicit operator double(Rat r) => (double)r.num / (double)r.den;
    public static explicit operator long(Rat r) => (long)(double)r;

    public static Rat operator+(Int r1, Rat r2) {
        return r2 + r1;
    }

    public static Rat operator+(Rat r1, Rat r2) {
        return new Rat(r1.num * r2.den + r2.num * r1.den, r1.den * r2.den);
    }

    public static Rat ToRat(Num r2) => r2 switch { 
        Rat r => r,
        Fix f => new Rat(f.num, BigInteger.Pow(10, f.dec)),
        Int i => new Rat(i),
        _ => new Rat()
    };
    
    public Fix ToFix(int places = 10, Rounding round = Rounding.Truncate) {
        var mult = num > 0 ? 1 : -1;
        num = num * mult;
        var w = num / den;
        var r = num % den;
        var dec = 0;
        while (r > 0 && dec < places) {
            ++dec;
            r = r * 10;
            w = (w * 10) + (r / den);
            r = r % den;
        }

        if (r >= (den / 2)) {
            w = round switch {
                Rounding.RoundUp => mult > 0 ? w + 1 : w,
                Rounding.RoundDown => mult < 0 ? w + 1 : w,
                Rounding.RoundAwayFromZero => w + 1,
                _ => w,
            };
        }

        return new Fix(w * mult, dec);
    }

    public static Rat operator+(Rat r1, Int r2) {
        var rT = ToRat(r2);
        return new Rat(r1.num * rT.den + rT.num * r1.den, r1.den * rT.den);
    }

    public static Rat operator-(Int r1, Rat r2) {
        var rT = ToRat(r1);
        return rT + (-r2);
    }

    public static Rat operator-(Rat r) {
        return new Rat(-r.num, r.den);
    }

    public static Rat operator-(Rat r1, Int r2) {
        var rT = ToRat(r2);
        return r1 + (-rT);
    }

    public static Rat operator-(Rat r1, Rat r2) {
        return r1 + (-r2);
    }

    public static Rat operator*(Rat r1, Int r2) {
        return new Rat(r1.num * r2.num, r1.den);
    }

    public static Rat operator*(Int r1, Rat r2) {
        return r2 * r1;
    }

    public static Rat operator*(Rat r1, Rat r2) {
        return new Rat(r1.num * r2.num, r1.den * r2.den);
    }

    public static Rat operator/(Rat r1, Int r2) {
        return new Rat(r1.num, r1.den * r2.num);
    }

    public static Rat operator/(Int r1, Rat r2) {
        return new Rat(r1.num * r2.den, r2.num);
    }

    public static Rat operator/(Rat r1, Rat r2) {
        return new Rat(r1.num * r2.den, r1.den * r2.num);
    }

    public new static Rat? Parse(string? val) {
        if (val == null) return null;
        if (val.Count(c => c == '/') > 1) throw new FormatException("Rational numbers cannot have > 1 '/' character");
        var sPos = val.IndexOf('/');
        if (sPos == 0 || sPos == val.Length -1) throw new FormatException("Invalid '/' position in rational number");

        // no '/' found
        if (sPos < 0) return new Rat(BigInteger.Parse(val));

        // single '/' found
        var den =  BigInteger.Parse(val.Substring(sPos + 1));
        if (den == 0) throw new FormatException("Cannot specify a rational with a denominator of zero (0)");
        return new Rat(BigInteger.Parse(val.Substring(0, sPos)), den);
    }
}

public class Comp : Num {
    public Int r { get; private set; }
    public Int im { get; private set; }
    public Comp() { r = new Int(); im = new Int(); }
    public Comp(Int r) { this.r = r; this.im = new Int(); }
    public Comp(Int r, Int im) { this.r = r; this.im = im; }

    public override string ToString() {
        if (im.num == 0) return r.ToString();
        return $"{r.ToString()}{(im.num > 0 ? "+" : "")}{im.ToString()}i";
    }

    public new static Comp? Parse(string? s) {
        if (s == null) return null;
        s = Regex.Replace(s, @"[\t _]", "").ToLower();
        if (s.Length == 0) return null;

        if (!(s.StartsWith('+') || s.StartsWith('-'))) {
            s = "+" + s;
        }

        var partCount = s.Count(c => "+-".Contains(c));
        if (partCount > 2) throw new FormatException("Too many separators found in Comp Num");
        if (s.Count(c => c == 'i') > 1) throw new FormatException("Too many imaginary parts found in Comp Num");

        Int? rPart = null;
        Int? imPart = null;

        int iOffset = s.IndexOf('i');
        s = s.Replace("i", "");
        if (partCount == 1) {
            if (iOffset < 0) {
                rPart = Num.Parse(s) as Int;
            }
            else {
                imPart = Num.Parse(s) as Int;
            }
        }
        else {
            if (iOffset == -1) throw new FormatException("No imaginary part in two-part Comp Num");
    
            var signs = s.Where(c => "+-".Contains(c)).ToArray();
            var nums = s.Split(new char[] {'+', '-'}, StringSplitOptions.RemoveEmptyEntries)
                .Select((part, i) => Num.Parse($"{signs[i]}{part}"))
                .ToArray();
            rPart = (iOffset == s.Length ? nums[0] : nums[1]) as Int;
            imPart = (iOffset != s.Length ? nums[0] : nums[1]) as Int;
        }

        return new Comp(rPart ?? new Int(0), imPart ?? new Int(0));
    }
}

public class FComp : Fix {
    public Fix im;
    public FComp() : base() => im = new Fix();
    public FComp(BigInteger num) : base(num) => im = new Fix();
    public FComp(Fix r, Fix im) : base(r) => this.im = im;
    public FComp(BigInteger num, int dec) : base(num, dec) => im = new Fix();
    public FComp(BigInteger numR, int decR, BigInteger numI, int decI) : base(numR, decR)
        => im = new Fix(numI, decI);
    public override string ToString() {
        if (im.num == 0) return base.ToString();
        return $"{base.ToString()}{(im.num > 0 ? "+" : "")}{im.ToString()}i";
    }

    public static new FComp? Parse(string? s) {
        if (s == null) return null;
        s = s.Trim().Replace("_", "");
        if (!s.EndsWith('i')) throw new FormatException();
        s = s.Remove(s.Length - 1); // trim the i

        var realSign = '+';
        if (s.StartsWith('+') || s.StartsWith('-')) {
            realSign = s[0];
            s = s.Substring(1);
        }
        
        var imaginarySign = '+';
        var splits = s.Split(imaginarySign);
        if (splits.Length == 1)
        {
            imaginarySign = '-';
            splits = s.Split(imaginarySign);
            if (splits.Length == 1) throw new FormatException("No real/imaginary separator character (+/-) found in FComp Num");
        }
        
        if (splits.Length > 2) throw new FormatException("Too many separators found in FComp Num");

        var f1 = Fix.Parse(splits[0]);
        var f2 = Fix.Parse(splits[1]);
        if (f1 == null || f2 == null) return null;

        if (realSign == '-') f1 = -f1;
        if (imaginarySign == '-') f2 = -f2;
        return new FComp(f1, f2);
    }
}
