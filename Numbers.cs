using System.Security.Principal;
using System.Numerics;
using System.Text.RegularExpressions;

public static class NumExtensions
{
    public static Num? ToNum(this string s) {
        return Num.Parse(s);
    }
}

public class Num {
    private const string _binRegex = "[+-]?0b[01]+";
    private const string _balTernRegex = @"0c[-0+]+(\.[-0+]+)?";
    private const string _decRegex = @"[+-]?(0d)?\d+([\./]\d+)?(e\d+)?([+-]\d+([\./]\d+)?(e\d+)?i)?";
    private const string _octRegex = "[+-]?0o[0-7]+";
    private const string _quatRegex = "[+-]?0q[0-3]+";
    private const string _terRegex = "[+-]?0t[012]+";
    private const string _hexRegex = "[+-]?0x[0-9a-f]+";
    private const string _b36Regex = "[+-]?0z[0-9a-z]+";
    private static readonly Regex _numRegex = new Regex(
        $"^({_binRegex}|{_decRegex}|{_octRegex}|{_quatRegex}|{_terRegex}|{_balTernRegex}|{_hexRegex}|{_b36Regex})$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    public Num() {}

    public static Num operator*(Num n, int m) {
        if (n is Rat) return new Rat((n as Rat)!.num * m, (n as Rat)!.den);
        if (n is Fix) return new Fix((n as Fix)!.num * m, (n as Fix)!.dec);
        if (n is Int) return new Int((n as Int)!.num * m);
        return new Int(m);
    }

    public static Num? Parse(string? s) {
        if (s == null) return null;

        var s2 = s.Trim().Replace("_", ""); // trim and remove any underscores
        if (!_numRegex.IsMatch(s2)) throw new FormatException($"String `{s2}` does not match the format for a Num type");

        if (s2.EndsWith('i')) {
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
}

public class Int : Num {
    public BigInteger num = 0;
    public Int() : base() {}
    public Int(BigInteger num) => this.num = num;

    public static explicit operator double(Int r) => (double)r.num;
    
    public static explicit operator long(Int r) => (long)r.num;

    public override string ToString() => num.ToString();

    public new static Num? Parse(string? s) => s == null ? null : new Int(BigInteger.Parse(s));

    public static Int operator-(Int i) => new Int(-i.num);
}

public class Fix : Int {
    public int dec = 0;

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

public class Rat: Int {
    public BigInteger den;
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
            return $"{base.ToString()}/{den.ToString()}";
        }
        return base.ToString();
    }

    public static explicit operator double(Rat r) => (double)r.num / (double)r.den;
    public static explicit operator long(Rat r) => (long)(double)r;

    public static Rat operator+(Int r1, Rat r2) {
        return r2 + r1;
    }

    public static Rat operator+(Rat r1, Rat r2) {
        return new Rat(r1.num * r2.den + r2.num * r1.den, r1.den * r2.den);
    }

    public static Rat ToRat(Rat r2) => r2;
    public static Rat ToRat(Int r2) => new Rat(r2);

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
        var rT = ToRat(r2);
        return r1 * rT;
    }

    public static Rat operator*(Int r1, Rat r2) {
        return r2 * r1;
    }

    public static Rat operator*(Rat r1, Rat r2) {
        return new Rat(r1.num * r2.num, r1.den * r2.den);
    }

    public static Rat operator/(Rat r1, Int r2) {
        var rT = ToRat(r2);
        return r1 / rT;
    }

    public static Rat operator/(Int r1, Rat r2) {
        var rT = ToRat(r1);
        return rT / r2;
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
    public Int r;
    public Int im;
    public Comp() { r = new Int(); im = new Int(); }
    public Comp(Int r) { this.r = r; this.im = new Int(); }
    public Comp(Int r, Int im) { this.r = r; this.im = im; }

    public override string ToString() {
        if (im.num == 0) return r.ToString();
        return $"{r.ToString()}{(im.num > 0 ? "+" : "")}{im.ToString()}i";
    }

    public new static Comp? Parse(string? s) {
        if (s == null) return null;
        s = s.Trim().Replace("_", "");
        if (!s.EndsWith('i')) throw new FormatException("Comp Nums must end in 'i'");
        s = s.Remove(s.Length - 1); // trim the i

        var realSign = '+';
        if (s.StartsWith('+') || s.StartsWith('-')) {
            realSign = s[0];
            s = s.Substring(1);
        }
        
        var splits = s.Split('+', '-');
        if (splits.Length == 1) throw new FormatException("No real/imaginary separator character (+/-) found in Comp Num");
        if (splits.Length > 2) throw new FormatException("Too many separators found in Comp Num");

        var r1 = Num.Parse($"{realSign}{splits[0]}") as Int;
        var r2 = Num.Parse($"{s.First(c => "+-".Contains(c))}{splits[1]}") as Int;
        if (r1 == null || r2 == null) return null;

        return new Comp(r1, r2);
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
