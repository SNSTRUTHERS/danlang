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
        return NumberParser.ParseString(s);
    }

    public int CompareTo(Num? obj) { // TODO: do a proper implementation!
        if (this is Int i && obj is Int i2) return i.num.CompareTo(i2.num);
        return 0;
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
}

public class Comp : Num {
    public Int r { get; private set; }
    public Int im { get; private set; }
    public Comp() { r = new Int(); im = new Int(); }
    public Comp(Int r) { this.r = r; this.im = new Int(); }
    public Comp(Int r, Int im) { this.r = r; this.im = im; }

    public override string ToString() {
        if (im.num == BigInteger.Zero) return r.ToString();
        if (r.num == BigInteger.Zero) return im.ToString() + 'i';
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

        return new Comp(rPart ?? new Int(BigInteger.Zero), imPart ?? new Int(BigInteger.Zero));
    }
}
