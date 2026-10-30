using System.Numerics;

// Natives.cs - lib/globals.dl's most used functions, done natively too: each is still the function its danlang code
// makes (printed, compared, partially applied, redefined as any function is), with a native way beside it, used only
// when it's sure to give exactly what the code gives: the value (the same kinds, made by the same C#), the integers it
// gives a Num to (LVal.NumVal makes one, kept in the value: after that Op1 and Op2 don't do it in the call), the names
// it binds (Name.Local from then on), and the Site the code leaves (where an error made before another call starts
// says it was).  That Site isn't worked out but seen: the code runs, and the Site it leaves is kept for the way it
// went, while no plan changes (PlanEpoch)
public partial class LVal {
    // A native way's value for the arguments, worked out with no value changed (an integer it gives a Num to is kept
    // for Builtins.Commit), and which way the code goes for them (a number the native's ways have); null: not a value
    // it's sure of (the code runs).  room: the calls it may nest beyond those its Native says; self: its Native, whose
    // Kept says whether a way's value can be used (if not, only the way is wanted: any value but null will do, for
    // the same arguments; a run of the code that's an error isn't kept for the way)
    public delegate LVal? NativeWay(List<LVal> args, int room, Native self, out int way);

    // A library function's native way, on its FunInfo (a copy of the function, a partial application's or a method's,
    // has none: it's used only for the function as it was defined): for the numbers of arguments it takes, while each
    // global its code uses (and the code of the library functions it calls) has the value it had, with room for the
    // calls the code nests (Nest, and a margin); not, from then on, if its ways turn out wrong (two Sites for one)
    public sealed class Native {
        public readonly int MinArgs, MaxArgs, Nest;
        private readonly LEnv.Slot[] _slots;
        private readonly LVal[] _values;
        public NativeWay? Way;
        public Func<LVal, List<LVal>, LVal?>? Code;     // (cond, case: the clauses' code run as their code runs it)
        // For each way: the Site its code left the last time it went that way settled (PlanEpoch the same at its end
        // as at its start: no plan changed on the way), and PlanEpoch then
        private readonly CodeInfo?[] _sites;
        private readonly long[] _epochs;
        private bool _off;
        private const int Margin = 100;
        // The plans at its last call, those it last learned a way for (its value not usable: its code run, the Site it
        // leaves to be kept), those its value was last used with, and those of the last way it learned for nothing
        // (the plans changed before it was used); and whether it learns a way at once, or only with the plans as they
        // were at its last call (not while they change at every call): at once after a value of it's used, and the
        // other of the two after each way learned for nothing
        private long _last = -1, _learned = -1, _used = -1, _wasted = -1;
        private bool _eager = true;

        public Native(int min, int max, int nest, int ways, LEnv.Slot[] slots) {
            (MinArgs, MaxArgs, Nest) = (min, max, nest);
            _slots = slots;
            _values = slots.Select(s => s.Value).ToArray();
            _sites = new CodeInfo?[ways];
            _epochs = Enumerable.Repeat(-1L, ways).ToArray();
        }

        // Whether each global its code uses has the value it had
        public bool Holds() {
            for (int i = 0; i < _slots.Length; i++) if (!ReferenceEquals(_slots[i].Value, _values[i])) return false;
            return true;
        }

        // Its value for the arguments (room: the calls that may nest, from here), as its code's would be, with Site
        // its code's; null: the code runs (way: the way it goes, if a Site is to be kept for it; wanted: whether Apply
        // would keep one: if not, it works only for a value sure to be used, a Site kept for every way).  A way learned
        // costs its work as well as its code's, for nothing if the plans change before it's used, as when they change
        // at every call (_eager)
        public LVal? Run(LVal f, List<LVal> args, int room, bool wanted, out int way) {
            way = -1;
            if (_off || args.Count < MinArgs || args.Count > MaxArgs || !Holds()) return null;
            if (Code != null) return Code(f, args);
            room -= Nest + Margin;
            if (room <= 0) return null;
            var epoch = PlanEpoch;
            var steady = _last == epoch;
            _last = epoch;
            int kept = 0;
            foreach (var e in _epochs) if (e == epoch) kept++;
            if (kept < _epochs.Length && !wanted) return null;     // (no way learned here)
            if (kept == 0) {            // (only a way to learn)
                if (_learned != epoch && _learned != _used && _learned != _wasted) (_wasted, _eager) = (_learned, !_eager);
                if (!_eager && !steady) return null;
            }
            Builtins.Begin();
            LVal? v;
            int w;
            try {
                v = Way!(args, room, this, out w);
            }
            catch (Exception) {         // (one it didn't foresee: nothing's changed, the code runs)
                return null;
            }
            if (v == null) return null;
            if (!Kept(w)) {
                (way, _learned) = (w, epoch);
                return null;
            }
            (_used, _eager) = (epoch, true);
            Builtins.Commit();
            foreach (var x in args) x.Freeze();
            Site = _sites[w];
            return v;
        }

        // Whether a Site is kept for way w for these plans (its value can be used)
        public bool Kept(int w) => _epochs[w] == PlanEpoch;

        // Its code went way w settled, from epoch on, leaving site: kept for that way
        public void Settled(int w, long epoch, CodeInfo? site) {
            if (_epochs[w] == epoch && !ReferenceEquals(_sites[w], site)) _off = true;
            _sites[w] = site;
            _epochs[w] = epoch;
        }
    }

    internal void SetNative(Native n) => F.Native = n;
    internal CodeInfo? Info => _x as CodeInfo;      // (a list's compiled code, if it has one)
}

public partial class Builtins
{
    // ---- What the natives' ways give a Num to, and the names they bind

    // The integers kept in their values that a native's way, as it works its value out, gives a Num (as LVal.NumVal
    // does, so they're not ones Op1 and Op2 do in the call), and the names its code binds for these arguments but not
    // for all of the way's (a run that went the way needn't have made them Local): hd, tl, and &1 ... &n (_extrasBound
    // of them); given them, and made Local, when its value is used
    private static readonly List<LVal> _uses = new();
    private static readonly List<Name> _binds = new();
    private static int _extrasBound;
    internal static void Begin() {
        foreach (var v in _uses) v.Used = false;
        _uses.Clear();
        _binds.Clear();
        _extrasBound = 0;
    }
    internal static void Commit() {
        foreach (var v in _uses) _ = v.NumVal;
        foreach (var n in _binds) n.MakeLocal();
        Extras(_extrasBound);
        Begin();
    }
    private static void Binds(Name a, Name b) {
        _binds.Add(a);
        _binds.Add(b);
    }
    private static void BindsExtras(int n) => _extrasBound = Math.Max(_extrasBound, n);

    // Names the library's code binds: remove-all's and remove-fst's hd and tl (for a list that isn't empty), and
    // unpack's formals (remove-all's too)
    private static readonly Name Hd = Name.Of("hd"), Tl = Name.Of("tl"), F = Name.Of("f"), L = Name.Of("l");

