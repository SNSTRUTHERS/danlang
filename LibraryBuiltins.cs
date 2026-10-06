using System.Numerics;

// The library's most used functions, built in (lib/globals.dl had them, in danlang): each does what its definition
// there did, with an item of a list as it's written; a comparison or a sum is the built-in's own (<, eq, + ...), so
// the results are theirs
public partial class Builtins
{
    // A value's negation, for >= and <=: T for NIL, NIL for anything else (an error, itself)
    private static LVal Not(LVal v) => v.IsErr ? v : LVal.Bool(v.IsNIL);

    // A list argument's items (NIL: none).  OUT: null, it isn't a list
    private static List<LVal>? Items(LVal l) => l.IsNIL ? new List<LVal>() : l.IsQExpr ? l.Cells! : null;

    // The list's item n (a number: 0 on), as it's written; past the list's end, or before it, an error
    private static LVal Nth(LVal n, LVal l, string fn) {
        var w = Whole(n, fn, "an index", out var i);
        if (w.IsErr) return w;
        var items = Items(l);
        if (items == null) return LVal.Err($"'{fn}' expects a list");
        if (i < 0 || i >= items.Count) return LVal.Err("'nth': the list has no such item");
        return items[(int)i];
    }

    // take's and drop's count: a whole number, 0 or more; or an error
    private static LVal Count(LVal n, string fn, out BigInteger k) {
        var w = Whole(n, fn, "a count", out k);
        if (w.IsErr) return w;
        return k < 0 ? LVal.Err($"'{fn}': a count can't be negative") : w;
    }

