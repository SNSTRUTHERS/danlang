// Compiler.cs - code compiled once into nodes, each list's kept with it (code isn't changed: it's frozen), and run:
// an S-expression's value, a Q-expression run as code, a function's body in tail position.  What the nodes do is
// what the evaluator's rules say (reference.md, section 3), as LVal's EvalSExpr did item by item: a call's first item
// evaluated, then, as it's a built-in, a special form, a function or a hash, its arguments.  A name remembers where
// its global binding is (a slot); a few special forms (if, do, and, or, set!, set, def, while) are run here when the
// first item is that built-in (whatever name it's called by), and the most used built-ins take their values directly
// (FunInfo.Fast1 and Fast2: NIL from them, a case they leave to the built-in itself)
public partial class LVal {
    // A list's compiled code: the call its items make
    internal sealed class CodeInfo {
        public CallNode? Call;
    }

    // This list's items as a call, compiled (once: it's kept with the list, and with every copy of it ScopedIn makes)
    internal CallNode CallOf() {
        if (_x is not CodeInfo info) {
            Freeze();
            _x = info = new CodeInfo();
        }
        return info.Call ??= new CallNode(this);
    }

    internal abstract class Node {
        public abstract LVal Eval(LEnv e);
        // In tail position: a call of a function comes back as a TAIL value, for Apply to run in the caller's place
        public virtual LVal Tail(LEnv e) => Eval(e);
    }

    // An expression's node: a name's, a call's, a Q-expression's (a copy remembering the scope), a value's (itself)
    internal static Node NodeOf(LVal x) => x.ValType switch {
        LE.SYM   => new SymNode(x.SymName),
        LE.SEXPR => x.CallOf(),
        LE.QEXPR => x.Count == 0 || x.Scope != null ? new ConstNode(x) : new QuoteNode(x),
        _        => new ConstNode(x),
    };

    internal sealed class ConstNode : Node {
        private readonly LVal _v;
        public ConstNode(LVal v) => _v = v;
        public override LVal Eval(LEnv e) => _v;
    }

    internal sealed class QuoteNode : Node {
        private readonly LVal _q;
        public QuoteNode(LVal q) => _q = q;
        public override LVal Eval(LEnv e) => _q.ScopedIn(e);
    }

    // A name: its value from the innermost scope out; found in the global scope, its slot there is remembered
    internal sealed class SymNode : Node {
        private readonly Name _name;
        private LEnv? _root;
        private LEnv.Slot? _slot;
        public SymNode(Name name) => _name = name;
        public override LVal Eval(LEnv e) {
            for (var s = e; ; s = s.Parent!) {
                if (s.Parent == null) {
                    if (ReferenceEquals(s, _root)) return _slot!.Value;
                    var slot = s.SlotOf(_name);
                    if (slot != null) {
                        _root = s;
                        _slot = slot;
                        return slot.Value;
                    }
                }
                if (s.TryGetLocal(_name, out var v)) return v;
                if (s.IsCall && ReferenceEquals(_name, Name.Rest)) return NIL();
                if (s.Parent == null) return Err($"Unbound Symbol '{_name.Text}'");
            }
        }
    }

    // The special forms run here: their built-ins (Builtins.AddBuiltins tells them)
    internal static Func<LEnv, LVal, LVal>? IfFn, DoFn, AndFn, OrFn, SetBangFn, SetFn, DefFn, WhileFn;

    // An expression in tail position, as a TailStep has it (run: a list's items as an S-expression)
    internal static LVal TailOf(LEnv e, LVal x, bool run) =>
        x.ValType == LE.SEXPR || (run && x.ValType == LE.QEXPR) ? x.CallOf().Tail(e) : x.Eval(e);

    // A call: a list's items, the first the function
    internal sealed class CallNode : Node {
        private readonly List<LVal> _cells;
        private readonly Node?[] _nodes;      // each item's, as it's needed

        public CallNode(LVal code) {
            _cells = code.Cells ?? new List<LVal>();
            _nodes = new Node?[_cells.Count];
        }