    // v's Num, as NumVal gives it (an integer kept in the value given one when the value's used)
    private static Num Use(LVal v) {
        if (!v.IsLong(out var n)) return v.NumVal!;
        Mark(v);
        return new Int(n);
    }
    // (An integer is in _uses once, however often it's compared: its Used says so, not a hash of it, as an object's
    // own hash, once it's given one, changes those given after it, which streams are ordered by)
    private static void Mark(LVal v) {
        if (!v.IsLong(out _) || v.Used) return;
        v.Used = true;
        _uses.Add(v);
    }

    // Whether v is an integer kept in its value now, as Op1 and Op2 see it (one the way has given a Num isn't)
    private static bool IsLongNow(LVal v, out long n) => v.IsLong(out n) && !v.Used;

    // ---- The built-ins the library's code calls, as they do it for values (null: what is their error)

    private static readonly LVal Zero = LVal.Number(0), One = LVal.Number(1), Two = LVal.Number(2);
    private static readonly LVal Half = LVal.Number(new Rat(1, 2));

    // (- x y), (+ x y), (* x y): the fast ways' (integers), or the built-in's (any numbers, their Nums had)
    private static LVal? Sub(LVal x, LVal y) {
        if (x.IsSmallInt(out var a) && y.IsSmallInt(out var b)) {
            var r = unchecked(a - b);
            if (((a ^ b) & (a ^ r)) >= 0) return LVal.Number(r);
        }
        if (x.IsPlainInt(out var p) && y.IsPlainInt(out var q)) return LVal.Number(p - q);
        if (!x.IsNum || !y.IsNum) return null;
        var n = Use(x);
        return LVal.Number(n - Use(y));
    }
    private static LVal? Add(LVal x, LVal y) {
        if (x.IsSmallInt(out var a) && y.IsSmallInt(out var b)) {
            var r = unchecked(a + b);
            if (((a ^ r) & (b ^ r)) >= 0) return LVal.Number(r);
        }
        if (x.IsPlainInt(out var p) && y.IsPlainInt(out var q)) return LVal.Number(p + q);
        if (!x.IsNum || !y.IsNum) return null;      // (a string or a character first: joined, not added)
        var n = Use(x);
        return LVal.Number(n + Use(y));
    }
    private static LVal? Mul(LVal x, LVal y) {
        if (x.IsSmallInt(out var a) && y.IsSmallInt(out var b)) {
            var hi = Math.BigMul(a, b, out var lo);
            if (hi == (lo >> 63)) return LVal.Number(lo);
        }
        if (x.IsPlainInt(out var p) && y.IsPlainInt(out var q)) return LVal.Number(p * q);
        if (!x.IsNum || !y.IsNum) return null;
        var n = Use(x);
        return LVal.Number(n * Use(y));
    }

    // (/ x y): the built-in's (by zero, its error)
    private static LVal? Div(LVal x, LVal y) {
        if (!x.IsNum || !y.IsNum) return null;
        if (x.IsSmallInt(out var a) && y.IsSmallInt(out var b) && b != 0 && b != -1 && a % b == 0) {
            Mark(x);
            Mark(y);
            return LVal.Number(a / b);
        }
        var n = Use(x);
        var d = Use(y);
        if (d.IsZero) return null;
        return LVal.Number(n / d);
    }

    // (- x): its negation, the fast way's or the built-in's
    private static LVal? Neg(LVal x) {
        if (x.IsSmallInt(out var a) && a != long.MinValue) return LVal.Number(-a);
        if (x.IsPlainInt(out var p)) return LVal.Number(-p);
        if (!x.IsNum) return null;
        return LVal.Number(-Use(x));
    }

    // (1- x), (1+ x): the fast ways', or the built-ins' (through - and +, with the shared 1)
    private static LVal? OneLess(LVal x) {
        if (x.IsSmallInt(out var a) && a != long.MinValue) return LVal.Number(a - 1);
        if (x.IsPlainInt(out var p)) return LVal.Number(p - 1);
        if (!x.IsNum) return null;
        var n = Use(x);
        return LVal.Number(n - Use(One));
    }
    private static LVal? OneMore(LVal x) {
        if (x.IsSmallInt(out var a) && a != long.MaxValue) return LVal.Number(a + 1);
        if (x.IsPlainInt(out var p)) return LVal.Number(p + 1);
        if (!x.IsNum) return null;                  // (a string or a character: joined)
        var n = Use(x);
        return LVal.Number(n + Use(One));
    }

    // (truncate x): a real number's integer part
    private static LVal? Truncated(LVal x) {
        if (x.IsSmallInt(out var a)) {
            Mark(x);
            return LVal.Number(a);
        }
        if (!x.IsNum) return null;
        var n = Use(x);
        return n is Comp ? null : LVal.Number(n.ToInt());
    }

    // Two numbers compared, as LVal.CompareTo does it (their Nums had): -1, 0 or 1
    private static int CompareNums(LVal x, LVal y) {
        if (x.IsSmallInt(out var a) && y.IsSmallInt(out var b)) {
            Mark(x);
            Mark(y);
            return a.CompareTo(b);
        }
        var n = Use(x);
        return Math.Sign(n.CompareTo(Use(y)));
    }

    // How deep into lists Order and Same go (their frames are bigger than LVal.Equals' and CompareTo's: a list nested
    // deeper is the code's to compare, so the host's stack runs out where it always has)
    private const int Deepest = 10000;

    // x against y, as LVal.CompareTo orders them (null: values it compares by what they print, functions, hashes ...,
    // and lists nested too deep)
    private static int? Order(LVal x, LVal y, int depth = 0) {
        static int Rank(LVal v) => v.ValType switch {
            LVal.LE.NUM => 0, LVal.LE.CHAR => 1, LVal.LE.STR => 2, LVal.LE.ATOM => 3, LVal.LE.SYM => 4, LVal.LE.QEXPR => 5,
            LVal.LE.SEXPR => 6, LVal.LE.T => 7, LVal.LE.FUN => 8, LVal.LE.HASH => 9, LVal.LE.BUFFER => 10,
            LVal.LE.STREAM => 11, LVal.LE.ERR => 12, _ => 13
        };
        var kind = Rank(x).CompareTo(Rank(y));
        if (kind != 0) return Math.Sign(kind);
        switch (x.ValType) {
            case LVal.LE.NUM: return CompareNums(x, y);
            case LVal.LE.CHAR:
            case LVal.LE.STR: return Math.Sign(string.CompareOrdinal(x.StrVal, y.StrVal));
            case LVal.LE.ATOM:
            case LVal.LE.SYM: return Math.Sign(string.CompareOrdinal(x.SymVal, y.SymVal));
            case LVal.LE.ERR: return Math.Sign(string.CompareOrdinal(x.ErrVal, y.ErrVal));
            case LVal.LE.T: return 0;
            case LVal.LE.BUFFER: return Math.Sign(x.BufferValue.AsSpan().SequenceCompareTo(y.BufferValue));
            case LVal.LE.QEXPR:
            case LVal.LE.SEXPR:
                if (depth == Deepest) return null;
                for (int i = 0; i < x.Count && i < y.Count; ++i) {
                    var c = Order(x.Cells![i], y.Cells![i], depth + 1);
                    if (c != 0) return c;
                }
                return Math.Sign(x.Count.CompareTo(y.Count));
        }
        return null;
    }

