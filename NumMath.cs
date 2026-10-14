using System.Numerics;

// The math functions (hydra-2's reborn/docs/design/plans/NUMBERS.md, "The math functions"): sqrt, exp, log, sin, cos,
// tan, atan, pi and pow of any real power.  Exact when the answer is (a square root of a perfect square, exp 0, log 1,
// sin 0 ..., a power whole, or a root that's rational); else a fixed decimal of the precision's significant digits
// (Digits: 12 at the start), correctly rounded: the value worked in binary fixed point with a bound on its error, and
// worked again with more bits till everything the bound allows rounds the same (Ziv's way).  So any way of working it
// that's right gives the same digits: the Hydra's math library's, and sim/tools/numref.js's.  A complex argument
// (or a complex power, or a negative number to a power that isn't whole) gives the principal value, as mathematics
// defines it, each part so rounded; abs of a complex number is its modulus.
public class MathError : Exception {
    public MathError(string message) : base(message) {}
}

public static class NumMath {
    public static int Digits = 12;
    public const int MaxDigits = 100;
    public const int ExpMost = 20000;                   // (exp's argument, and a power's y log x, at most this far from 0)

    // ---- Integers

    public static int BitLen(BigInteger v) => v.IsZero ? 0 : (int)BigInteger.Abs(v).GetBitLength();

    // The floor of the square root of n (0 or more)
    public static BigInteger Isqrt(BigInteger n) {
        if (n.Sign <= 0) return BigInteger.Zero;
        var x = BigInteger.One << ((BitLen(n) + 1) / 2);   // (above the root)
        for (;;) {
            var y = (x + n / x) >> 1;
            if (y >= x) return x;
            x = y;
        }
    }

    // The floor of n's kth root (n 0 or more, k 1 or more)
    public static BigInteger Iroot(BigInteger n, BigInteger k) {
        if (n.Sign <= 0) return BigInteger.Zero;
        if (k.IsOne) return n;
        if (k >= BitLen(n)) return BigInteger.One;         // (2^k is past n)
        var kk = (int)k;
        var x = BigInteger.One << ((BitLen(n) + kk - 1) / kk);   // (above the root)
        for (;;) {
            var y = ((kk - 1) * x + n / BigInteger.Pow(x, kk - 1)) / kk;
            if (y >= x) return x;
            x = y;
        }
    }

    static BigInteger FloorDiv(BigInteger a, BigInteger b) {
        var q = BigInteger.DivRem(a, b, out var r);
        if (!r.IsZero && (r.Sign < 0) != (b.Sign < 0)) q -= 1;
        return q;
    }

    // An exact real number at b bits: x times 2^b, cut toward 0 (an error under 1 ulp)
    static BigInteger Fixed(Rat x, int b) => (x.num << b) / x.den;

    // ---- Constants at b bits, each within 2 ulps

    // atan(1/m) times 2^b, an error of at most an ulp a term
    static BigInteger AtanInv(int m, int b) {
        BigInteger t = (BigInteger.One << b) / m, sum = BigInteger.Zero, m2 = m * m;
        for (var k = 0; !t.IsZero; k++) {
            var term = t / (2 * k + 1);
            sum = (k & 1) == 0 ? sum + term : sum - term;
            t /= m2;
        }
        return sum;
    }

    // atanh(1/m) times 2^b, likewise
    static BigInteger AtanhInv(int m, int b) {
        BigInteger t = (BigInteger.One << b) / m, sum = BigInteger.Zero, m2 = m * m;
        for (var k = 0; !t.IsZero; k++) {
            sum += t / (2 * k + 1);
            t /= m2;
        }
        return sum;
    }

    static int Guard(int b) => BitLen(b) + 10;

    public static BigInteger PiFixed(int b) {             // pi = 16 atan(1/5) - 4 atan(1/239), Machin's
        var g = Guard(b);
        return (16 * AtanInv(5, b + g) - 4 * AtanInv(239, b + g)) >> g;
    }

    public static BigInteger Ln2Fixed(int b) {            // log 2 = 2 atanh(1/3)
        var g = Guard(b);
        return (2 * AtanhInv(3, b + g)) >> g;
    }

    public static BigInteger Ln10Fixed(int b) {           // log 10 = 3 log 2 + log 5/4 = 6 atanh(1/3) + 2 atanh(1/9)
        var g = Guard(b);
        return (6 * AtanhInv(3, b + g) + 2 * AtanhInv(9, b + g)) >> g;
    }

    // ---- Rounding to the precision

    // round(x 10^s / 2^b), x 0 or more
    static BigInteger RoundScaled(BigInteger x, int b, int s) {
        BigInteger num = x, den = BigInteger.One << b;
        if (s >= 0) num *= BigInteger.Pow(10, s);
        else den *= BigInteger.Pow(10, -s);
        return (2 * num + den) / (2 * den);
    }

