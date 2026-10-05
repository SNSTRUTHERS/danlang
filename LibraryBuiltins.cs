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
        if (!n.IsNum) return LVal.Err($"'{fn}' expects a Number and a list");
        var items = Items(l);
        if (items == null) return LVal.Err($"'{fn}' expects a list");
        if (n.NumVal!.CompareTo(Num.Zero) < 0 || n.NumVal.CompareTo(new Int(items.Count)) >= 0)
            return LVal.Err("'nth': the list has no such item");
        return items[(int)n.NumVal.ToInt().num].Copy();
    }

    private static void AddLibraryBuiltins(LEnv e) {
        LVal plus = e.Get("+"), minus = e.Get("-"), times = e.Get("*"), eq = e.Get("eq"), lt = e.Get("<"), gt = e.Get(">");
        LVal zero = LVal.Number(BigInteger.Zero), one = LVal.Number(BigInteger.One);

        // logic, comparison and arithmetic
        AddBuiltinEvaluated(e, "not",  (e, a) => LVal.Bool(a[0].IsNIL));
        AddBuiltinEvaluated(e, "==",   (e, a) => Apply(e, eq, a[0], a[1]));
        AddBuiltinEvaluated(e, ">=",   (e, a) => Not(Apply(e, lt, a[0], a[1])));
        AddBuiltinEvaluated(e, "<=",   (e, a) => Not(Apply(e, gt, a[0], a[1])));
        AddBuiltinEvaluated(e, "neg?", (e, a) => Apply(e, lt, a[0], zero));
        AddBuiltinEvaluated(e, "pos?", (e, a) => Apply(e, lt, zero, a[0]));
        AddBuiltinEvaluated(e, "zero?", (e, a) => Apply(e, eq, zero, a[0]));
        AddBuiltinEvaluated(e, "one?", (e, a) => Apply(e, eq, one, a[0]));
        AddBuiltinEvaluated(e, "1+",   (e, a) => Apply(e, plus, a[0], one));
        AddBuiltinEvaluated(e, "1-",   (e, a) => Apply(e, minus, a[0], one));
        AddBuiltinEvaluated(e, "abs",  (e, a) => {
            if (!a[0].IsNum) return LVal.Err("Cannot perform 'abs' on non-number");
            var neg = Apply(e, lt, a[0], zero);
            return neg.IsErr ? neg : neg.IsNIL ? a[0] : Apply(e, minus, a[0]);
        });

        // lists: an item as it's written
        AddBuiltinEvaluated(e, "cons", (e, a) => {
            var items = Items(a[1]);
            if (items == null) return LVal.Err("'cons' expects a list second");
            var q = LVal.Qexpr();
            q.Add(a[0]);
            foreach (var c in items) q.Add(c.Copy());
            return q;
        });
        AddBuiltinEvaluated(e, "fst",  (e, a) => Nth(zero, a[0], "fst"));
        AddBuiltinEvaluated(e, "snd",  (e, a) => Nth(one, a[0], "snd"));
        AddBuiltinEvaluated(e, "thd",  (e, a) => Nth(LVal.Number(new BigInteger(2)), a[0], "thd"));
        AddBuiltinEvaluated(e, "nth",  (e, a) => Nth(a[0], a[1], "nth"));
        AddBuiltinEvaluated(e, "last", (e, a) => {
            var items = Items(a[0]);
            if (items == null) return LVal.Err("'last' expects a list");
            return Nth(LVal.Number(new BigInteger(items.Count - 1)), a[0], "last");
        });
        // take, drop: n of them (n counted down to 0: n not a whole number, or negative, is all of them)
        AddBuiltinEvaluated(e, "take", (e, a) => {
            var items = Items(a[1]);
            if (items == null) return LVal.Err("'take' expects a list");
            var q = LVal.Qexpr();
            if (items.Count == 0) return q;
            if (!a[0].IsNum) return LVal.Err("'take' expects a Number and a list");
            var k = a[0].NumVal!;
            foreach (var c in items) {
                if (k.CompareTo(Num.Zero) == 0) break;
                q.Add(c.Copy());
                k = k - BigInteger.One;
            }
            return q;
        });
        AddBuiltinEvaluated(e, "drop", (e, a) => {
            var items = Items(a[1]);
            if (items == null) return LVal.Err("'drop' expects a list");
            if (items.Count == 0) return a[1];
            if (!a[0].IsNum) return LVal.Err("'drop' expects a Number and a list");
            var k = a[0].NumVal!;
            int i = 0;
            while (i < items.Count && k.CompareTo(Num.Zero) != 0) {
                i++;
                k = k - BigInteger.One;
            }
            var q = LVal.Qexpr();
            for (; i < items.Count; i++) q.Add(items[i].Copy());
            return q;
        });
        LVal Elem(LVal x, LVal l, string fn) {
            var items = Items(l);
            if (items == null) return LVal.Err($"'{fn}' expects a list");
            return LVal.Bool(items.Any(c => x.Equals(c)));
        }
        AddBuiltinEvaluated(e, "elem?", (e, a) => Elem(a[0], a[1], "elem?"));
        AddBuiltinEvaluated(e, "in?",   (e, a) => Elem(a[0], a[1], "in?"));

        // f over a list's items: each applied in turn (the first error, the value)
        AddBuiltinEvaluated(e, "map", (e, a) => {
            var items = Items(a[1]);
            if (items == null) return LVal.Err("'map' expects a function and a list");
            var q = LVal.Qexpr();
            foreach (var c in items) {
                var r = Apply(e, a[0], c.Copy());
                if (r.IsErr) return r;
                q.Add(r);
            }
            return q;
        });
        AddBuiltinEvaluated(e, "filter", (e, a) => {
            var items = Items(a[1]);
            if (items == null) return LVal.Err("'filter' expects a function and a list");
            var q = LVal.Qexpr();
            foreach (var c in items) {
                var r = Apply(e, a[0], c.Copy());
                if (r.IsErr) return r;
                if (!r.IsNIL) q.Add(c.Copy());
            }
            return q;
        });
        AddBuiltinEvaluated(e, "foldl", (e, a) => {
            var items = Items(a[2]);
            if (items == null) return LVal.Err("'foldl' expects a function, a value and a list");
            var acc = a[1];
            foreach (var c in items) {
                acc = Apply(e, a[0], acc, c.Copy());
                if (acc.IsErr) return acc;
            }
            return acc;
        });
        AddBuiltinEvaluated(e, "foldr", (e, a) => {
            var items = Items(a[2]);
            if (items == null) return LVal.Err("'foldr' expects a function, a value and a list");
            var acc = a[1];
            for (int i = items.Count - 1; i >= 0; i--) {
                acc = Apply(e, a[0], items[i].Copy(), acc);
                if (acc.IsErr) return acc;
            }
            return acc;
        });
        // any? (T at the first item f says T of), all? (NIL at the first it says NIL of), find (that item), count
        LVal Test(LVal f, LVal l, string fn, Func<LVal, LVal, LVal?> step, Func<LVal> end) {
            var items = Items(l);
            if (items == null) return LVal.Err($"'{fn}' expects a function and a list");
            foreach (var c in items) {
                var r = Apply(e, f, c.Copy());
                if (r.IsErr) return r;
                var v = step(c, r);
                if (v != null) return v;
            }
            return end();
        }
        AddBuiltinEvaluated(e, "any?",  (e, a) => Test(a[0], a[1], "any?", (c, r) => r.IsNIL ? null : LVal.Bool(true), LVal.NIL));
        AddBuiltinEvaluated(e, "all?",  (e, a) => Test(a[0], a[1], "all?", (c, r) => r.IsNIL ? LVal.NIL() : null, () => LVal.Bool(true)));
        AddBuiltinEvaluated(e, "find",  (e, a) => Test(a[0], a[1], "find", (c, r) => r.IsNIL ? null : c.Copy(), LVal.NIL));
        AddBuiltinEvaluated(e, "count", (e, a) => {
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
                acc = Apply(e, f, acc, c.Copy());
                if (acc.IsErr) return acc;
            }
            return acc;
        }
        AddBuiltinEvaluated(e, "sum",     (e, a) => Fold(plus, zero, a[0], "sum"));
        AddBuiltinEvaluated(e, "product", (e, a) => Fold(times, one, a[0], "product"));
        LVal Least(LVal by, LVal a) {
            var acc = a[0];
            for (int i = 1; i < a.Count; i++) {
                var r = Apply(e, by, acc, a[i]);
                if (r.IsErr) return r;
                if (r.IsNIL) acc = a[i];
            }
            return acc;
        }
        AddBuiltinEvaluated(e, "min", (e, a) => Least(lt, a));
        AddBuiltinEvaluated(e, "max", (e, a) => Least(gt, a));
    }
}