    // x and y equal, as LVal.Equals has it, going as far into them as it does (null: functions, hashes, and lists
    // nested too deep)
    private static bool? Same(LVal x, LVal y, int depth = 0) {
        if (x.ValType != y.ValType) return false;
        switch (x.ValType) {
            case LVal.LE.T: return true;
            case LVal.LE.NUM: return CompareNums(x, y) == 0;
            case LVal.LE.ERR: return x.ErrVal == y.ErrVal;
            case LVal.LE.ATOM:
            case LVal.LE.SYM: return x.SymVal == y.SymVal;
            case LVal.LE.CHAR:
            case LVal.LE.STR: return x.StrVal == y.StrVal;
            case LVal.LE.BUFFER: return x.BufferValue.AsSpan().SequenceEqual(y.BufferValue);
            case LVal.LE.QEXPR:
            case LVal.LE.SEXPR:
                if (x.Count != y.Count) return false;
                if (depth == Deepest) return null;
                for (int i = 0; i < x.Count; i++) {
                    var s = Same(x.Cells![i], y.Cells![i], depth + 1);
                    if (s != true) return s;
                }
                return true;
            case LVal.LE.FUN:
            case LVal.LE.HASH: return null;
        }
        return false;
    }

    // As eq and == have it, and <, >, <= and >= (their fast ways): integers that fit a long as they are
    private static bool? Eq(LVal x, LVal y) => x.IsSmallInt(out var a) && y.IsSmallInt(out var b) ? a == b : Same(x, y);
    private static int? Compared(LVal x, LVal y) => x.IsSmallInt(out var a) && y.IsSmallInt(out var b) ? a.CompareTo(b) : Order(x, y);

    // (zero? x), (one? x): their fast ways
    private static bool IsZero(LVal x) => x.IsSmallInt(out var a) ? a == 0 : Same(Zero, x) == true;
    private static bool IsOne(LVal x) => Same(One, x) == true;

    // ---- The library's numbers

    // floor: (let {do =(tr (trunc a)) (<=> (cmp a tr) {< (1- tr)} {tr})}): its integer part, or one less below it
    private static LVal? Floor(LVal a, out int way) {
        way = -1;
        var tr = Truncated(a, out var c);
        if (tr == null) return null;
        Mark(LVal.Number(c));           // (<=>'s: the comparison's Num)
        way = c < 0 ? 1 : 0;
        return c < 0 ? OneLess(tr) : tr;
    }

    // ceil: (let {do =(tr (trunc a)) (<=> (cmp a tr) {> (1+ tr)} {tr})})
    private static LVal? Ceil(LVal a, out int way) {
        way = -1;
        var tr = Truncated(a, out var c);
        if (tr == null) return null;
        Mark(LVal.Number(c));
        way = c > 0 ? 1 : 0;
        return c > 0 ? OneMore(tr) : tr;
    }

    // (truncate a), and a compared with it (cmp's): a rational of longs (its denominator above 0) without BigIntegers,
    // its integer part toward 0, and the sign of the rest (its numerator's)
    private static LVal? Truncated(LVal a, out int c) {
        if (a.IsNum && !a.IsLong(out _) && a.NumVal is Rat r && r.Fits && r.den > 0 && r.den <= long.MaxValue) {
            long n = r.Small, d = (long)r.den;
            var t = LVal.Number(n / d);
            Mark(t);
            c = n % d == 0 ? 0 : n < 0 ? -1 : 1;
            return t;
        }
        c = 0;
        var tr = Truncated(a);
        if (tr != null) c = CompareNums(a, tr);
        return tr;
    }

    // (floor (/ a b)): for integers, without the rational quotient made (no one sees it)
    private static LVal? FloorOf(LVal a, LVal b, out int way) {
        way = -1;
        if (a.IsSmallInt(out var x) && b.IsSmallInt(out var y) && y != 0 && y != -1) {
            Mark(a);
            Mark(b);
            var tr = LVal.Number(x / y);
            Mark(tr);
            if (x % y == 0) {           // (the quotient its integer part, and cmp's 0)
                Mark(Zero);
                way = 0;
                return tr;
            }
            var c = (x < 0) == (y < 0) ? 1 : -1;
            Mark(LVal.Number(c));
            way = c < 0 ? 1 : 0;
            return c < 0 ? OneLess(tr) : tr;
        }
        var q = Div(a, b);
        return q == null ? null : Floor(q, out way);
    }

    // mod: (- a (* b (floor (/ a b)))), of numbers
    private static LVal? Mod(LVal a, LVal b) {
        var f = FloorOf(a, b, out _);
        if (f == null) return null;
        var p = Mul(b, f);
        return p == null ? null : Sub(a, p);
    }

    // round: (if (neg? a) (- (floor (- 1/2 a))) (floor (+ a 1/2))): the nearest integer, a half away from zero
    private static LVal? Round(LVal a, out int way) {
        way = -1;
        if (!a.IsNum) return null;
        if (CompareNums(a, Zero) < 0) {
            var v = Sub(Half, a);
            var f = v == null ? null : Floor(v, out _);
            if (f == null) return null;
            way = 2;
            return Neg(f);
        }
        var s = Add(a, Half);
        return s == null ? null : Floor(s, out way);
    }

    // even?: (=0 (mod n 2)), Op1's in the call for an integer kept in its value (the Site mod left kept)
    private static LVal? Even(LVal n, out int way) {
        way = -1;
        if (!n.IsNum) return null;
        var m = Mod(n, Two);
        if (m == null) return null;
        if (IsLongNow(m, out var k)) {
            way = 0;
            return LVal.Bool(k == 0);
        }
        way = 1;
        return LVal.Bool(IsZero(m));
    }

    // pow: (if (not (eq b (trunc b))) (error ...) (if (-? b) (/ 1 (pow a (- b))) (if (zero? b) 1 (if (even? b) (*
    // (pow a (/ b 2)) (pow a (/ b 2))) (* a (pow a (1- b))))))): each call of it nests one more, even? three
    private static LVal? Pow(LVal a, LVal b, int room, out int way) {
        way = -1;
        if (room < 4) return null;
        var tb = Truncated(b);
        if (tb == null || Eq(b, tb) != true) return null;
        if (CompareNums(b, Zero) < 0) {
            var nb = Neg(b);
            var p = nb == null ? null : Pow(a, nb, room - 1, out _);
            var r = p == null ? null : Div(One, p);
            if (r == null || LVal.Interrupted) return null;     // (Ctrl-C as it worked: the code runs, and sees it)
            way = 0;
            return r;
        }
        if (IsZero(b)) {
            way = 1;
            return One;
        }
        var even = Even(b, out _);
        if (even == null) return null;
        if (!even.IsNIL) {
            var h = Div(b, Two);
            var p = h == null ? null : Pow(a, h, room - 1, out _);    // (the second the same, from the same: no more Nums)
            var r = p == null ? null : Mul(p, p);
            if (r == null || LVal.Interrupted) return null;
            way = 2;
            return r;
        }
        var bl = OneLess(b);
        var q = bl == null ? null : Pow(a, bl, room - 1, out _);
        var s = q == null ? null : Mul(a, q);
        if (s == null || LVal.Interrupted) return null;
        way = 3;
        return s;
    }