    // x / 2^b times 10^k10 (x above 0) rounded to n significant digits: R (n digits) and its places p, the value R 10^-p
    static (BigInteger R, int p) Sig(BigInteger x, int b, int k10, int n) {
        var e = (int)Math.Floor((BitLen(x) - 1 - b) * 0.30102999566398120) + k10;   // (log10 of it, near enough)
        var p = n - 1 - e;
        BigInteger low = BigInteger.Pow(10, n - 1), high = low * 10;
        for (;;) {
            var R = RoundScaled(x, b, k10 + p);
            if (R >= high) p--;
            else if (R < low) p++;
            else return (R, p);
        }
    }

    // The number in [(A - E) / 2^b, (A + E) / 2^b] times 10^k10, rounded to n significant digits, if all of it rounds
    // the same; else null
    public static Num? RoundSig(BigInteger A, BigInteger E, int b, int k10, int n) {
        var neg = A.Sign < 0;
        var a = BigInteger.Abs(A);
        if (a - E <= 0) return null;
        var (r1, p1) = Sig(a - E, b, k10, n);
        var (r2, p2) = Sig(a + E, b, k10, n);
        if (r1 != r2 || p1 != p2) return null;
        if (neg) r1 = -r1;
        return p1 >= 0 ? new Fix(r1, p1) : new Fix(r1 * BigInteger.Pow(10, -p1), 0);
    }

    // An approximation: A / 2^B within E / 2^B, times 10^K
    public record struct Approx(BigInteger A, BigInteger E, int B, int K);

    // f at more and more bits till its value rounds surely to n digits
    static Num Ziv(Func<int, Approx> f, int n) {
        var need = (int)(n * 3.33) + 16;
        var b = need + 8;
        for (;;) {
            var x = f(b);
            var r = RoundSig(x.A, x.E, x.B, x.K, n);
            if (r != null) return r;
            var have = BitLen(x.A) - BitLen(x.E);       // (its bits known: too few, cancelled, or a value near a
            b += Math.Max(b / 2, need - have + 8);       //   rounding's edge)
        }
    }

    // ---- The functions' cores, at b bits

    // exp z, z within Ez / 2^bz of Z / 2^bz (bz at least b + 48): 10^k exp r, r = z - k log 10
    static Approx ExpCore(BigInteger Z, BigInteger Ez, int bz, int b, string name) {
        if (BigInteger.Abs(Z) > ((BigInteger)ExpMost << bz)) throw new MathError($"'{name}': out of range");
        var w = b + 40;
        var L = Ln10Fixed(bz);
        var k = FloorDiv(Z, L);
        var kb = BitLen(k) + 4;
        var R = ((Z << kb) - k * Ln10Fixed(bz + kb)) >> (kb + bz - w);   // (r at w bits: within 2 ulps, and Ez's)
        const int s = 12;                                  // (exp r = exp(r / 2^12)^4096)
        var t = R >> s;
        BigInteger one = BigInteger.One << w, sum = one, term = one;
        var n = 0;
        for (var i = 1; !term.IsZero; i++) {
            term = ((term * t) >> w) / i;
            sum += term;
            n++;
        }
        for (var i = 0; i < s; i++) sum = (sum * sum) >> w;
        // (Its error: the sum's, 3 a term and 3, doubled at each squaring, and less than 10 times the value's own;
        // and z's, a relative error of Ez / 2^bz)
        var Ew = (BigInteger)(10 * (3 * n + 6)) << (s + 1);
        Ew += ((sum * Ez) >> (bz - 4)) + 1;
        return new Approx(sum >> (w - b), (Ew >> (w - b)) + 2, b, (int)k);
    }

    // log x (x above 0, not 1), at b bits: t log 2 + 2 atanh((f - 1) / (f + 1)), x = 2^t f, f from 3/4 to 3/2
    static Approx LogCore(Rat x, int b) {
        var w = b + 24;
        var t = BitLen(x.num) - BitLen(x.den);
        var f = Scale2(x, -t);
        if (f.CompareTo(new Rat(3, 4)) < 0) { t--; f = Scale2(x, -t); }
        else if (f.CompareTo(new Rat(3, 2)) >= 0) { t++; f = Scale2(x, -t); }
        var zneg = f.num < f.den;                           // (z = (f - 1) / (f + 1), its size: atanh is odd)
        var Z = Fixed(new Rat(BigInteger.Abs(f.num - f.den), f.num + f.den), w);
        var Z2 = (Z * Z) >> w;
        BigInteger sum = BigInteger.Zero, term = Z;
        var n = 0;
        for (var k = 0; !term.IsZero; k++) {
            sum += term / (2 * k + 1);
            term = (term * Z2) >> w;
            n++;
        }
        sum *= zneg ? -2 : 2;
        var tb = BitLen(t) + 2;
        sum += (t * Ln2Fixed(w + tb)) >> tb;
        var Ew = (BigInteger)(4 * n + 8);
        return new Approx(sum >> (w - b), (Ew >> (w - b)) + 2, b, 0);
    }