        private Node NodeAt(int k) => _nodes[k] ??= NodeOf(_cells[k]);

        public override LVal Eval(LEnv e) => Do(e, false);
        public override LVal Tail(LEnv e) => Do(e, true);

        private LVal Do(LEnv e, bool tail) {
            if (_cells.Count == 0) return NIL();
            var f = NodeAt(0).Eval(e);
            if (f.ValType == LE.FUN) return Invoke(e, f, 1, tail);
            if (f.IsErr) return f;
            if (f.IsExit && _cells.Count > 1) return ExitWith(e, _cells[1]);
            if (_cells.Count == 1) return f;
            if (f.IsHash) {
                int start = 1;
                f = HashAt(e, f, _cells, ref start);
                if (!f.IsFun) return f;
                return Invoke(e, f, start, tail);
            }
            return Err($"S-Expression starts with incorrect type. Got {LEName(f.ValType)}, Expected {LEName(LE.FUN)}.");
        }

        // f called with the items from start on as its arguments
        private LVal Invoke(LEnv e, LVal f, int start, bool tail) {
            var fi = f.Fn!;
            int n = _cells.Count - start;
            if (fi.BuiltinVal != null) {
                if (fi.IsSpecial) return Special(e, f, fi, start, n, tail);

                // An ordinary built-in: its arguments' values (the first error is the call's value, but for one that
                // takes errors as values), then its fast way, if it has one for them, or its own
                int total = (fi.Bound?.Count ?? 0) + n;
                if (total > fi.MaxArgs) return TooMany(f, total);
                if (n <= 2 && fi.Bound == null) {
                    LVal? a0 = null, a1 = null;
                    if (n > 0) {
                        a0 = NodeAt(start).Eval(e);
                        if (a0.IsErr && !fi.TakesErrors) return a0;
                    }
                    if (n > 1) {
                        a1 = NodeAt(start + 1).Eval(e);
                        if (a1.IsErr && !fi.TakesErrors) return a1;
                    }
                    if (n >= fi.MinArgs) {
                        LVal? r = null;
                        if (n == 2 && fi.Fast2 != null) r = Fast(fi.Fast2, a0!, a1!);
                        else if (n == 1 && fi.Fast1 != null) r = Fast(fi.Fast1, a0!);
                        if (r != null) return r;
                    }
                    var two = new List<LVal>(n);
                    if (n > 0) two.Add(a0!);
                    if (n > 1) two.Add(a1!);
                    return ValueArgs(f, two, out var a2) ?? CallBuiltin(e, f, a2);
                }
                var vals = new List<LVal>(n);
                for (int k = start; k < _cells.Count; k++) {
                    var v = NodeAt(k).Eval(e);
                    if (v.IsErr && !fi.TakesErrors) return v;
                    vals.Add(v);
                }
                return ValueArgs(f, vals, out var a) ?? CallBuiltin(e, f, a);
            }

            // A function: its arguments' values (the first error stops them), or, for an fexpr, as they're written
            var args = new List<LVal>(n);
            for (int k = start; k < _cells.Count; k++) {
                if (fi.IsFexpr) {
                    args.Add(_cells[k].ScopedIn(e));
                    continue;
                }
                var v = NodeAt(k).Eval(e);
                if (v.IsErr) return v;
                args.Add(v);
            }
            if (tail) return new LVal { ValType = LE.TAIL, TailFn = f, TailArgs = args };
            return Apply(f, args);
        }

        // A built-in's fast way (an exception it throws: an error, as CallBuiltin has it)
        private static LVal? Fast(Func<LVal, LVal, LVal?> f, LVal x, LVal y) {
            try { return f(x, y); }
            catch (ExitException) { throw; }
            catch (Exception ex) { return Err(Builtins.FromHost(ex.Message)); }
        }
        private static LVal? Fast(Func<LVal, LVal?> f, LVal x) {
            try { return f(x); }
            catch (ExitException) { throw; }
            catch (Exception ex) { return Err(Builtins.FromHost(ex.Message)); }
        }