    // between?: (if (<= &1 &2) (and (>= &3 &1) (<= &3 &2)) (between? &2 &1 &3)), the last a tail call
    private static LVal? Between(LVal lo, LVal hi, LVal x, out int way) {
        way = -1;
        int w = 0;
        if (Compared(lo, hi) is not int c) return null;
        if (c > 0) {
            if (Compared(hi, lo) is not int d || d > 0) return null;    // (round and round: the code's)
            (lo, hi, w) = (hi, lo, 2);
        }
        if (Compared(x, lo) is not int g) return null;
        if (g < 0) {
            way = w;
            return LVal.NIL();
        }
        if (Compared(x, hi) is not int h) return null;
        way = w + 1;
        return LVal.Bool(h <= 0);
    }

    // avg: (/ (sum s) (len s)), sum as the built-in has it (+ of each item, from 0)
    private static LVal? Avg(LVal s, LVal.Native self) {
        if (!s.IsQExpr) return null;
        if (!self.Kept(0)) return s;    // (only the way)
        var acc = Zero;
        foreach (var c in s.Cells!) {
            if (LVal.Interrupted) return null;      // (Ctrl-C, seen by sum's + of each: the code runs, and sees it)
            var n = Add(acc, c);
            if (n == null) return null;
            acc = n;
        }
        return Div(acc, LVal.Number(s.Count));
    }

    // ??: (if a a (if &_ (unpack ?? &_))): the first that isn't NIL; the rest go through unpack, each evaluated again
    // (as itself: a value that's its own).  Its call binds &1 ... for the rest, and unpack's f and l if they go on
    private static LVal? Coalesce(List<LVal> args, out int way) {
        way = -1;
        BindsExtras(args.Count - 1);
        if (!args[0].IsNIL) {
            way = 0;
            return args[0];
        }
        if (args.Count == 1) {
            way = 1;
            return LVal.NIL();
        }
        for (int i = 1; i < args.Count; i++) if (!AsItself(args[i])) return null;
        Binds(F, L);
        for (int i = 1; i < args.Count; i++) {
            if (!args[i].IsNIL) {
                way = 2;
                return args[i];
            }
        }
        way = 3;
        return LVal.NIL();
    }

    // Whether a value evaluated again (as code: an item of the list unpack runs) is itself: not a name, not a call, not
    // a Q-expression with no scope (it'd be given one)
    private static bool AsItself(LVal v) => v.ValType switch {
        LVal.LE.SYM or LVal.LE.SEXPR => false,
        LVal.LE.QEXPR => v.Count == 0 || v.Scope != null,
        _ => true
    };

    // ---- Strings

    // (index-of s x): a string's (ordinal), or a list's (an item Equals x)
    private static LVal? IndexOf(LVal s, LVal x) {
        if (s.IsQExpr) {
            for (int i = 0; i < s.Count; i++) {
                var e = Same(s.Cells![i], x);
                if (e == null) return null;
                if (e == true) return LVal.Number(i);
            }
            return LVal.Number(-1);
        }
        if (!s.IsStr || !(x.IsStr || x.IsChar)) return null;
        return LVal.Number(s.StrVal.IndexOf(x.StrVal, StringComparison.Ordinal));
    }

    // starts-with?: (=0 (index-of s sub)), Op1's in the call for an integer kept in its value
    private static LVal? StartsWith(LVal s, LVal sub, out int way) {
        way = -1;
        var i = IndexOf(s, sub);
        if (i == null) return null;
        way = IsLongNow(i, out _) ? 0 : 1;
        return LVal.Bool(IsZero(i));
    }

    // contains?: (>=0 (index-of s sub))
    private static LVal? Contains(LVal s, LVal sub, LVal.Native self) {
        if (!s.IsQExpr && !(s.IsStr && (sub.IsStr || sub.IsChar))) return null;
        if (!self.Kept(0)) return s;    // (only the way)
        var i = IndexOf(s, sub);
        return i == null ? null : LVal.Bool(Compared(i, Zero) >= 0);
    }

    // ends-with?: (and (>= (len s) (len sub)) (eq (substring s (- (len s) (len sub))) sub)), of strings; Op2's >= in
    // the call for integers kept in their values
    private static LVal? EndsWith(LVal s, LVal sub, out int way) {
        way = -1;
        if (!s.IsStr || !sub.IsStr) return null;
        if (s.StrVal.Length < sub.StrVal.Length) {
            way = IsLongNow(LVal.Number(s.StrVal.Length), out _) && IsLongNow(LVal.Number(sub.StrVal.Length), out _) ? 0 : 1;
            return LVal.NIL();
        }
        way = 2;
        return LVal.Bool(s.StrVal.EndsWith(sub.StrVal, StringComparison.Ordinal));
    }

    // ---- Lists

    // zip: (if (and a b) (cons (list (fst a) (fst b)) (zip (tail a) (tail b))) NIL): its pairs each a list made in a
    // call of its own (list's: remembering that call's scope, a and b the lists left then); each call nests one more
    private static LVal? Zip(LVal a, LVal b, Name na, Name nb, LEnv? closure, int room, LVal.Native self, out int way) {
        way = -1;
        if (a.IsNIL || b.IsNIL) {
            way = 0;
            return LVal.NIL();
        }
        if (!a.IsQExpr || !b.IsQExpr) return null;
        way = 1;
        if (!self.Kept(1)) return a;    // (only the way)
        var n = Math.Min(a.Count, b.Count);
        if (n >= room) return null;
        for (int i = 0; i < n; i++) if (a.Cells![i].IsErr || b.Cells![i].IsErr) return null;     // (an item list's call stops at)
        a.Freeze();                     // (as the call binds them: their items too, as the lists left are)
        b.Freeze();
        var r = LVal.Qexpr();
        for (int i = 0; i < n; i++) {
            if (LVal.Interrupted) return null;      // (Ctrl-C: the code runs, and sees it)
            if (i > 0) {
                a = SharedTail(a);
                b = SharedTail(b);
            }
            var scope = new LEnv(closure) { IsCall = true };
            scope.Put(na, a);
            scope.Put(nb, b);
            var pair = LVal.Qexpr().Add(a.Cells![0]).Add(b.Cells![0]);
            pair.Scope = scope;
            r.Add(pair);
        }
        return r;
    }

    // (tail l) of a list that's shared (frozen): a new one of the items after its first (with its scope), shared too
    private static LVal SharedTail(LVal l) =>
        new LVal { ValType = LVal.LE.QEXPR, Cells = l.Cells!.GetRange(1, l.Count - 1), Scope = l.Scope, Frozen = true };