    static Rat Scale2(Rat x, int t) => t >= 0 ? new Rat(x.num << t, x.den) : new Rat(x.num, x.den << -t);   // (x 2^t)

    // sin, cos or tan (which: 0, 1, 2) of x (not 0), at b bits: r = x - k pi/2, from -pi/4 to pi/4 near enough
    static Approx TrigCore(Rat x, int which, int b) {
        var w = b + 16;
        var xb = Math.Max(BitLen(x.num) - BitLen(x.den), 0);
        var p0 = PiFixed(xb + 64);
        var k = FloorDiv(2 * Fixed(x, xb + 64) + (p0 >> 1), p0);    // (x over pi/2, rounded)
        var kb = BitLen(k) + 4;
        var R = (Fixed(x, w + kb) - ((k * PiFixed(w + kb)) >> 1)) >> kb;
        var R2 = (R * R) >> w;
        BigInteger s = R, c = BigInteger.One << w, ts = R, tc = c;
        var n = 0;
        for (var i = 1; !ts.IsZero || !tc.IsZero; i++) {
            ts = -((ts * R2) >> w) / ((2 * i) * (2 * i + 1));
            tc = -((tc * R2) >> w) / ((2 * i - 1) * (2 * i));
            s += ts;
            c += tc;
            n++;
        }
        var Es = (BigInteger)(3 * n + 6);
        switch ((int)(((k % 4) + 4) % 4)) {
            case 1: (s, c) = (c, -s); break;
            case 2: (s, c) = (-s, -c); break;
            case 3: (s, c) = (-c, s); break;
        }
        if (which == 0) return new Approx(s >> (w - b), (Es >> (w - b)) + 2, b, 0);
        if (which == 1) return new Approx(c >> (w - b), (Es >> (w - b)) + 2, b, 0);
        var ca = BigInteger.Abs(c);
        if (ca <= Es) return new Approx(BigInteger.Zero, BigInteger.One << b, b, 0);   // (cos too near 0: more bits)
        var T = (s << w) / c;
        var Et = (((2 * Es + ((BigInteger.Abs(T) * 2 * Es) >> w)) << w) / (ca - Es)) + 2;
        return new Approx(T >> (w - b), (Et >> (w - b)) + 2, b, 0);
    }

    // atan x (x not 0), at b bits: three halvings (y / (1 + sqrt(1 + y^2))), then the series; past 1, pi/2 - atan 1/x
    static Approx AtanCore(Rat x, int b) {
        var w = b + 20;
        var neg = x.num.Sign < 0;
        var y = neg ? new Rat(-x.num, x.den) : x;
        var inv = y.num > y.den;
        if (inv) y = new Rat(y.den, y.num);
        var one = BigInteger.One << w;
        var Y = Fixed(y, w);
        for (var i = 0; i < 3; i++) Y = (Y << w) / (one + Isqrt((one << w) + Y * Y));
        var Y2 = (Y * Y) >> w;
        BigInteger sum = BigInteger.Zero, term = Y;
        var n = 0;
        for (var k = 0; !term.IsZero; k++) {
            sum += (k & 1) == 0 ? term / (2 * k + 1) : -(term / (2 * k + 1));
            term = (term * Y2) >> w;
            n++;
        }
        sum <<= 3;
        if (inv) sum = (PiFixed(w) >> 1) - sum;
        if (neg) sum = -sum;
        var Ew = (BigInteger)(8 * (2 * n + 8) + 4);
        return new Approx(sum >> (w - b), (Ew >> (w - b)) + 2, b, 0);
    }

    // ---- Complex arguments (hydra-2's sim/tools/numref.js csqrt ... cpow, the math library's mtcplx.inc): the
    // functions as mathematics defines them, their principal values (a number's argument above -pi and to pi; atan
    // odd), each part correctly rounded as a real result is.  A part is worked as an approximation (an Approx, its
    // error bounded) again with more bits till it rounds surely (ZivC); a part that's exactly 0 is known from the
    // argument (a modulus of 1, an angle of whole quarter turns, a power with a 0 part), never worked: Ziv's way can't
    // decide a 0 (and gives 0, past 1,024 bits, for one too small to tell from it).