        // An item run as code, as the loops' EvalArg does: a Q-expression's items (in the scope it remembers, or e),
        // anything else evaluated
        private LVal RunItem(LEnv e, int k) {
            var x = _cells[k];
            if (x.ValType != LE.QEXPR) return NodeAt(k).Eval(e);
            return x.Count == 0 ? NIL() : x.CallOf().Eval(x.Scope ?? e);
        }

        // A special form: its items as they're written
        private LVal Special(LEnv e, LVal f, FunInfo fi, int start, int n, bool tail) {
            if (n > fi.MaxArgs) return TooMany(f, n);
            var b = fi.BuiltinVal;
            if (ReferenceEquals(b, IfFn) && n >= 2) {
                var t = NodeAt(start).Eval(e);
                if (t.IsErr) return t;
                if (!t.IsNIL) return tail ? NodeAt(start + 1).Tail(e) : NodeAt(start + 1).Eval(e);
                if (n == 2) return NIL();
                return tail ? NodeAt(start + 2).Tail(e) : NodeAt(start + 2).Eval(e);
            }
            if (ReferenceEquals(b, DoFn)) {
                if (n == 0) return NIL();
                int last = _cells.Count - 1;
                for (int k = start; k < last; k++) {
                    var v = NodeAt(k).Eval(e);
                    if (v.IsErr) return v;
                }
                return tail ? NodeAt(last).Tail(e) : NodeAt(last).Eval(e);
            }
            if (ReferenceEquals(b, AndFn)) {
                LVal last = T();
                for (int k = start; k < _cells.Count; k++) {
                    last = NodeAt(k).Eval(e);
                    if (last.IsErr || last.IsNIL) return last;
                }
                return last;
            }
            if (ReferenceEquals(b, OrFn)) {
                for (int k = start; k < _cells.Count; k++) {
                    var v = NodeAt(k).Eval(e);
                    if (v.IsErr || !v.IsNIL) return v;
                }
                return NIL();
            }
            if (n >= 1 && (ReferenceEquals(b, SetBangFn) || ReferenceEquals(b, SetFn) || ReferenceEquals(b, DefFn))) {
                var func = ReferenceEquals(b, SetBangFn) ? "set!" : ReferenceEquals(b, SetFn) ? "set" : "def";
                var syms = _cells[start];
                if (syms.ValType == LE.SYM && n == 2) return Builtins.Bind(e, func, syms.SymName, NodeAt(start + 1).Eval(e));
                if (syms.ValType == LE.QEXPR && syms.Count == n - 1 && syms.Cells!.TrueForAll(s => s.ValType == LE.SYM)) {
                    for (int i = 0; i < syms.Count; i++) {
                        var r = Builtins.Bind(e, func, syms.Cells![i].SymName, NodeAt(start + 1 + i).Eval(e));
                        if (r.IsErr) return r;
                    }
                    return NIL();
                }
            }
            if (ReferenceEquals(b, WhileFn) && n >= 1) {
                LVal result = NIL();
                while (true) {
                    var intr = CheckInterrupt();
                    if (intr != null) return intr;
                    var t = RunItem(e, start);
                    if (t.IsErr) return t;
                    if (t.IsNIL) return result;
                    for (int k = start + 1; k < _cells.Count; k++) {
                        result = RunItem(e, k);
                        if (result.IsErr) return result;
                    }
                }
            }

            // Any other: the built-in itself, given the items (in tail position, its tail form)
            var a = Sexpr();
            for (int k = start; k < _cells.Count; k++) a.Cells!.Add(_cells[k]);
            if (tail && fi.TailForm != null) {
                TailStep step;
                try {
                    step = fi.TailForm(e, a);
                }
                catch (ExitException) { throw; }
                catch (Exception ex) {
                    return Err(Builtins.FromHost(ex.Message));
                }
                if (step.Expr == null) return step.Value!;
                return TailOf(step.Env!, step.Expr, step.Run);
            }
            return CallBuiltin(e, f, a);
        }
    }
}