    // flatten: (if l (if (qexpr? (fst l)) (join (flatten (fst l)) (flatten (tail l))) (cons (fst l) (flatten (tail
    // l)))) NIL): its items that aren't lists, and theirs, in one new list; the rest of a list nests one more, and so
    // does a list in it
    private static LVal? Flatten(LVal l, int room, LVal.Native self, out int way) {
        way = -1;
        if (l.IsNIL) {
            way = 0;
            return LVal.NIL();
        }
        if (!l.IsQExpr) return null;
        way = l.Cells![0].IsQExpr ? 1 : 2;
        if (!self.Kept(way)) return l;  // (only the way)
        var r = LVal.Qexpr();
        bool Leaves(LVal x, int depth) {
            if (depth + x.Count >= room || LVal.Interrupted) return false;  // (Ctrl-C: the code runs, and sees it)
            for (int i = 0; i < x.Count; i++) {
                var c = x.Cells![i];
                if (c.IsErr) return false;      // (cons stops at it)
                if (!c.IsQExpr) r.Add(c);
                else if (!Leaves(c, depth + i + 1)) return false;
            }
            return true;
        }
        return Leaves(l, 0) ? r : null;
    }

    // remove-all: (if l (do =(hd (fst l)) =(tl (tail l)) (if (eq f hd) (remove-all f tl) (cons hd (remove-all f tl))))):
    // the items not equal to f (each kept nests one more call); hd and tl bound, for a list that isn't empty
    private static LVal? RemoveAll(LVal f, LVal l, int room, LVal.Native self, out int way) {
        way = -1;
        if (l.IsNIL) {
            way = 0;
            return LVal.NIL();
        }
        if (!l.IsQExpr) return null;
        Binds(Hd, Tl);
        var r = LVal.Qexpr();
        foreach (var hd in l.Cells!) {
            if (hd.IsErr || LVal.Interrupted) return null;     // (eq stops at an error; Ctrl-C: the code sees it)
            var e = Eq(f, hd);
            if (e == null) return null;
            if (e == true) continue;
            if (r.Count == 0 && !self.Kept(1)) {    // (only the way: an item's kept)
                way = 1;
                return l;
            }
            r.Add(hd);
        }
        if (r.Count >= room) return null;
        way = r.Count == 0 ? 0 : 1;
        return r.Count == 0 ? LVal.NIL() : r;
    }

    // remove-fst: (if l (do =(hd (fst l)) =(tl (tail l)) (if (eq f hd) tl (cons hd (remove-fst f tl))))): the list
    // without the first item equal to f (the list's tail itself, when it's the first); hd and tl bound, as remove-all's
    private static LVal? RemoveFst(LVal f, LVal l, int room, LVal.Native self, out int way) {
        way = -1;
        if (l.IsNIL) {
            way = 0;
            return LVal.NIL();
        }
        if (!l.IsQExpr) return null;
        Binds(Hd, Tl);
        int at = -1;
        for (int i = 0; i < l.Count && at < 0; i++) {
            if (l.Cells![i].IsErr || LVal.Interrupted) return null;     // (Ctrl-C: the code runs, and sees it)
            var e = Eq(f, l.Cells![i]);
            if (e == null) return null;
            if (e == true) at = i;
            else if (i == 0 && !self.Kept(2)) {     // (only the way: the first isn't the one)
                way = 2;
                return l;
            }
        }
        if ((at < 0 ? l.Count : at) >= room) return null;
        if (at == 0) {
            way = 1;
            return self.Kept(1) ? Sublist(l, 1, l.Count - 1).Freeze() : l;
        }
        var r = LVal.Qexpr();
        for (int i = 0; i < l.Count; i++) if (i != at) r.Add(l.Cells![i]);
        way = 2;
        return r;
    }

    // distinct: (if l (cons (fst l) (distinct (remove-all (fst l) (tail l)))) NIL): each item the first time it
    // comes; each nests one more, and remove-all nests one more and one for each item it keeps (as many, at most, as
    // the list has); items compared as eq compares them, as far as it goes into them (a function or a hash reached:
    // the code's), none an error (cons or eq stop at one).  remove-all's f and l bound, and its hd and tl for a list
    // of two items or more
    private static LVal? Distinct(LVal l, int room, LVal.Native self, out int way) {
        way = -1;
        if (l.IsNIL) {
            way = 0;
            return LVal.NIL();
        }
        if (!l.IsQExpr) return null;
        way = 1;
        if (!self.Kept(1)) return l;    // (only the way)
        if (l.Count >= room || l.Cells!.Exists(c => c.IsErr)) return null;
        Binds(F, L);
        if (l.Count > 1) Binds(Hd, Tl);
        var r = LVal.Qexpr();
        var items = l.Cells!;
        while (items.Count > 0) {
            if (LVal.Interrupted) return null;      // (Ctrl-C: the code runs, and sees it)
            var x = items[0];
            r.Add(x);
            var rest = new List<LVal>();
            for (int k = 1; k < items.Count; k++) {
                var e = Eq(x, items[k]);
                if (e == null) return null;
                if (e == false) rest.Add(items[k]);
            }
            items = rest;
        }
        return r;
    }

    // has-element?: (elem? x l): an item Equals x
    private static LVal? HasElement(LVal l, LVal x, LVal.Native self) {
        if (!l.IsQExpr) return l.IsNIL ? LVal.NIL() : null;
        if (!self.Kept(0)) return l;    // (only the way)
        foreach (var c in l.Cells!) {
            var e = Same(x, c);
            if (e == null) return null;
            if (e == true) return LVal.T();
        }
        return LVal.NIL();
    }

    // minimum, maximum: (unpack min &1), (unpack max &1): the list's least (most) item, by the built-in's fast way.
    // unpack's eval runs the list it makes, {min x...}, each item evaluated again (as itself), so that made list's
    // call is where the code leaves evaluation (made each time: not a Site to keep, but one made the same)
    private static LVal? Least(LVal l, LVal by, bool most) {
        // (none: min partially applied; an error: the call stops at it)
        if (!l.IsQExpr || l.Count == 0 || !l.Cells!.TrueForAll(c => !c.IsErr && AsItself(c))) return null;
        Extras(1);
        F.MakeLocal();
        L.MakeLocal();
        l.Freeze();
        var made = LVal.Qexpr().Add(by);
        foreach (var x in l.Cells) made.Add(x);
        var acc = l.Cells[0];
        for (int i = 1; i < l.Count; i++) {
            var c = acc.CompareTo(l.Cells[i]);
            if (most ? c <= 0 : c >= 0) acc = l.Cells[i];
        }
        LVal.Site = new LVal.CodeInfo { Code = made };
        return acc;
    }

    // ---- cond and case: their clauses' code run as their code runs it, through compiled code

    // A clause the natives run: {test value ...} written in a scope (a Q-expression evaluated there, remembering it)
    private static bool IsClause(LVal c) => c.IsQExpr && c.Count >= 2 && c.Scope != null;