    private static void AddLibraryBuiltins(LEnv e) {
        LVal plus = e.Get("+"), minus = e.Get("-"), times = e.Get("*"), eq = e.Get("eq"), lt = e.Get("<"), gt = e.Get(">");
        LVal zero = LVal.Number(BigInteger.Zero).Freeze(), one = LVal.Number(BigInteger.One).Freeze();

        // logic, comparison and arithmetic
        AddBuiltin(e, "not",  (e, a) => LVal.Bool(a[0].IsNIL));
        AddBuiltin(e, "==",   (e, a) => Apply(e, eq, a[0], a[1]));
        AddBuiltin(e, ">=",   (e, a) => Not(Apply(e, lt, a[0], a[1])));
        AddBuiltin(e, "<=",   (e, a) => Not(Apply(e, gt, a[0], a[1])));
        AddBuiltin(e, "neg?", (e, a) => Apply(e, lt, a[0], zero));
        AddBuiltin(e, "pos?", (e, a) => Apply(e, lt, zero, a[0]));
        AddBuiltin(e, "zero?", (e, a) => Apply(e, eq, zero, a[0]));
        AddBuiltin(e, "one?", (e, a) => Apply(e, eq, one, a[0]));
        AddBuiltin(e, "1+",   (e, a) => Apply(e, plus, a[0], one));
        AddBuiltin(e, "1-",   (e, a) => Apply(e, minus, a[0], one));
        AddBuiltin(e, "abs",  (e, a) => {
            if (!a[0].IsNum) return LVal.Err("Cannot perform 'abs' on non-number");
            var neg = Apply(e, lt, a[0], zero);
            return neg.IsErr ? neg : neg.IsNIL ? a[0] : Apply(e, minus, a[0]);
        });

        // lists: an item as it's written
        AddBuiltin(e, "cons", (e, a) => {
            var items = Items(a[1]);
            if (items == null) return LVal.Err("'cons' expects a list second");
            var q = LVal.Qexpr();
            q.Add(a[0]);
            foreach (var c in items) q.Add(c);
            return q;
        });
        AddBuiltin(e, "fst",  (e, a) => Nth(zero, a[0], "fst"));
        AddBuiltin(e, "snd",  (e, a) => Nth(one, a[0], "snd"));
        AddBuiltin(e, "thd",  (e, a) => Nth(LVal.Number(new BigInteger(2)), a[0], "thd"));
        AddBuiltin(e, "nth",  (e, a) => Nth(a[0], a[1], "nth"));
        AddBuiltin(e, "last", (e, a) => {
            var items = Items(a[0]);
            if (items == null) return LVal.Err("'last' expects a list");
            return Nth(LVal.Number(new BigInteger(items.Count - 1)), a[0], "last");
        });
        // take, drop: the first n items, or all but them (n a whole number, 0 or more; past the end, all of them)
        AddBuiltin(e, "take", (e, a) => {
            var w = Count(a[0], "take", out var k);
            if (w.IsErr) return w;
            var items = Items(a[1]);
            if (items == null) return LVal.Err("'take' expects a list");
            var q = LVal.Qexpr();
            foreach (var c in items.Take(k < items.Count ? (int)k : items.Count)) q.Add(c);
            return q;
        });
        AddBuiltin(e, "drop", (e, a) => {
            var w = Count(a[0], "drop", out var k);
            if (w.IsErr) return w;
            var items = Items(a[1]);
            if (items == null) return LVal.Err("'drop' expects a list");
            var q = LVal.Qexpr();
            foreach (var c in items.Skip(k < items.Count ? (int)k : items.Count)) q.Add(c);
            return q;
        });
        LVal Elem(LVal x, LVal l, string fn) {
            var items = Items(l);
            if (items == null) return LVal.Err($"'{fn}' expects a list");
            return LVal.Bool(items.Any(c => x.Equals(c)));
        }
        AddBuiltin(e, "elem?", (e, a) => Elem(a[0], a[1], "elem?"));
        AddBuiltin(e, "in?",   (e, a) => Elem(a[0], a[1], "in?"));

        // f over a list's items: each applied in turn (the first error, the value)
        AddBuiltin(e, "map", (e, a) => {
            var items = Items(a[1]);
            if (items == null) return LVal.Err("'map' expects a function and a list");
            var q = LVal.Qexpr();
            foreach (var c in items) {
                var r = Apply(e, a[0], c);
                if (r.IsErr) return r;
                q.Add(r);
            }
            return q;
        });
        AddBuiltin(e, "filter", (e, a) => {
            var items = Items(a[1]);
            if (items == null) return LVal.Err("'filter' expects a function and a list");
            var q = LVal.Qexpr();
            foreach (var c in items) {
                var r = Apply(e, a[0], c);
                if (r.IsErr) return r;
                if (!r.IsNIL) q.Add(c);
            }
            return q;
        });
        AddBuiltin(e, "foldl", (e, a) => {
            var items = Items(a[2]);
            if (items == null) return LVal.Err("'foldl' expects a function, a value and a list");
            var acc = a[1];
            foreach (var c in items) {
                acc = Apply(e, a[0], acc, c);
                if (acc.IsErr) return acc;
            }
            return acc;
        });
        AddBuiltin(e, "foldr", (e, a) => {
            var items = Items(a[2]);
            if (items == null) return LVal.Err("'foldr' expects a function, a value and a list");
            var acc = a[1];
            for (int i = items.Count - 1; i >= 0; i--) {
                acc = Apply(e, a[0], items[i], acc);
                if (acc.IsErr) return acc;
            }
            return acc;
        });
        // any? (T at the first item f says T of), all? (NIL at the first it says NIL of), find (that item), count
        LVal Test(LVal f, LVal l, string fn, Func<LVal, LVal, LVal?> step, Func<LVal> end) {
            var items = Items(l);
            if (items == null) return LVal.Err($"'{fn}' expects a function and a list");
            foreach (var c in items) {
                var r = Apply(e, f, c);
                if (r.IsErr) return r;
                var v = step(c, r);
                if (v != null) return v;
            }
            return end();
        }
        AddBuiltin(e, "any?",  (e, a) => Test(a[0], a[1], "any?", (c, r) => r.IsNIL ? null : LVal.Bool(true), LVal.NIL));
        AddBuiltin(e, "all?",  (e, a) => Test(a[0], a[1], "all?", (c, r) => r.IsNIL ? LVal.NIL() : null, () => LVal.Bool(true)));
        AddBuiltin(e, "find",  (e, a) => Test(a[0], a[1], "find", (c, r) => r.IsNIL ? null : c, LVal.NIL));
        AddBuiltin(e, "count", (e, a) => {
            int n = 0;
            var r = Test(a[0], a[1], "count", (c, x) => { if (!x.IsNIL) n++; return null; }, LVal.NIL);
            return r.IsErr ? r : LVal.Number(new BigInteger(n));
        });

        // sum, product: + (or *) over the items, from 0 (or 1); min, max: the arguments' least (or most), by < (>)
        LVal Fold(LVal f, LVal z, LVal l, string fn) {
            var items = Items(l);
            if (items == null) return LVal.Err($"'{fn}' expects a list");
            var acc = z;
            foreach (var c in items) {
                acc = Apply(e, f, acc, c);
                if (acc.IsErr) return acc;
            }
            return acc;
        }
        AddBuiltin(e, "sum",     (e, a) => Fold(plus, zero, a[0], "sum"));
        AddBuiltin(e, "product", (e, a) => Fold(times, one, a[0], "product"));
        LVal Least(LVal by, LVal a) {
            var acc = a[0];
            for (int i = 1; i < a.Count; i++) {
                var r = Apply(e, by, acc, a[i]);
                if (r.IsErr) return r;
                if (r.IsNIL) acc = a[i];
            }
            return acc;
        }
        AddBuiltin(e, "min", (e, a) => Least(lt, a));
        AddBuiltin(e, "max", (e, a) => Least(gt, a));
    }
}
