using System.Numerics;
using System.Text.RegularExpressions;

public static class NumExtensions
{
    public static Num? ToNum(this string s) {
        return Num.Parse(s);
    }
}

public class Num : IComparable<Num>, IComparable<BigInteger>, IComparable<long> {
    protected Num() {}

    public static Num Zero = new Int(BigInteger.Zero);

    public static Num operator-(Num n) {
        return n switch {
            Comp c => new Comp((Int)(-(Num)c.r), (Int)(-(Num)c.im)),
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

    public bool IsZero => this switch {
        Comp c => c.r.IsZero && c.im.IsZero,
        Int i => i.num.IsZero,
        _ => false
    };

    // A result as the simplest kind that holds it: a rational with a denominator of 1 is an integer, a complex number
    // with no imaginary part a real one
    public static Num Norm(Num n) => n switch {
        Comp c when c.im.IsZero => Norm(c.r),
        Rat r when r.den == 1 => new Int(r.num),
        _ => n
    };

    // The arithmetic: a result is complex if either number is; else rational if either is; else fixed (decimal) if
    // either is; else an integer.  A quotient is exact: a rational (or an integer, if it's whole), complex or not
    public static Num operator*(Num n, BigInteger m) => n * (Num)new Int(m);
    public static Num operator+(Num n, BigInteger m) => n + (Num)new Int(m);
    public static Num operator/(Num n, BigInteger m) => n / (Num)new Int(m);
    public static Num operator-(Num n, BigInteger m) => n - (Num)new Int(m);

    public static Num operator*(Num n, Num m) {
        if (n.GetType() == typeof(Int) && m.GetType() == typeof(Int)) return new Int(((Int)n).num * ((Int)m).num);
        if (n is Comp || m is Comp) return Comp.Mul(Comp.Of(n), Comp.Of(m));
        if (n is Rat || m is Rat) return Norm(Rat.ToRat(n) * Rat.ToRat(m));
        if (n is Fix || m is Fix) {
            var f = Fix.Of((Int)n);
            var g = Fix.Of((Int)m);
            return new Fix(f.num * g.num, f.dec + g.dec);
        }
        return new Int(((Int)n).num * ((Int)m).num);
    }

    public static Num operator+(Num n, Num m) {
        if (n.GetType() == typeof(Int) && m.GetType() == typeof(Int)) return new Int(((Int)n).num + ((Int)m).num);
        if (n is Comp || m is Comp) return Comp.Add(Comp.Of(n), Comp.Of(m));
        if (n is Rat || m is Rat) return Norm(Rat.ToRat(n) + Rat.ToRat(m));
        if (n is Fix || m is Fix) return Fix.Add(Fix.Of((Int)n), Fix.Of((Int)m));
        return new Int(((Int)n).num + ((Int)m).num);
    }

    public static Num operator/(Num n, Num m) {
        if (m.IsZero) throw new DivideByZeroException("Division by zero.");
        if (n is Comp || m is Comp) return Comp.Div(Comp.Of(n), Comp.Of(m));
        return Norm(Rat.ToRat(n) / Rat.ToRat(m));
    }

    public static Num operator-(Num n, Num m) => n + (-m);

    public static Num? Parse(string? s) {
        return NumberParser.ParseString(s);
    }

    // Numbers compared by value, whatever their kinds (1/2 and 0.5 are equal); complex numbers by real part, then
    // imaginary part
    public int CompareTo(Num? obj) {
        if (obj is null) return 1;
        if (GetType() == typeof(Int) && obj.GetType() == typeof(Int)) return ((Int)this).num.CompareTo(((Int)obj).num);
        if (this is Comp || obj is Comp) {
            var a = Comp.Of(this);
            var b = Comp.Of(obj);
            var c = a.r.CompareTo(b.r);
            return c != 0 ? c : a.im.CompareTo(b.im);
        }
        if (this is Rat || obj is Rat || this is Fix || obj is Fix) {
            var x = Rat.ToRat(this);
            var y = Rat.ToRat(obj);
            return (x.num * y.den).CompareTo(y.num * x.den);
        }
        return ((Int)this).num.CompareTo(((Int)obj).num);
    }

    public int CompareTo(BigInteger other) => this.CompareTo(new Int(other));

    public int CompareTo(long other) => CompareTo(new BigInteger(other));
}

public class Int : Num {
    public BigInteger num { get; protected set; } = 0;
    public Int() : base() {}
    public Int(BigInteger? num = null) => this.num = num ?? BigInteger.Zero;

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

    public static explicit operator double(Fix r) => (double)r.num / Math.Pow(10, r.dec);
    
    public static explicit operator long(Fix r) => (long)(double)r;

    public static Fix operator-(Fix f) {
        return new Fix(-f.num, f.dec);
    }

    // An integer as a fixed number (with no places), or the fixed number itself
    public static Fix Of(Int i) => i as Fix ?? new Fix(i.num, 0);

    // A sum, its places the more of the two's
    public static Fix Add(Fix a, Fix b) {
        var d = Math.Max(a.dec, b.dec);
        return new Fix(a.num * BigInteger.Pow(10, d - a.dec) + b.num * BigInteger.Pow(10, d - b.dec), d);
    }
}

public enum Rounding {
    Truncate, RoundUp, RoundDown, RoundAwayFromZero
}

public class Rat : Int {
    public BigInteger den { get; private set; }
    public Rat() : base() => this.den = 1;

    public Rat(Int? r) : this(r?.num ?? 0) {
        if (r is Rat r2) den = r2.den;
        else if (r is Fix f) {
            den = 1;
            var dec = f.dec;
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
        var n = num * mult;     // (its own: this number is unchanged)
        var w = n / den;
        var r = n % den;
        var dec = 0;
        while (r > 0 && dec < places) {
            ++dec;
            r = r * 10;
            w = (w * 10) + (r / den);
            r = r % den;
        }

        if (r > 0 && r * 2 >= den) {
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
}

public class Comp : Num {
    public Int r { get; private set; }
    public Int im { get; private set; }
    public Comp() { r = new Int(); im = new Int(); }
    public Comp(Int r) { this.r = r; this.im = new Int(); }
    public Comp(Int r, Int im) { this.r = r; this.im = im; }

    public override string ToString() {
        if (im.IsZero) return r.ToString();
        if (r.IsZero) return im.ToString() + 'i';
        return $"{r.ToString()}{(im.CompareTo(Num.Zero) > 0 ? "+" : "")}{im.ToString()}i";
    }

    // A number as a complex one (a real one with no imaginary part)
    public static Comp Of(Num n) => n as Comp ?? new Comp((Int)n);

    // The arithmetic, its parts' through Num's operators (each part is an Int, a Fix or a Rat)
    public static Num Add(Comp a, Comp b) => Num.Norm(new Comp((Int)((Num)a.r + b.r), (Int)((Num)a.im + b.im)));

    public static Num Mul(Comp a, Comp b) => Num.Norm(new Comp(
        (Int)((Num)a.r * b.r - (Num)a.im * b.im),
        (Int)((Num)a.r * b.im + (Num)a.im * b.r)));

    public static Num Div(Comp a, Comp b) {
        var d = (Num)b.r * b.r + (Num)b.im * b.im;
        return Num.Norm(new Comp(
            (Int)(((Num)a.r * b.r + (Num)a.im * b.im) / d),
            (Int)(((Num)a.im * b.r - (Num)a.r * b.im) / d)));
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

        return new Comp(rPart ?? new Int(BigInteger.Zero), imPart ?? new Int(BigInteger.Zero));
    }
}