    // A clause's test or value: its first item or its second, made a list (as head makes it, a made one), for eval to
    // run as code; kept with the clause's code (its copies share it)
    private static LVal Part(LVal c, int i) {
        var info = c.Info;
        var kept = info == null ? null : i == 0 ? info.Test : info.Value;
        if (kept != null && ReferenceEquals(kept.Cells![0], c.Cells![i])) return kept;
        var part = LVal.Qexpr().Add(c.Cells![i]);
        if (info != null) {
            if (i == 0) info.Test = part;
            else info.Value = part;
        }
        return part;
    }

    // The names a call binds its extra arguments to, &1 ... &n: Local (as a call made of the arguments makes them)
    private static int _extras;
    private static void Extras(int n) {
        for (; _extras < n; _extras++) Name.Of("&" + (_extras + 1)).MakeLocal();
    }

    // The call's scope the code has at a clause (for its code from there on: the then or the else of its if): the
    // arguments from that clause on bound as the call binds them (x first, for case)
    private static LEnv ClauseScope(LEnv? closure, Name? x, List<LVal> args, int from) {
        var scope = new LEnv(closure) { IsCall = true };
        int k = 0;
        if (x != null) scope.Put(x, args[k++]);
        LVal? extras = null;
        for (int i = from; i < args.Count; i++) {
            (extras ??= LVal.Qexpr()).Add(args[i]);
            scope.Put($"&{extras.Count}", args[i]);
        }
        if (extras != null) scope.Put(Name.Rest, extras);
        return scope;
    }

    // cond: (if &_ (if (eval (head (fst &_))) (eval (head (tail (fst &_)))) (unpack cond (tail &_))) (error ...)), and
    // case: (if &_ (if (eq x (eval (head (fst &_)))) ...  (unpack case (cons x (tail &_)))) (error ...)): each test run
    // where its clause was written (an error, the value), then the value of the first that's true, in tail position.
    // The next clause's is cond's (case's) call with the clauses after, through unpack (each evaluated again: as
    // itself); a clause of another shape, or none, is the code's from there; and a global its code uses changed by a
    // test, the code's from that test on
    private static LVal? Clauses(LVal.Native self, LVal f, List<LVal> args, bool isCase, LEnv? closure, Name? x,
            LVal.CallNode then, LVal.CallNode orElse) {
        int first = isCase ? 1 : 0;
        if (args.Count <= first || !IsClause(args[first])) return null;
        x?.MakeLocal();
        Extras(args.Count - first);
        foreach (var a in args) a.Freeze();
        int itself = args.Count;        // (from here on each argument is itself, evaluated again, and for case x is too)
        while (itself > first && AsItself(args[itself - 1])) itself--;
        if (isCase && !AsItself(args[0])) itself = int.MaxValue;
        for (int i = first; ; i++) {
            var c = args[i];
            var t = Test(c);
            if (t.IsErr) return t;
            bool yes = !isCase ? !t.IsNIL : args[0].IsSmallInt(out var p) && t.IsSmallInt(out var q) ? p == q : args[0].Equals(t);
            if (!self.Holds()) return (yes ? then : orElse).Tail(ClauseScope(closure, x, Rest(args, isCase, i), first));
            if (yes) return Part(c, 1).CallOf().Tail(c.Scope!);
            if (i + 1 < itself) return orElse.Tail(ClauseScope(closure, x, Rest(args, isCase, i), first));
            F.MakeLocal();              // (unpack's formals)
            L.MakeLocal();
            if (i + 1 == args.Count || !IsClause(args[i + 1])) return LVal.TailCall(f, Rest(args, isCase, i + 1));
        }
    }

    // A clause's test (case's key) run, as eval's call runs it (Ctrl-C seen first; an exception, an error)
    private static LVal Test(LVal c) {
        var intr = LVal.CheckInterrupt();
        if (intr != null) return intr;
        try {
            return Part(c, 0).CallOf().Eval(c.Scope!);
        }
        catch (ExitException) { throw; }
        catch (Exception ex) {
            return LVal.Err(FromHost(ex.Message));
        }
    }

    // The arguments from clause i on (x first, for case)
    private static List<LVal> Rest(List<LVal> args, bool isCase, int i) {
        var r = new List<LVal>(args.Count - i + 1);
        if (isCase) r.Add(args[0]);
        for (int k = i; k < args.Count; k++) r.Add(args[k]);
        return r;
    }

    // ---- The natives given to the library's functions

    // The library's functions the natives were written for, and those their code calls (-? is negative?, unpack is
    // cond's, case's, ??'s, minimum's and maximum's): their formals and bodies as lib/globals.dl has them (as they
    // print), read as its code is
    private static readonly Dictionary<string, (LVal Formals, LVal Body)> Library = new Dictionary<string, (string, string)> {
        ["floor"] = ("{a}", "{let {do (set tr (trunc a)) (<=> (cmp a tr) {< (1- tr)} {tr})}}"),
        ["ceil"] = ("{a}", "{let {do (set tr (trunc a)) (<=> (cmp a tr) {> (1+ tr)} {tr})}}"),
        ["round"] = ("{a}", "{(if (neg? a) (- (floor (- 1/2 a))) (floor (+ a 1/2)))}"),
        ["mod"] = ("{a b}", "{if (and (num? a) (num? b)) (- a (* b (floor (/ a b)))) (error \"Cannot perform 'mod' on non-numbers\")}"),
        ["div"] = ("{a b}", "{if (and (num? a) (num? b)) (floor (/ a b)) (error \"Cannot perform 'div' on non-numbers\")}"),
        ["divmod"] = ("{a b}", "{if (and (num? a) (num? b)) (cons (div a b) (list (mod a b))) (error \"Cannot perform 'divmod' on non-numbers\")}"),
        ["even?"] = ("{n}", "{if (num? n) (=0 (mod n 2)) (error \"Cannot perform 'even?' on non-number\")}"),
        ["odd?"] = ("{n}", "{if (num? n) (=1 (mod n 2)) (error \"Cannot perform 'odd?' on non-number\")}"),
        ["pow"] = ("{a b}", "{if (not (eq b (trunc b))) (error \"'pow' takes a whole number as its power\") (if (-? b) (/ 1 (pow a (- b))) (if (zero? b) 1 (if (even? b) (* (pow a (/ b 2)) (pow a (/ b 2))) (* a (pow a (1- b))))))}"),
        ["square"] = ("{n}", "{* n n}"),
        ["cube"] = ("{n}", "{* n (square n)}"),
        [">=0"] = ("{n}", "{>= n 0}"),
        ["<=0"] = ("{n}", "{<= n 0}"),
        [">0"] = ("{n}", "{> n 0}"),
        ["<0"] = ("{n}", "{< n 0}"),
        ["!=0"] = ("{n}", "{not (zero? n)}"),
        ["!=1"] = ("{n}", "{not (one? n)}"),
        ["positive?"] = ("{a}", "{if (num? a) (pos? a) (error \"Only numbers can be positive\")}"),
        ["negative?"] = ("{a}", "{if (num? a) (neg? a) (error \"Only numbers can be negative\")}"),
        ["-?"] = ("{a}", "{if (num? a) (neg? a) (error \"Only numbers can be negative\")}"),
        ["between?"] = ("NIL", "{if (<= &1 &2) (and (>= &3 &1) (<= &3 &2)) (between? &2 &1 &3)}"),
        ["xor"] = ("{a b}", "{and (or a b) (not (and a b))}"),
        ["avg"] = ("{s}", "{/ (sum s) (len s)}"),
        ["??"] = ("{a}", "{if a a (if &_ (unpack ?? &_))}"),
        ["unpack"] = ("{f l}", "{eval (cons f l)}"),
        ["starts-with?"] = ("{s sub}", "{=0 (index-of s sub)}"),
        ["ends-with?"] = ("{s sub}", "{and (>= (len s) (len sub)) (eq (substring s (- (len s) (len sub))) sub)}"),
        ["contains?"] = ("{s sub}", "{>=0 (index-of s sub)}"),
        ["cond"] = ("NIL", "{if &_ (if (eval (head (fst &_))) (eval (head (tail (fst &_)))) (unpack cond (tail &_))) (error \"Selection not found\")}"),
        ["case"] = ("{x}", "{if &_ (if (eq x (eval (head (fst &_)))) (eval (head (tail (fst &_)))) (unpack case (cons x (tail &_)))) (error \"Case not found\")}"),
        ["zip"] = ("{a b}", "{if (and a b) (cons (list (fst a) (fst b)) (zip (tail a) (tail b))) NIL}"),
        ["flatten"] = ("{l}", "{if l (if (qexpr? (fst l)) (join (flatten (fst l)) (flatten (tail l))) (cons (fst l) (flatten (tail l)))) NIL}"),
        ["distinct"] = ("{l}", "{if l (cons (fst l) (distinct (remove-all (fst l) (tail l)))) NIL}"),
        ["remove-all"] = ("{f l}", "{if l (do (set hd (fst l)) (set tl (tail l)) (if (eq f hd) (remove-all f tl) (cons hd (remove-all f tl))))}"),
        ["remove-fst"] = ("{f l}", "{if l (do (set hd (fst l)) (set tl (tail l)) (if (eq f hd) tl (cons hd (remove-fst f tl))))}"),
        ["has-element?"] = ("{l x}", "{elem? x l}"),
        ["minimum"] = ("NIL", "{unpack min &1}"),
        ["maximum"] = ("NIL", "{unpack max &1}"),
    }.ToDictionary(kv => kv.Key, kv => (CodeOf(kv.Value.Item1), CodeOf(kv.Value.Item2)));