    static Approx AOne(int b) => new Approx(BigInteger.One << b, BigInteger.Zero, b, 0);
    static Approx APi(int b) => new Approx(PiFixed(b), 2, b, 0);
    static Approx AZero(int b) => new Approx(BigInteger.Zero, BigInteger.Zero, b, 0);
    static Approx AK(Approx X, int K) {                   // (X as a multiple of 10^K)
        if (X.K == K) return X;
        var d = X.K - K;
        if (d > 0) { var p = BigInteger.Pow(10, d); return new Approx(X.A * p, X.E * p, X.B, K); }
        var q = BigInteger.Pow(10, -d);
        return new Approx(X.A / q, X.E / q + 1, X.B, K);
    }
    static Approx AAdd(Approx X, Approx Y) {
        var K = Math.Max(X.K, Y.K);
        X = AK(X, K); Y = AK(Y, K);
        return new Approx(X.A + Y.A, X.E + Y.E, X.B, K);
    }
    static Approx ANeg(Approx X) => X with { A = -X.A };
    static Approx ASub(Approx X, Approx Y) => AAdd(X, ANeg(Y));
    static Approx AShr(Approx X, int s) => new Approx(X.A >> s, (X.E >> s) + 1, X.B, X.K);
    static Approx AMul(Approx X, Approx Y) =>
        new Approx((X.A * Y.A) >> X.B, ((BigInteger.Abs(X.A) * Y.E + BigInteger.Abs(Y.A) * X.E + X.E * Y.E) >> X.B) + 2, X.B, X.K + Y.K);
    static Approx AMulQ(Approx X, Rat f) =>              // (X times an exact fraction)
        new Approx((X.A * f.num) / f.den, (X.E * BigInteger.Abs(f.num) + f.den - 1) / f.den + 1, X.B, X.K);
    static Approx ADiv(Approx X, Approx Y) {
        var ya = BigInteger.Abs(Y.A);
        var K = X.K - Y.K;
        if (ya <= Y.E) return new Approx(BigInteger.Zero, BigInteger.One << (2 * X.B), X.B, K);   // (Y not known from 0)
        return new Approx((X.A << X.B) / Y.A, (((X.E * ya + BigInteger.Abs(X.A) * Y.E) << X.B) / ((ya - Y.E) * ya)) + 2, X.B, K);
    }
    static Approx ASqrt(Approx X) {                       // (X at or above 0)
        if ((X.K & 1) != 0) X = AK(X, X.K - 1);
        var lo = X.A - X.E;
        var K = X.K / 2;
        if (lo.Sign <= 0) return new Approx(BigInteger.Zero, Isqrt((X.A + X.E) << X.B) + 2, X.B, K);
        return new Approx(Isqrt(X.A << X.B), ((X.E << X.B) / (2 * Isqrt(lo << X.B))) + 2, X.B, K);
    }
    static Approx AToB(Approx X, int b) { var s = X.B - b; return new Approx(X.A >> s, (X.E >> s) + 1, b, X.K); }
    static Approx AExact(Rat v, int b) => new Approx(Fixed(v, b), 1, b, 0);
    static Approx ATrig(Approx Z, int which, int b) {    // (sin or cos of an approximation: 1 its slope at most)
        var T = Z.A.IsZero ? (which == 1 ? AOne(b) : AZero(b)) : TrigCore(new Rat(Z.A, BigInteger.One << Z.B), which, b);
        return new Approx(T.A, T.E + (Z.E >> (Z.B - b)) + 1, b, 0);
    }
    static Num ZivC(Func<int, Approx> f) {
        var n = Digits;
        var need = (int)(n * 3.33) + 16;
        var b = need + 8;
        for (;;) {
            var x = f(b);
            var r = RoundSig(x.A, x.E, x.B, x.K, n);
            if (r != null) return r;
            if (b > 1024 && BigInteger.Abs(x.A) <= x.E) return new Int(0);
            b += Math.Max(b / 2, need - (BitLen(x.A) - BitLen(x.E)) + 8);
        }
    }
    static int W(int b) => b + 24;                          // (A part's working bits)
    static Rat RNeg(Rat f) => new Rat(-f.num, f.den);
    static Rat RDiv(Rat f, Rat g) => g.num.Sign < 0 ? new Rat(-f.num * g.den, f.den * -g.num) : new Rat(f.num * g.den, f.den * g.num);
    static Num ModSq(Comp c) => (Num)c.r * c.r + (Num)c.im * c.im;   // (|x|^2, exactly)
    static Num HalfOf(Comp c) => c.r is Fix || c.im is Fix ? new Fix(5, 1) : Num.Norm(new Rat(1, 2));
    static Num MakeComp(Num re, Num im) => Num.Norm(new Comp((Int)re, (Int)im));

