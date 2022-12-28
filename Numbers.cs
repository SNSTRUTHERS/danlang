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
    public BigInteger num = 0;
    public override string ToString() => num.ToString();
    public static Num? Parse(string? s) {
        if (s == null) return null;
        var splits = s.Split('/');
        if (splits.Length > 1) { // rational
            var t0 = Parser.Tokenize(new StringReader(splits[0])).First();
            if (t0.type == Parser.Token.Type.Number) {
                var t1 = Parser.Tokenize(new StringReader(splits[1])).First();
                if (t1.type == Parser.Token.Type.Number && t1.num != 0) {
                    return new Rat(t0.num ?? 0, t1.num ?? 0);
                }
            }
            throw new FormatException($"Failed to parse rational {s}");
        }
        
        if (s.Contains('.')) return Fix.Parse(s);
        var t = Parser.Tokenize(new StringReader(s)).First();
        return new Int(t?.num ?? 0);
    }
}

public class Int : Num {
    public Int() : base() {}
    public Int(BigInteger num) => this.num = num;
    public new static Num? Parse(string? s) => s == null ? null : new Int(BigInteger.Parse(s));
}

public class Fix : Int {
    public int dec = 0;

    public Fix() {}
    public Fix(BigInteger num) : this(num, 0) {}
    public Fix(BigInteger num, int dec) { this.num = num; this.dec = dec; Normalize(); }

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

    public new static Num? Parse(string? val) {
        if (val == null) return null;
        var dp = val.Length - val.IndexOf('.');
        return new Fix(BigInteger.Parse(val.Remove(val.IndexOf('.'), 1)), dp);
    }
}

public class Rat: Int {
    public BigInteger den;
    public Rat() : base() => this.den = 1;

    public Rat(Num? r) : this(r?.num ?? 0) {
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

    public static Rat operator+(Num r1, Rat r2) {
        return r2 + r1;
    }

    public static Rat operator+(Rat r1, Rat r2) {
        return new Rat(r1.num * r2.den + r2.num * r1.den, r1.den * r2.den);
    }

    public static Rat ToRat(Rat r2) => r2;
    public static Rat ToRat(Num r2) => new Rat(r2);

    public static Rat operator+(Rat r1, Num r2) {
        var rT = ToRat(r2);
        return new Rat(r1.num * rT.den + rT.num * r1.den, r1.den * rT.den);
    }

    public static Rat operator-(Num r1, Rat r2) {
        var rT = ToRat(r1);
        return new Rat(rT.num * r2.den - r2.num * rT.den, r2.den * rT.den);
    }

    public static Rat operator-(Rat r) {
        return new Rat(-r.num, r.den);
    }

    public static Rat operator-(Rat r1, Num r2) {
        var rT = ToRat(r2);
        return r1 + (-rT);
    }

    public static Rat operator*(Rat r1, Num r2) {
        var rT = ToRat(r2);
        return new Rat(r1.num * rT.num, r1.den * rT.den);
    }

    public static Rat operator*(Num r1, Rat r2) {
        return r2 * r1;
    }

    public static Rat operator/(Rat r1, Num r2) {
        var rT = ToRat(r2);
        return new Rat(r1.num * rT.den, r1.den * rT.num);
    }

    public static Rat operator/(Num r1, Rat r2) {
        var rT = ToRat(r1);
        return new Rat(rT.num * r2.den, rT.den * r2.num);
    }

    public new static Num? Parse(string? val) {
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

public class Comp : Rat {
    public Comp() : base() => im = new Rat();
    public Comp(BigInteger num) : base(num) => im = new Rat();
    public Comp(BigInteger num, BigInteger den) : base(num, den) => im = new Rat();
    public Comp(BigInteger numR, BigInteger denR, BigInteger numI, BigInteger denI) : base(numR, denR)
        => im = new Rat(numI, denI);
    public override string ToString() {
        if (im.num == 0) return base.ToString();
        return $"{base.ToString()}{(im.num > 0 ? '-' : '+')}{im.ToString()}i";
    }
    public Rat im;
}

public class FComp : Fix {
    public FComp() : base() => im = new Fix();
    public FComp(BigInteger num) : base(num) => im = new Fix();
    public FComp(BigInteger num, int dec) : base(num, dec) => im = new Fix();
    public FComp(BigInteger numR, int decR, BigInteger numI, int decI) : base(numR, decR)
        => im = new Fix(numI, decI);
    public override string ToString() {
        if (im.num == 0) return base.ToString();
        return $"{base.ToString()}{(im.num > 0 ? '-' : '+')}{im.ToString()}i";
    }
    public Fix im;
}