    // A text's code, as the reader reads it (its one expression)
    private static LVal CodeOf(string text) => LVal.ReadExprFromTokens(Parser.Tokenize(new StringReader(text)).ToList())!.Cells![0];

    // The built-ins the library's code calls by other names (lib/globals.dl's aliases), and the names its code binds
    // itself (no global's)
    private static readonly Dictionary<string, string> Aliases = new() { ["trunc"] = "truncate", ["=0"] = "zero?", ["=1"] = "one?" };
    private static readonly string[] Locals = { "tr", "hd", "tl", "&_", "&1", "&2", "&3" };

    // The library's functions given their native ways (lib/globals.dl loaded), each that's as the native was written
    // for, and calls what it was written for (builtins: the built-ins as AddBuiltins made them)
    public static void AddNatives(LEnv e, Dictionary<Name, LVal> builtins) {
        var g = e.Root;
        LVal.Native? Native(string name, int min, int max, int nest, int ways) => Attach(g, builtins, name, min, max, nest, ways);

        // (a function of numbers, its ways and the calls its code nests)
        void Way1(string name, int nest, int ways, Func<LVal, (LVal?, int)> way) {
            var n = Native(name, 1, 1, nest, ways);
            if (n != null) n.Way = (List<LVal> a, int room, LVal.Native self, out int w) => { (var v, w) = way(a[0]); return v; };
        }
        void Way2(string name, int nest, int ways, Func<LVal, LVal, (LVal?, int)> way) {
            var n = Native(name, 2, 2, nest, ways);
            if (n != null) n.Way = (List<LVal> a, int room, LVal.Native self, out int w) => { (var v, w) = way(a[0], a[1]); return v; };
        }

        Way1("floor", 0, 2, a => (Floor(a, out var w), w));
        Way1("ceil", 0, 2, a => (Ceil(a, out var w), w));
        Way1("round", 1, 3, a => (Round(a, out var w), w));
        Way2("mod", 1, 1, (a, b) => (Mod(a, b), 0));
        Way2("div", 0, 2, (a, b) => (FloorOf(a, b, out var w), w));
        Way2("divmod", 2, 1, (a, b) => {
            var d = FloorOf(a, b, out _);
            var m = d == null ? null : Mod(a, b);
            return (m == null ? null : LVal.Qexpr().Add(d!).Add(m), 0);
        });
        Way1("even?", 2, 2, n => (Even(n, out var w), w));
        Way1("odd?", 2, 1, n => {
            var m = n.IsNum ? Mod(n, Two) : null;
            return (m == null ? null : LVal.Bool(IsOne(m)), 0);
        });
        var pow = Native("pow", 2, 2, 0, 4);
        if (pow != null) pow.Way = (List<LVal> a, int room, LVal.Native self, out int w) => Pow(a[0], a[1], room, out w);
        Way1("square", 0, 1, n => (Mul(n, n), 0));
        Way1("cube", 1, 1, n => {
            var s = Mul(n, n);
            return (s == null ? null : Mul(n, s), 0);
        });
        Way1(">=0", 0, 1, n => (LVal.Bool(Compared(n, Zero) >= 0), 0));
        Way1("<=0", 0, 1, n => (LVal.Bool(Compared(n, Zero) <= 0), 0));
        Way1(">0", 0, 1, n => (LVal.Bool(Compared(n, Zero) > 0), 0));
        Way1("<0", 0, 1, n => (LVal.Bool(Compared(n, Zero) < 0), 0));
        Way1("!=0", 0, 1, n => (LVal.Bool(!IsZero(n)), 0));
        Way1("!=1", 0, 1, n => (LVal.Bool(!IsOne(n)), 0));
        Way1("positive?", 0, 1, a => (a.IsNum ? LVal.Bool(CompareNums(Zero, a) < 0) : null, 0));
        Way1("negative?", 0, 1, a => (a.IsNum ? LVal.Bool(CompareNums(a, Zero) < 0) : null, 0));
        var between = Native("between?", 3, 3, 0, 4);
        if (between != null) between.Way = (List<LVal> a, int room, LVal.Native self, out int w) => Between(a[0], a[1], a[2], out w);
        Way2("xor", 0, 2, (a, b) => a.IsNIL && b.IsNIL ? (LVal.NIL(), 0) : (LVal.Bool(a.IsNIL || b.IsNIL), 1));
        var avg = Native("avg", 1, 1, 0, 1);
        if (avg != null) avg.Way = (List<LVal> a, int room, LVal.Native self, out int w) => { w = 0; return Avg(a[0], self); };
        var coalesce = Native("??", 1, int.MaxValue, 0, 4);
        if (coalesce != null) coalesce.Way = (List<LVal> a, int room, LVal.Native self, out int w) => Coalesce(a, out w);

        // strings
        Way2("starts-with?", 0, 2, (s, sub) => (StartsWith(s, sub, out var w), w));
        Way2("ends-with?", 0, 3, (s, sub) => (EndsWith(s, sub, out var w), w));
        var contains = Native("contains?", 2, 2, 0, 1);
        if (contains != null) contains.Way = (List<LVal> a, int room, LVal.Native self, out int w) => { w = 0; return Contains(a[0], a[1], self); };

        // cond and case
        void Clause(string name, bool isCase) {
            var n = Native(name, isCase ? 1 : 0, int.MaxValue, 0, 0);
            if (n == null) return;
            var f = g.Get(Name.Of(name));
            var branches = f.Body!.Cells![2].Cells!;     // (the inner if's then and else)
            var closure = f.Closure;
            var x = isCase ? f.Formals!.Cells![0].SymName : null;
            LVal.CallNode then = branches[2].CallOf(), orElse = branches[3].CallOf();
            n.Code = (fn, a) => Clauses(n, fn, a, isCase, closure, x, then, orElse);
        }
        Clause("cond", false);
        Clause("case", true);

        // lists
        var zip = Native("zip", 2, 2, 0, 2);
        if (zip != null) {
            var f = g.Get(Name.Of("zip"));
            Name na = f.Formals!.Cells![0].SymName, nb = f.Formals!.Cells![1].SymName;
            var closure = f.Closure;
            zip.Way = (List<LVal> a, int room, LVal.Native self, out int w) => Zip(a[0], a[1], na, nb, closure, room, self, out w);
        }
        var flatten = Native("flatten", 1, 1, 0, 3);
        if (flatten != null) flatten.Way = (List<LVal> a, int room, LVal.Native self, out int w) => Flatten(a[0], room, self, out w);
        var distinct = Native("distinct", 1, 1, 0, 2);
        if (distinct != null) distinct.Way = (List<LVal> a, int room, LVal.Native self, out int w) => Distinct(a[0], room, self, out w);
        var removeAll = Native("remove-all", 2, 2, 0, 2);
        if (removeAll != null) removeAll.Way = (List<LVal> a, int room, LVal.Native self, out int w) => RemoveAll(a[0], a[1], room, self, out w);
        var removeFst = Native("remove-fst", 2, 2, 0, 3);
        if (removeFst != null) removeFst.Way = (List<LVal> a, int room, LVal.Native self, out int w) => RemoveFst(a[0], a[1], room, self, out w);
        var hasElement = Native("has-element?", 2, 2, 0, 1);
        if (hasElement != null) hasElement.Way = (List<LVal> a, int room, LVal.Native self, out int w) => { w = 0; return HasElement(a[0], a[1], self); };
        void Extreme(string name, string by, bool most) {
            var n = Native(name, 1, 1, 0, 0);
            if (n == null) return;
            var fn = g.Get(Name.Of(by));
            n.Code = (f, a) => Least(a[0], fn, most);
        }
        Extreme("minimum", "min", false);
        Extreme("maximum", "max", true);
    }