    // x's square root if it's rational, else null
    static Num? SqrtExact(Num x) {
        var v = Rat.ToRat(x);
        if (v.num.Sign < 0) return null;
        BigInteger ra = Isqrt(v.num), rb = Isqrt(v.den);
        return ra * ra == v.num && rb * rb == v.den ? Kind(x, new Rat(ra, rb)) : null;
    }

    // The argument (angle) of a + y i (not 0), at w bits
    static Approx AArg(Rat a, Rat y, int w) {
        if (a.num.IsZero) { var h = AShr(APi(w), 1); return y.num.Sign < 0 ? ANeg(h) : h; }
        var t = y.num.IsZero ? AZero(w) : AtanCore(RDiv(y, a), w);
        if (a.num.Sign > 0) return t;
        return y.num.Sign < 0 ? ASub(t, APi(w)) : AAdd(t, APi(w));
    }

    // |x|: a real number's, or a complex number's modulus (the square root of re^2 + im^2: exact when it's rational)
    public static Num Abs(Num x) => x is Comp c ? Sqrt(ModSq(c)) : (x.CompareTo(Num.Zero) < 0 ? -x : x);

    // The square root (its real part above 0, or 0 and its imaginary part above 0): exact when its parts are rational
    static Num CSqrt(Comp c) {
        var s = SqrtExactC(c);
        if (s != null) return s;
        Rat a = Rat.ToRat(c.r), y = Rat.ToRat(c.im), m2 = Rat.ToRat(ModSq(c));
        (Approx, Approx) Parts(int w) {                     // (the part not cancelling first: then y over twice it)
            var R = ASqrt(AExact(m2, w));
            var P = ASqrt(AShr(AAdd(R, AExact(a.num.Sign < 0 ? RNeg(a) : a, w)), 1));
            var Q = ADiv(AExact(y.num.Sign < 0 && a.num.Sign < 0 ? RNeg(y) : y, w), AMulQ(P, new Rat(2, 1)));
            if (a.num.Sign >= 0) return (P, Q);
            return (Q, y.num.Sign < 0 ? ANeg(P) : P);
        }
        return MakeComp(ZivC(b => AToB(Parts(W(b)).Item1, b)), ZivC(b => AToB(Parts(W(b)).Item2, b)));
    }

    // A complex number's square root if both its parts are rational, else null
    static Num? SqrtExactC(Comp c) {
        var r = SqrtExact(ModSq(c));
        if (r == null) return null;
        var h = HalfOf(c);
        var re = SqrtExact(((Num)r + (Num)c.r) * h);
        var im = SqrtExact(((Num)r - (Num)c.r) * h);
        if (re == null || im == null) return null;
        return MakeComp(re, c.im.CompareTo(Num.Zero) < 0 ? -im : im);
    }

    // e to the x: e^a (cos y + i sin y)
    static Num CExp(Comp c) {
        Rat a = Rat.ToRat(c.r), y = Rat.ToRat(c.im);
        Num Part(int k) => ZivC(b => {
            var w = W(b);
            var T = TrigCore(y, k == 0 ? 1 : 0, w);
            if (a.num.IsZero) return AToB(T, b);
            return AToB(AMul(ExpCore(Fixed(a, w + 48), BigInteger.One, w + 48, w, "exp"), T), b);
        });
        return MakeComp(Part(0), Part(1));
    }

    // The natural logarithm: log |x| + i arg x
    static Num CLog(Comp c) {
        Rat a = Rat.ToRat(c.r), y = Rat.ToRat(c.im), m2 = Rat.ToRat(ModSq(c));
        var re = m2.num == m2.den ? new Int(0) : ZivC(b => AToB(AShr(LogCore(m2, W(b)), 1), b));
        return MakeComp(re, ZivC(b => AToB(AArg(a, y, W(b)), b)));
    }

