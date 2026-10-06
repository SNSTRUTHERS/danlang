using System.Numerics;

// The most used built-ins' fast ways (Compiler.cs runs them, given the values without a list made for them): each does
// what the built-in does for the values it takes (plain integers, a string and an index ...), and is NIL for anything
// else, which the built-in itself does (its errors too).  And the special forms Compiler.cs runs itself
public partial class Builtins
{
    private static void Fast(LEnv e, string name, Func<LVal, LVal?>? one, Func<LVal, LVal, LVal?>? two = null) =>
        e.Get(name).SetFast(one, two);

    private static BigInteger? PlainInt(LVal v) => v.IsPlainInt(out var n) ? n : null;
    private static bool Ints(LVal x, LVal y, out BigInteger p, out BigInteger q) {
        q = default;
        return x.IsPlainInt(out p) && y.IsPlainInt(out q);
    }

    private static void AddFastWays(LEnv e) {
        LVal.IfFn = e.Get("if").BuiltinVal;
        LVal.DoFn = e.Get("do").BuiltinVal;
        LVal.AndFn = e.Get("and").BuiltinVal;
        LVal.OrFn = e.Get("or").BuiltinVal;
        LVal.SetBangFn = e.Get("set!").BuiltinVal;
        LVal.SetFn = e.Get("set").BuiltinVal;
        LVal.DefFn = e.Get("def").BuiltinVal;
        LVal.WhileFn = e.Get("while").BuiltinVal;

        // (any number of plain integers: the sum, the difference, the product, the bits)
        static LVal? IntsN(LVal[] x, BigInteger empty, Func<BigInteger, BigInteger, BigInteger> op, bool negateOne = false) {
            if (x.Length == 0) return LVal.Number(empty);
            if (!x[0].IsPlainInt(out var r)) return null;
            if (x.Length == 1 && negateOne) return LVal.Number(-r);
            for (int i = 1; i < x.Length; i++) {
                if (!x[i].IsPlainInt(out var n)) return null;
                r = op(r, n);
            }
            return LVal.Number(r);
        }
        e.Get("+").SetFastN(x => IntsN(x, 0, (p, q) => p + q));
        e.Get("-").SetFastN(x => IntsN(x, 0, (p, q) => p - q, true));
        e.Get("*").SetFastN(x => IntsN(x, 1, (p, q) => p * q));
        e.Get("bit-and").SetFastN(x => x.Length == 0 ? null : IntsN(x, 0, (p, q) => p & q));
        e.Get("bit-or").SetFastN(x => x.Length == 0 ? null : IntsN(x, 0, (p, q) => p | q));
        e.Get("bit-xor").SetFastN(x => x.Length == 0 ? null : IntsN(x, 0, (p, q) => p ^ q));
        // (min, max: as the library's Least has it: each value in turn taken unless the one so far is before it (after it))
        e.Get("min").SetFastN(x => { var acc = x[0]; for (int i = 1; i < x.Length; i++) if (acc.CompareTo(x[i]) >= 0) acc = x[i]; return acc; });
        e.Get("max").SetFastN(x => { var acc = x[0]; for (int i = 1; i < x.Length; i++) if (acc.CompareTo(x[i]) <= 0) acc = x[i]; return acc; });

        var zero = LVal.Number(0);
        var one = LVal.Number(1);

        // arithmetic and comparison
        Fast(e, "+", null, (x, y) => Ints(x, y, out var p, out var q) ? LVal.Number(p + q) : null);
        Fast(e, "-", x => PlainInt(x) is BigInteger p ? LVal.Number(-p) : null,
            (x, y) => Ints(x, y, out var p, out var q) ? LVal.Number(p - q) : null);
        Fast(e, "*", null, (x, y) => Ints(x, y, out var p, out var q) ? LVal.Number(p * q) : null);
        Fast(e, "1+", x => PlainInt(x) is BigInteger p ? LVal.Number(p + 1) : null);
        Fast(e, "1-", x => PlainInt(x) is BigInteger p ? LVal.Number(p - 1) : null);
        Fast(e, "<", null, (x, y) => LVal.Bool(x.CompareTo(y) < 0));
        Fast(e, ">", null, (x, y) => LVal.Bool(x.CompareTo(y) > 0));
        Fast(e, "<=", null, (x, y) => LVal.Bool(x.CompareTo(y) <= 0));
        Fast(e, ">=", null, (x, y) => LVal.Bool(x.CompareTo(y) >= 0));
        Fast(e, "cmp", null, (x, y) => LVal.Number(x.CompareTo(y)));
        Fast(e, "eq", null, (x, y) => LVal.Bool(x.Equals(y)));
        Fast(e, "==", null, (x, y) => LVal.Bool(x.Equals(y)));
        Fast(e, "neq", null, (x, y) => LVal.Bool(!x.Equals(y)));
        Fast(e, "not", x => LVal.Bool(x.IsNIL));
        Fast(e, "zero?", x => LVal.Bool(zero.Equals(x)));
        Fast(e, "one?", x => LVal.Bool(one.Equals(x)));
        Fast(e, "pos?", x => LVal.Bool(zero.CompareTo(x) < 0));
        Fast(e, "neg?", x => LVal.Bool(x.CompareTo(zero) < 0));
        Fast(e, "min", null, (x, y) => x.CompareTo(y) < 0 ? x : y);
        Fast(e, "max", null, (x, y) => x.CompareTo(y) > 0 ? x : y);

        // bits and bytes
        Fast(e, "bit-and", x => PlainInt(x) is BigInteger ? x : null, (x, y) => Ints(x, y, out var p, out var q) ? LVal.Number(p & q) : null);
        Fast(e, "bit-or", x => PlainInt(x) is BigInteger ? x : null, (x, y) => Ints(x, y, out var p, out var q) ? LVal.Number(p | q) : null);
        Fast(e, "bit-xor", x => PlainInt(x) is BigInteger ? x : null, (x, y) => Ints(x, y, out var p, out var q) ? LVal.Number(p ^ q) : null);
        Fast(e, "bit-not", x => PlainInt(x) is BigInteger p ? LVal.Number(-p - 1) : null);
        Fast(e, "shl", null, (x, y) => Ints(x, y, out var p, out var q) && q >= int.MinValue && q <= int.MaxValue ? LVal.Number(p << (int)q) : null);
        Fast(e, "shr", null, (x, y) => Ints(x, y, out var p, out var q) && q >= int.MinValue && q <= int.MaxValue ? LVal.Number(p >> (int)q) : null);
        Fast(e, "bit?", null, (x, y) => Ints(x, y, out var p, out var q) && q >= int.MinValue && q <= int.MaxValue
            ? LVal.Bool(!((p >> (int)q) & 1).IsZero) : null);
        Fast(e, "lo", x => PlainInt(x) is BigInteger p ? LVal.Number(p & 255) : null);
        Fast(e, "hi", x => PlainInt(x) is BigInteger p ? LVal.Number((p >> 8) & 255) : null);
        Fast(e, "word", null, (x, y) => Ints(x, y, out var p, out var q) ? LVal.Number((p & 255) + 256 * (q & 255)) : null);

        // strings and characters
        Fast(e, "char-at", null, (x, y) => (x.IsStr || x.IsChar) && y.IsPlainInt(out var i) && i >= 0 && i < x.StrVal.Length
            ? LVal.Char(x.StrVal[(int)i]) : null);
        Fast(e, "char-code", x => (x.IsStr || x.IsChar) && x.StrVal.Length == 1 ? LVal.Number(x.StrVal[0]) : null);
        Fast(e, "code-char", x => x.IsPlainInt(out var n) && n >= 0 && n <= 255 ? LVal.Char((char)(int)n) : null);

        // lists and hashes
        Fast(e, "len", x => x.ValType switch {
            LVal.LE.QEXPR => LVal.Number(x.Count),
            LVal.LE.STR   => LVal.Number(x.StrVal.Length),
            LVal.LE.HASH  => LVal.Number(x.HashValue!.Count),
            LVal.LE.BUFFER => LVal.Number(x.BufferValue!.Length),
            _             => null,
        });
        Fast(e, "fst", x => x.IsQExpr && x.Count > 0 ? x.Cells![0] : null);
        Fast(e, "snd", x => x.IsQExpr && x.Count > 1 ? x.Cells![1] : null);
        Fast(e, "thd", x => x.IsQExpr && x.Count > 2 ? x.Cells![2] : null);
        Fast(e, "nth", null, (n, l) => l.IsQExpr && n.IsPlainInt(out var i) && i >= 0 && i < l.Count ? l.Cells![(int)i] : null);
        Fast(e, "item-at", null, (l, n) => l.IsQExpr && n.IsPlainInt(out var i) && i >= 0 && i < l.Count ? l.Cells![(int)i] : null);
        Fast(e, "hash-get", null, (h, k) => h.IsHash ? h.HashValue!.Get(k) : null);
    }
}