    // name, lib/globals.dl's function, given a native way (to be set), if it's as the native was written for (Written);
    // the globals its code uses, as they are now, the native's
    private static LVal.Native? Attach(LEnv g, Dictionary<Name, LVal> builtins, string name, int min, int max, int nest, int ways) {
        var uses = new Dictionary<Name, LEnv.Slot>();
        if (!g.TryGetLocal(Name.Of(name), out var f) || !Written(g, builtins, f, name, uses)) return null;
        var n = new LVal.Native(min, max, nest, ways, uses.Values.ToArray());
        f.SetNative(n);
        return n;
    }

    // Whether f is the library's function name, its code as Library has it (SameCode), made at the top level, and each
    // name in its code (its formals aside) one of the library's own locals, bound globally nowhere, or a global that's
    // as the natives were written for (Expected): each of those in uses
    private static bool Written(LEnv g, Dictionary<Name, LVal> builtins, LVal f, string name, Dictionary<Name, LEnv.Slot> uses) {
        if (!Library.TryGetValue(name, out var code) || !f.IsFun || f.BuiltinVal != null || f.IsFexpr
            || f.Env is { Count: > 0 } || !ReferenceEquals(f.Closure, g)
            || !SameCode(f.Formals!, code.Formals, true) || !SameCode(f.Body!, code.Body, true)) return false;
        var formals = f.Formals!.Cells!.Select(c => c.SymName).ToHashSet();
        bool Walk(LVal x) {
            if (!x.IsSym) return x.Cells == null || x.Cells.TrueForAll(Walk);
            var s = x.SymName;
            if (formals.Contains(s)) return true;
            return Locals.Contains(s.Text) ? g.SlotOf(s) == null : Expected(g, builtins, s, uses);
        }
        return Walk(f.Body!);
    }

    // Whether name's global (kept in uses) is as the natives were written for: the library's function of that name
    // (Written), or the built-in of that name (or of the one it's an alias of), as AddBuiltins made it
    private static bool Expected(LEnv g, Dictionary<Name, LVal> builtins, Name name, Dictionary<Name, LEnv.Slot> uses) {
        if (uses.ContainsKey(name)) return true;
        if (g.SlotOf(name) is not LEnv.Slot slot) return false;
        uses[name] = slot;
        if (Library.ContainsKey(name.Text)) return Written(g, builtins, slot.Value, name.Text, uses);
        return builtins.TryGetValue(Name.Of(Aliases.GetValueOrDefault(name.Text, name.Text)), out var b) && ReferenceEquals(slot.Value, b);
    }

    // Whether x is the code y (the library's, read): values of the same kinds, with the same names and strings, the
    // very integers the reader made (shared ones, which the natives give a Num to as the code does), other numbers of
    // the same kind and value, NIL itself, and lists of the same, none remembering a scope (where eval would run it)
    // but the formals or the body themselves (top).  No integer's given a Num for it (the code's stay kept in their
    // values, as they are till it runs)
    private static bool SameCode(LVal x, LVal y, bool top = false) {
        if (x.ValType != y.ValType) return false;
        switch (y.ValType) {
            case LVal.LE.SYM: return x.SymVal == y.SymVal;
            case LVal.LE.STR: return x.StrVal == y.StrVal;
            case LVal.LE.T: return true;
            case LVal.LE.NUM:
                if (y.IsPlainInt(out _)) return ReferenceEquals(x, y);
                return !x.IsPlainInt(out _) && x.NumVal!.GetType() == y.NumVal!.GetType() && x.NumVal.ToString() == y.NumVal.ToString();
            case LVal.LE.QEXPR:
            case LVal.LE.SEXPR:
                if (x.Count != y.Count || (!top && (x.Scope != null || (y.Count == 0 && !ReferenceEquals(x, y))))) return false;
                for (int i = 0; i < y.Count; i++) if (!SameCode(x.Cells![i], y.Cells![i])) return false;
                return true;
        }
        return false;
    }
}