    // sin, cos or tan of a + y i: with cosh y and sinh y (from e^y and e^-y)
    static Num CTrig(Comp c, int which) {
        Rat a = Rat.ToRat(c.r), y = Rat.ToRat(c.im);
        string name = which == 0 ? "sin" : which == 1 ? "cos" : "tan";
        (Approx C, Approx S) Hyp(Rat v, int w) {
            var bz = w + 48;
            var P = ExpCore(Fixed(v, bz), BigInteger.One, bz, w, name);
            var M = ExpCore(Fixed(RNeg(v), bz), BigInteger.One, bz, w, name);
            return (AShr(AAdd(P, M), 1), AShr(ASub(P, M), 1));
        }
        Approx SinA(Rat v, int w) => TrigCore(v, 0, w);
        Approx CosA(Rat v, int w) => v.num.IsZero ? AOne(w) : TrigCore(v, 1, w);
        Func<int, Func<int, Approx>> f;
        int zero;                                           // (the part with sin a in it: 0 when a is)
        if (which == 2) {                                   // (tan: (sin 2a + i sinh 2y) / (cos 2a + cosh 2y))
            Rat a2 = new Rat(2 * a.num, a.den), y2 = new Rat(2 * y.num, y.den);
            f = k => b => { var w = W(b); var h = Hyp(y2, w); return AToB(ADiv(k == 1 ? h.S : SinA(a2, w), AAdd(CosA(a2, w), h.C)), b); };
            zero = 0;
        } else if (which == 0) {                            // (sin a cosh y + i cos a sinh y)
            f = k => b => { var w = W(b); var h = Hyp(y, w); return AToB(k == 1 ? AMul(CosA(a, w), h.S) : AMul(SinA(a, w), h.C), b); };
            zero = 0;
        } else {                                            // (cos a cosh y - i sin a sinh y)
            f = k => b => { var w = W(b); var h = Hyp(y, w); return AToB(k == 1 ? ANeg(AMul(SinA(a, w), h.S)) : AMul(CosA(a, w), h.C), b); };
            zero = 1;
        }
        Num Part(int k) => a.num.IsZero && k == zero ? new Int(0) : ZivC(f(k));
        return MakeComp(Part(0), Part(1));
    }

    // The arc tangent: (1/2) arg(1 - a^2 - y^2 + 2a i) + (i/4) log((a^2 + (y + 1)^2) / (a^2 + (y - 1)^2)); on the
    // imaginary axis past i and -i, a quarter turn (atan is odd); of i and -i, an error
    static Num CAtan(Comp c) {
        Rat a = Rat.ToRat(c.r), y = Rat.ToRat(c.im);
        if (a.num.IsZero && BigInteger.Abs(y.num) == y.den) throw new MathError("'atan': out of range");
        var aa = (Num)c.r * c.r;
        Num yp = (Num)c.im + 1, ym = (Num)c.im - 1;
        Rat N = Rat.ToRat(aa + yp * yp), D = Rat.ToRat(aa + ym * ym);
        var im = ZivC(b => AToB(AShr(LogCore(RDiv(N, D), W(b)), 2), b));
        Num re;
        if (a.num.IsZero) re = BigInteger.Abs(y.num) < y.den ? new Int(0) : ZivC(b => { var h = AShr(APi(W(b)), 1); return AToB(y.num.Sign < 0 ? ANeg(h) : h, b); });
        else {
            Rat den = Rat.ToRat((Num)new Int(1) - aa - (Num)c.im * c.im), num = new Rat(2 * a.num, a.den);
            re = ZivC(b => AToB(AShr(AArg(den, num, W(b)), 1), b));
        }
        return MakeComp(re, im);
    }

    // x's square root if it's exact (x complex, or a negative real number), else null
    static Num? CSqrtExact(Num x) {
        if (x is Comp c) return SqrtExactC(c);
        var r = SqrtExact(-x);
        return r == null ? null : MakeComp(new Int(0), r);
    }

