using System.Numerics;

class Int {
    public BigInteger num = 0;
    public Int() : base() {}
    public Int(BigInteger num) => this.num = num;
    public override string ToString() => num.ToString();
    public static Int Parse(string val) => new Int(BigInteger.Parse(val));
}

class Fix : Int {
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

    public new static Fix Parse(string val) {
        var dp = val.Length - val.IndexOf('.');
        return new Fix(BigInteger.Parse(val.Remove(val.IndexOf('.'), 1)), dp);
    }
}

class Rat: Int {
    public BigInteger den;
    public Rat() : base() => this.den = 1;
    public Rat(BigInteger num) : base(num) => this.den = 1;
    public Rat(BigInteger num, BigInteger den) : base(num) { this.den = den; Normalize(); }
    private void Normalize() {
        BigInteger g;
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
    public new static Rat Parse(string val) {
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

class Comp : Rat {
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

class FComp : Fix {
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

public static class Tests {
        public static void TestNumbers() {
        var i = Int.Parse("100000000000000000000000000000000000000000000000002");
        Console.WriteLine(i.ToString());
        i = Fix.Parse("100000000000000000.00000000000000000");
        Console.WriteLine(i.ToString());
        i = Fix.Parse("100000.00000000100000");
        Console.WriteLine(i.ToString());
        i = Fix.Parse("100000.00000000100009");
        Console.WriteLine(i.ToString());
        var bi = BigInteger.Parse("999999129321000000");
        var dp = -25;

        while (dp <= 25) {
            i = new Fix(bi, dp++);
            Console.WriteLine(i.ToString());
        }

        var r = new Rat(10000, 1000);
        Console.WriteLine(r.ToString());  // "10"
        r = new Rat(169, 13);
        Console.WriteLine(r.ToString());  // "13"
        r = new Rat(712381, 32882);
        Console.WriteLine(r.ToString()); // "712381/32882"
        r = Rat.Parse(r.ToString());
        Console.WriteLine(r.ToString()); // "712381/32882"
        r = Rat.Parse("720/84");
        Console.WriteLine(r.ToString()); // "60/7"

        var badRats = new [] {"/1", "1/", "1/0"};
        foreach (var rat in badRats)
            try {
                r = Rat.Parse(rat);
            }
            catch (Exception e) {
                Console.WriteLine(e.Message);
            }
    }
}