    // x to the power y, the principal value: exp(y log x).  y = n/2 exact when x's square root is; an angle that's a
    // whole number of quarter turns exact, the parts with it
    static Num CPow(Num x, Num y) {
        var cy = Comp.Of(y);
        Rat wr = Rat.ToRat(cy.r), wi = Rat.ToRat(cy.im);
        if (x.IsZero) {
            if (wr.num.Sign > 0) return new Int(0);
            if (y is Comp) throw new MathError("'pow': out of range");
            throw new DivideByZeroException("Division by zero.");
        }
        if (y is not Comp && wr.den == 2) { var s = CSqrtExact(x); if (s != null) return WholePow(s, wr.num); }
        var c = Comp.Of(x);
        Rat a = Rat.ToRat(c.r), v = Rat.ToRat(c.im), m2 = Rat.ToRat(ModSq(c));
        var lre0 = m2.num == m2.den;                        // (log |x| is 0)
        Rat? q = null;                                      // (arg x, when it's q pi)
        if (v.num.IsZero) q = a.num.Sign > 0 ? new Rat(0, 1) : new Rat(1, 1);
        else if (a.num.IsZero) q = new Rat(v.num.Sign > 0 ? 1 : -1, 2);
        else if (a.num * v.den == v.num * a.den || a.num * v.den == -v.num * a.den)
            q = new Rat((a.num.Sign > 0 ? 1 : 3) * (v.num.Sign > 0 ? 1 : -1), 4);
        var arg0 = q != null && q.num.IsZero;
        bool t1 = !wr.num.IsZero && !lre0, t2 = !wi.num.IsZero && !arg0;   // (Tre = wr log|x| - wi arg x)
        bool t3 = !wr.num.IsZero && !arg0, t4 = !wi.num.IsZero && !lre0;   // (Tim = wr arg x + wi log|x|)
        Rat? p = null;                                      // (Tim = p pi, when it's that)
        if (!t4) { if (!t3) p = new Rat(0, 1); else if (q != null) p = new Rat(wr.num * q.num, wr.den * q.den); }
        int Bits(Rat f) => Math.Max(BitLen(f.num) - BitLen(f.den), 0);
        int BzOf(int w) => w + 48 + Bits(wr) + Bits(wi);
        Approx Lre(int bz) => AShr(LogCore(m2, bz), 1);
        Approx Lim(int bz) => q != null ? AMulQ(APi(bz), q) : AArg(a, v, bz);
        Approx Tre(int bz) {
            var T = AZero(bz);
            if (t1) T = AAdd(T, AMulQ(Lre(bz), wr));
            if (t2) T = ASub(T, AMulQ(Lim(bz), wi));
            return T;
        }
        Approx Tim(int bz) {
            if (p != null) return AMulQ(APi(bz), p);
            var T = AZero(bz);
            if (t3) T = AAdd(T, AMulQ(Lim(bz), wr));
            if (t4) T = AAdd(T, AMulQ(Lre(bz), wi));
            return T;
        }
        var mag0 = !t1 && !t2;                              // (|x^y| is 1)
        Approx Mag(int w) { if (mag0) return AOne(w); var bz = BzOf(w); var Z = Tre(bz); return ExpCore(Z.A, Z.E, bz, w, "pow"); }
        int cs = -1;                                        // (cos and sin of Tim, when they're 0 and 1s: (2p) mod 4)
        if (p != null) {
            var twice = new Rat(2 * p.num, p.den);
            if ((twice.num % twice.den).IsZero) cs = (int)(((twice.num / twice.den) % 4 + 4) % 4);
        }
        Num Part(int k) {
            if (cs >= 0) {
                int[] cos = { 1, 0, -1, 0 }, sin = { 0, 1, 0, -1 };
                var one = k == 0 ? cos[cs] : sin[cs];
                if (one == 0) return new Int(0);
                if (mag0) return new Int(one);
                var m = ZivC(b => AToB(Mag(W(b)), b));
                return one < 0 ? -m : m;
            }
            return ZivC(b => { var w = W(b); return AToB(AMul(Mag(w), ATrig(Tim(BzOf(w)), k == 0 ? 1 : 0, w)), b); });
        }
        return MakeComp(Part(0), Part(1));
    }

    // ---- The functions

    static Rat Real(Num x, string name) {
        if (x is Comp) throw new MathError($"'{name}' expects a real number");
        return Rat.ToRat(x);
    }

    static Num Precision(Func<int, Approx> f) => Ziv(f, Digits);

    public static Num Pi() => Precision(b => new Approx(PiFixed(b), 2, b, 0));

    public static Num Sqrt(Num x) {
        if (x is Comp c) return CSqrt(c);
        var v = Real(x, "sqrt");
        if (v.num.Sign < 0) return Num.Norm(new Comp(new Int(0), (Int)Sqrt(Kind(x, new Rat(-v.num, v.den)))));
        if (v.num.IsZero) return x;
        BigInteger ra = Isqrt(v.num), rb = Isqrt(v.den);
        if (ra * ra == v.num && rb * rb == v.den) return Kind(x, new Rat(ra, rb));
        // round(sqrt(v) 10^p): sqrt(P Q) / Q, v 10^2p = P / Q
        var n = Digits;
        var e = Log10Floor(v);
        var p = n - 1 - (e >= 0 ? e / 2 : -((1 - e) / 2));   // (floor(e / 2))
        for (;;) {
            BigInteger P = v.num, Q = v.den;
            if (p >= 0) P *= BigInteger.Pow(10, 2 * p);
            else Q *= BigInteger.Pow(10, -2 * p);
            var R = (Isqrt(4 * P * Q) + Q) / (2 * Q);
            if (R >= BigInteger.Pow(10, n)) { p--; continue; }
            if (R < BigInteger.Pow(10, n - 1)) { p++; continue; }
            return p >= 0 ? new Fix(R, p) : new Fix(R * BigInteger.Pow(10, -p), 0);
        }
    }

    // floor(log10 v), v above 0
    static int Log10Floor(Rat v) {
        var e = (int)Math.Floor((BitLen(v.num) - BitLen(v.den)) * 0.30102999566398120);
        for (;;) {
            if (Cmp10(v, e) < 0) { e--; continue; }
            if (Cmp10(v, e + 1) >= 0) { e++; continue; }
            return e;
        }
    }

    // v against 10^e
    static int Cmp10(Rat v, int e) => e >= 0 ? v.num.CompareTo(v.den * BigInteger.Pow(10, e)) : (v.num * BigInteger.Pow(10, -e)).CompareTo(v.den);

    // v (exact, 0 or more) as x's kind: an integer's an integer, a fixed decimal's a fixed decimal, a rational's a
    // rational (each the simplest that holds it)
    static Num Kind(Num x, Rat v) {
        if (x is Fix) {
            var d = v.den;
            int places = 0;
            while (!(BigInteger.Remainder(BigInteger.Pow(10, places), d)).IsZero) places++;
            return new Fix(v.num * (BigInteger.Pow(10, places) / d), places);
        }
        return Num.Norm(v);
    }

    public static Num Exp(Num x) {
        if (x is Comp c) return CExp(c);
        var v = Real(x, "exp");
        if (v.num.IsZero) return new Int(1);
        return Precision(b => {
            var bz = b + 48 + BitLen(v.num) - BitLen(v.den);
            if (bz < b + 48) bz = b + 48;
            return ExpCore(Fixed(v, bz), BigInteger.One, bz, b, "exp");
        });
    }

    public static Num Log(Num x) {
        if (x is Comp c) return CLog(c);
        var v = Real(x, "log");
        if (v.num.IsZero) throw new MathError("'log': the logarithm of 0");
        if (v.num.Sign < 0) {
            var a = new Rat(-v.num, v.den);
            var re = a.num == a.den ? new Int(0) : Precision(b => LogCore(a, b));
            return Num.Norm(new Comp((Int)re, (Int)Pi()));
        }
        if (v.num == v.den) return new Int(0);
        return Precision(b => LogCore(v, b));
    }

    public static Num Trig(Num x, int which) {
        if (x is Comp c) return CTrig(c, which);
        var v = Real(x, which == 0 ? "sin" : which == 1 ? "cos" : "tan");
        if (v.num.IsZero) return new Int(which == 1 ? 1 : 0);
        return Precision(b => TrigCore(v, which, b));
    }

    public static Num Atan(Num x) {
        if (x is Comp c) return CAtan(c);
        var v = Real(x, "atan");
        if (v.num.IsZero) return new Int(0);
        return Precision(b => AtanCore(v, b));
    }

    // x to the power y: a whole y's power exact (as the tower multiplies and divides: any x); else x real, 0 or more:
    // exact when the root is rational (x's kind), else exp (y log x) at the precision; else (x below 0, or x or y
    // complex) the principal value, CPow's
    public static Num Pow(Num x, Num y) {
        if (y is Comp) return CPow(x, y);
        var yr = Rat.ToRat(y);
        if (yr.den.IsOne) return WholePow(x, yr.num);
        if (x is Comp) return CPow(x, y);
        var v = Rat.ToRat(x);
        if (v.num.Sign < 0) return CPow(x, y);
        if (v.num.IsZero) {
            if (yr.num.Sign < 0) throw new DivideByZeroException("Division by zero.");
            return new Int(0);
        }
        if (PerfectRoot(v.num, yr.den, out var ra) && PerfectRoot(v.den, yr.den, out var rb))
            return WholePow(Kind(x, new Rat(ra, rb)), yr.num);
        return Precision(b => {
            var yb = Math.Max(BitLen(yr.num) - BitLen(yr.den), 0) + 1;
            var bl = b + 52 + yb;
            var L = LogCore(v, bl);                           // (log x within L.E ulps)
            var Z = (L.A * yr.num) / yr.den;                  // (y log x: within |y| L.E and 1)
            var Ez = ((BigInteger.Abs(L.E * yr.num) + yr.den - 1) / yr.den) + 1;
            return ExpCore(Z, Ez, bl, b, "pow");
        });
    }

    // Whether a (above 0) is a perfect qth power, r its root
    static bool PerfectRoot(BigInteger a, BigInteger q, out BigInteger r) {
        r = Iroot(a, q);
        if (r.IsOne) return a.IsOne;
        return BigInteger.Pow(r, (int)q) == a;            // (q under a's bits: Iroot's 1 past them)
    }

    // x to the whole power n, by the tower's products (and 1 / x^-n below 0), as danlang's pow always was
    public static Num WholePow(Num x, BigInteger n) {
        if (n.Sign < 0) return new Int(1) / WholePow(x, -n);
        Num r = new Int(1), b = x;
        var first = true;
        while (!n.IsZero) {
            if (!n.IsEven) { r = first ? b : r * b; first = false; }
            n >>= 1;
            if (!n.IsZero) b = b * b;
        }
        return r;
    }
}
