// Compiler.cs - code compiled once into nodes, each list's kept with it (code isn't changed: it's frozen), and run:
// an S-expression's value, a Q-expression run as code, a function's body in tail position.  What the nodes do is
// what the evaluator's rules say (reference.md, section 3), as LVal's EvalSExpr did item by item: a call's first item
// evaluated, then, as it's a built-in, a special form, a function or a hash, its arguments.  A name remembers where
// its global binding is (a slot); a few special forms (if, do, and, or, set!, set, def, while) are run here when the
// first item is that built-in (whatever name it's called by), and the most used built-ins take their values directly
// (FunInfo.Fast1 and Fast2: NIL from them, a case they leave to the built-in itself)
public partial class LVal {
    // A list's compiled code: the call its items make
    public sealed class CodeInfo {
        public string? File;    // where it was read (load's file, or none), and its line there, and the code
        public int Line;
        public LVal? Code;
        internal CallNode? Call;
        // Its place, as a trace shows it: file:line, and the code's start
        public string Place() {
            var code = Code?.ToStr() ?? "";
            if (code.Length > 60) code = code.Substring(0, 57) + "...";
            return (File != null ? File + ":" + Line : Line > 0 ? "line " + Line : "(made, not read)") + "  " + code;
        }
        internal Uses? Uses;    // the names a function's body has (&_ or &1 ..., &0), anywhere in it
    }

    // What a function's body names, anywhere in it: its extra arguments (&_, &1 ...), the hash it's a method of (&0)
    [Flags]
    internal enum Uses { None = 0, Extras = 1, Self = 2 }

    internal bool Mentions(Uses u) {
        if (_x is not CodeInfo info) {
            Freeze();
            _x = info = new CodeInfo();
        }
        info.Uses ??= UsesOf(this);
        return (info.Uses.Value & u) != 0;
    }

    private static Uses UsesOf(LVal v) {
        if (v.ValType == LE.SYM) {
            var s = v.SymVal;
            var u0 = s == "&0" ? Uses.Self : Uses.None;      // (&0 counts as an extra's name too, as it always has)
            if (s == "&_" || (s.Length > 1 && s[0] == '&' && s.Skip(1).All(char.IsDigit))) return u0 | Uses.Extras;
            return u0;
        }
        var u = Uses.None;
        if (v.Cells != null) foreach (var c in v.Cells) u |= UsesOf(c);
        return u;
    }

    // This list's items as a call, compiled (once: it's kept with the list, and with every copy of it ScopedIn makes)
    internal CallNode CallOf() {
        if (_x is not CodeInfo info) {
            Freeze();
            _x = info = new CodeInfo();
        }
        info.Code ??= this;
        return info.Call ??= new CallNode(this, info);
    }

    // Where evaluation is: the call being made (an error made now was made there), and the calls of functions it's in
    // (a stack, the outermost first), for an error's trace
    internal static CodeInfo? Site;
    private static readonly CodeInfo?[] _calls = new CodeInfo?[MaxDepth + 2];
    private static int _ncalls;
    // (the profiler's look: the call being made, and the innermost function's call)
    internal static (CodeInfo? site, CodeInfo? call) Now() {
        int n = _ncalls;
        return (Site, n > 0 && n <= _calls.Length ? _calls[n - 1] : null);
    }
    private static CodeInfo[]? CallsNow() {
        if (_ncalls == 0) return null;
        int n = Math.Min(_ncalls, 12);
        var a = new CodeInfo[n];
        for (int i = 0; i < n; i++) a[i] = _calls[_ncalls - 1 - i]!;
        return a;
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

    // A name: its value from the innermost scope out; found in the global scope, its slot there is remembered (and,
    // the name bound in no other scope, ever, its value had from it at once)
    internal sealed class SymNode : Node {
        private readonly Name _name;
        private LEnv? _root;
        private LEnv.Slot? _slot;
        public SymNode(Name name) => _name = name;
        public Name Name => _name;
        // Whether its value, from e, is a global's (the name bound in no other scope): its global scope and slot
        public bool Global(LEnv e, out LEnv root, out LEnv.Slot slot) {
            root = _root!;
            slot = _slot!;
            return _root != null && !_name.Local && ReferenceEquals(e.Root, _root);
        }
        public override LVal Eval(LEnv e) {
            if (_root != null && !_name.Local && ReferenceEquals(e.Root, _root)) return _slot!.Value;
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

    // A tail call: the function and its arguments, for the Apply it goes back to (one value, used again: each goes
    // straight back to its Apply, which takes them out before another is made)
    [ThreadStatic] private static LVal? _tail;
    internal static LVal TailCall(LVal f, List<LVal> args) {
        var t = _tail ??= new LVal { ValType = LE.TAIL };
        t.TailFn = f;
        t.TailArgs = args;
        return t;
    }

    // An expression in tail position, as a TailStep has it (run: a list's items as an S-expression)
    internal static LVal TailOf(LEnv e, LVal x, bool run) =>
        x.ValType == LE.SEXPR || (run && x.ValType == LE.QEXPR) ? x.CallOf().Tail(e) : x.Eval(e);

    // A call: a list's items, the first the function
    internal sealed class CallNode : Node {
        private readonly List<LVal> _cells;
        private readonly Node?[] _nodes;      // each item's, as it's needed
        private readonly CodeInfo _info;      // (its place, for an error's trace)
        private readonly int _n;              // (its items: how many)

        public CallNode(LVal code, CodeInfo info) {
            _cells = code.Cells ?? new List<LVal>();
            _n = _cells.Count;
            _nodes = new Node?[_n];
            _info = info;
        }

        private Node NodeAt(int k) => _nodes[k] ??= NodeOf(_cells[k]);

        public override LVal Eval(LEnv e) => Do(e, false);
        public override LVal Tail(LEnv e) => Do(e, true);

        // The call's plan: its first item's value as it was last, when that was a global's (the name bound in no
        // other scope, ever: Name.Local), and the way the call went with it (a special form run here, a built-in's
        // fast way for so many arguments, a function's call, a hash's, a buffer's); while the global still has that
        // value, the call goes that way at once
        private enum Plan : byte { None, If, Do, And, Or, Set, While, Special, B1, B2, B3, Builtin, Fun, Hash, Buffer }
        private Plan _plan;
        private Name? _hname;
        private LEnv? _hroot;
        private LEnv.Slot? _hslot;
        private LVal? _hval;

        private LVal Do(LEnv e, bool tail) {
            Site = _info;
            if (_plan != Plan.None) {
                var f = _hslot!.Value;
                if (ReferenceEquals(f, _hval) && !_hname!.Local && ReferenceEquals(e.Root, _hroot)) {
                    switch (_plan) {
                        case Plan.If: return If(e, 1, _n - 1, tail);
                        case Plan.Do: return DoForm(e, 1, tail);
                        case Plan.And: return And(e, 1);
                        case Plan.Or: return Or(e, 1);
                        case Plan.Set: return SetForm(e, f.Fn!.BuiltinVal!, 1, Names(1, _n - 1));
                        case Plan.While: return While(e, 1);
                        case Plan.Special: return Special(e, f, f.Fn!, 1, _n - 1, tail);
                        case Plan.B1: return Builtin1(e, f);
                        case Plan.B2: return Builtin2(e, f);
                        case Plan.B3: return Builtin3(e, f);
                        case Plan.Builtin: return Invoke(e, f, 1, tail);
                        case Plan.Fun: return CallFun(e, f, f.Fn!, 1, tail);
                        case Plan.Hash: return HashCall(e, f, tail);
                        case Plan.Buffer: return BufferCall(e, f);
                    }
                }
            }
            if (_n == 0) return NIL();
            var g = NodeAt(0).Eval(e);
            Learn(e, g);
            if (g.ValType == LE.FUN) return Invoke(e, g, 1, tail);
            if (g.IsErr) return g;
            if (g.IsExit && _n > 1) return ExitWith(e, _cells[1]);
            if (_n == 1) return g;
            if (g.IsHash) return HashCall(e, g, tail);
            if (g.IsBuffer) return BufferCall(e, g);
            return Err($"S-Expression starts with incorrect type. Got {LEName(g.ValType)}, Expected {LEName(LE.FUN)}.");
        }

        // The plan for the first item's value f (none: its name isn't a global's, or bound in another scope too)
        private void Learn(LEnv e, LVal f) {
            _plan = Plan.None;
            if (_nodes[0] is not SymNode s || !s.Global(e, out var root, out var slot) || !ReferenceEquals(slot.Value, f)) return;
            var p = PlanFor(f);
            if (p == Plan.None) return;
            (_hname, _hroot, _hslot, _hval, _plan) = (s.Name, root, slot, f, p);
        }

        private Plan PlanFor(LVal f) {
            int n = _n - 1;
            if (f.ValType == LE.HASH) return n >= 1 ? Plan.Hash : Plan.None;
            if (f.ValType == LE.BUFFER) return n == 1 ? Plan.Buffer : Plan.None;
            if (f.ValType != LE.FUN) return Plan.None;
            var fi = f.Fn!;
            var b = fi.BuiltinVal;
            if (b == null) return fi.IsFexpr ? Plan.None : Plan.Fun;
            if (fi.IsSpecial) {
                if (n > fi.MaxArgs) return Plan.None;
                if (ReferenceEquals(b, IfFn)) return n >= 2 ? Plan.If : Plan.Special;
                if (ReferenceEquals(b, DoFn)) return Plan.Do;
                if (ReferenceEquals(b, AndFn)) return Plan.And;
                if (ReferenceEquals(b, OrFn)) return Plan.Or;
                if (ReferenceEquals(b, SetBangFn) || ReferenceEquals(b, SetFn) || ReferenceEquals(b, DefFn))
                    return n >= 1 && Names(1, n).Length > 0 ? Plan.Set : Plan.Special;
                if (ReferenceEquals(b, WhileFn)) return n >= 1 ? Plan.While : Plan.Special;
                return Plan.Special;
            }
            if (fi.Bound != null || n < fi.MinArgs || n > fi.MaxArgs) return Plan.None;
            return n == 1 && fi.Fast1 != null ? Plan.B1 : n == 2 && fi.Fast2 != null ? Plan.B2
                : n == 3 && fi.Fast3 != null ? Plan.B3 : Plan.Builtin;
        }

        // A hash called, (h key arg...): the value at key; a function there, a method, given the rest
        private LVal HashCall(LEnv e, LVal f, bool tail) {
            var key = NodeAt(1).Eval(e);
            if (key.IsErr) return key;
            var value = f.HashValue!.Get(key);
            if (value.IsErr) return value;
            if (value.IsFun) return Invoke(e, Method(value, f), 2, tail);
            if (_n > 2) return Err($"{key.ToStr()} isn't a method: its value isn't a function");
            return value;
        }

        // A buffer called, (b i): its byte i
        private LVal BufferCall(LEnv e, LVal f) {
            if (_n > 2) return Err("A buffer is called with an index: (b i)");
            var i = NodeAt(1).Eval(e);
            if (i.IsErr) return i;
            var bytes = f.BufferValue!;
            if (i.IsSmallInt(out var k) && k >= 0 && k < bytes.Length) return Number(bytes[(int)k]);
            return Builtins.BufferGet(f, i);
        }

        // A built-in of 1, 2 or 3 arguments with a fast way for them (none of them bound already, as many as it
        // takes): their values (the first error is the call's, but for a built-in that takes errors), its fast way,
        // or the rest of the ways (Rest)
        private LVal Builtin1(LEnv e, LVal f) {
            var fi = f.Fn!;
            var a0 = NodeAt(1).Eval(e);
            if (a0.IsErr && !fi.TakesErrors) return a0;
            Site = _info;
            return Fast(fi.Fast1!, a0) ?? Rest(e, f, fi, 1, a0, null, null);
        }
        private LVal Builtin2(LEnv e, LVal f) {
            var fi = f.Fn!;
            var a0 = NodeAt(1).Eval(e);
            if (a0.IsErr && !fi.TakesErrors) return a0;
            var a1 = NodeAt(2).Eval(e);
            if (a1.IsErr && !fi.TakesErrors) return a1;
            Site = _info;
            return Fast(fi.Fast2!, a0, a1) ?? Rest(e, f, fi, 2, a0, a1, null);
        }
        private LVal Builtin3(LEnv e, LVal f) {
            var fi = f.Fn!;
            var a0 = NodeAt(1).Eval(e);
            if (a0.IsErr && !fi.TakesErrors) return a0;
            var a1 = NodeAt(2).Eval(e);
            if (a1.IsErr && !fi.TakesErrors) return a1;
            var a2 = NodeAt(3).Eval(e);
            if (a2.IsErr && !fi.TakesErrors) return a2;
            Site = _info;
            return Fast(fi.Fast3!, a0, a1, a2) ?? Rest(e, f, fi, 3, a0, a1, a2);
        }

        // A built-in's values, n of them (3 at most), when its fast way for them didn't do it: its fast way for any
        // number, or its own
        private static LVal Rest(LEnv e, LVal f, FunInfo fi, int n, LVal? a0, LVal? a1, LVal? a2) {
            if (fi.FastN != null) {
                var arr = new LVal[n];
                if (n > 0) arr[0] = a0!;
                if (n > 1) arr[1] = a1!;
                if (n > 2) arr[2] = a2!;
                var r = FastAny(fi.FastN, arr);
                if (r != null) return r;
            }
            var vals = new List<LVal>(n);
            if (n > 0) vals.Add(a0!);
            if (n > 1) vals.Add(a1!);
            if (n > 2) vals.Add(a2!);
            return ValueArgs(f, vals, out var a) ?? CallBuiltin(e, f, a);
        }

        // f called with the items from start on as its arguments
        private LVal Invoke(LEnv e, LVal f, int start, bool tail) {
            var fi = f.Fn!;
            int n = _n - start;
            if (fi.BuiltinVal == null) return CallFun(e, f, fi, start, tail);
            if (fi.IsSpecial) return Special(e, f, fi, start, n, tail);

            // An ordinary built-in: its arguments' values (the first error is the call's value, but for one that
            // takes errors as values), then its fast way, if it has one for them, or its own
            int total = (fi.Bound?.Count ?? 0) + n;
            if (total > fi.MaxArgs) return TooMany(f, total);
            if (n > 3 && fi.Bound == null && fi.FastN != null) {
                var arr = new LVal[n];
                for (int k = 0; k < n; k++) {
                    var v = NodeAt(start + k).Eval(e);
                    if (v.IsErr && !fi.TakesErrors) return v;
                    arr[k] = v;
                }
                Site = _info;
                if (n >= fi.MinArgs) {
                    var r = FastAny(fi.FastN, arr);
                    if (r != null) return r;
                }
                return ValueArgs(f, new List<LVal>(arr), out var an) ?? CallBuiltin(e, f, an);
            }
            if (n <= 3 && fi.Bound == null) {
                LVal? a0 = null, a1 = null, a2 = null;
                if (n > 0) {
                    a0 = NodeAt(start).Eval(e);
                    if (a0.IsErr && !fi.TakesErrors) return a0;
                }
                if (n > 1) {
                    a1 = NodeAt(start + 1).Eval(e);
                    if (a1.IsErr && !fi.TakesErrors) return a1;
                }
                if (n > 2) {
                    a2 = NodeAt(start + 2).Eval(e);
                    if (a2.IsErr && !fi.TakesErrors) return a2;
                }
                Site = _info;
                if (n >= fi.MinArgs) {
                    LVal? r = null;
                    if (n == 2 && fi.Fast2 != null) r = Fast(fi.Fast2, a0!, a1!);
                    else if (n == 3 && fi.Fast3 != null) r = Fast(fi.Fast3, a0!, a1!, a2!);
                    else if (n == 1 && fi.Fast1 != null) r = Fast(fi.Fast1, a0!);
                    if (r != null) return r;
                    return Rest(e, f, fi, n, a0, a1, a2);
                }
                var two = new List<LVal>(n);
                if (n > 0) two.Add(a0!);
                if (n > 1) two.Add(a1!);
                if (n > 2) two.Add(a2!);
                return ValueArgs(f, two, out var a3) ?? CallBuiltin(e, f, a3);
            }
            var vals = new List<LVal>(n);
            for (int k = start; k < _n; k++) {
                var v = NodeAt(k).Eval(e);
                if (v.IsErr && !fi.TakesErrors) return v;
                vals.Add(v);
            }
            Site = _info;
            return ValueArgs(f, vals, out var a) ?? CallBuiltin(e, f, a);
        }

        // A function (not a built-in) called: its arguments' values (the first error stops them), or, for an fexpr,
        // as they're written; in tail position, a TAIL value for the Apply it goes back to
        private LVal CallFun(LEnv e, LVal f, FunInfo fi, int start, bool tail) {
            var args = _n == start ? NoArgs : new List<LVal>(_n - start);
            for (int k = start; k < _n; k++) {
                if (fi.IsFexpr) {
                    args.Add(_cells[k].ScopedIn(e));
                    continue;
                }
                var v = NodeAt(k).Eval(e);
                if (v.IsErr) return v;
                args.Add(v);
            }
            if (tail) return TailCall(f, args);
            _calls[_ncalls++] = _info;
            try {
                return Apply(f, args, !fi.IsFexpr);
            }
            finally {
                _ncalls--;
            }
        }

        // A built-in's fast way (an exception it throws: an error, as CallBuiltin has it)
        private static LVal? Fast(Func<LVal, LVal, LVal?> f, LVal x, LVal y) {
            try { return f(x, y); }
            catch (ExitException) { throw; }
            catch (Exception ex) { return Err(Builtins.FromHost(ex.Message)); }
        }
        private static LVal? Fast(Func<LVal, LVal, LVal, LVal?> f, LVal x, LVal y, LVal z) {
            try { return f(x, y, z); }
            catch (ExitException) { throw; }
            catch (Exception ex) { return Err(Builtins.FromHost(ex.Message)); }
        }
        private static LVal? FastAny(Func<LVal[], LVal?> f, LVal[] x) {
            try { return f(x); }
            catch (ExitException) { throw; }
            catch (Exception ex) { return Err(Builtins.FromHost(ex.Message)); }
        }
        private static readonly List<LVal> NoArgs = new(0);     // (a call of none's: never changed)
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

        private Name[]? _names;               // (def's, set's, set!'s names: none, the built-in's way)
        private int _namesStart;

        // The names a def, set or set! of the items from start on binds, worked out once for this call, as its items
        // are its own: a name, or a list of names as many as the values (none: anything else, the built-in's way)
        private Name[] Names(int start, int n) {
            if (_names == null || _namesStart != start) {
                _namesStart = start;
                var syms = _cells[start];
                _names = syms.ValType == LE.SYM && n == 2 ? new[] { syms.SymName }
                    : syms.ValType == LE.QEXPR && syms.Count == n - 1 && syms.Cells!.TrueForAll(s => s.ValType == LE.SYM)
                        ? syms.Cells!.Select(s => s.SymName).ToArray() : Array.Empty<Name>();
            }
            return _names;
        }

        // The special form this call was last (its built-in: one Compiler.cs runs, which, 0 none)
        private Func<LEnv, LVal, LVal>? _special;
        private int _kind;

        // set! of one name (the call's i'th): its nearest binding changed; a global one's slot remembered (and, the
        // name bound in no other scope, changed at once)
        private LEnv? _setRoot;
        private LEnv.Slot?[]? _setSlots;
        private LVal SetBang(LEnv e, Name k, int i, LVal v) {
            v.Freeze();
            if (!k.Local && ReferenceEquals(e.Root, _setRoot) && _setSlots![i] is LEnv.Slot known) {
                known.Value = v;
                return NIL();
            }
            for (var s = e; ; s = s.Parent!) {
                if (s.Parent == null) {
                    if (ReferenceEquals(s, _setRoot) && _setSlots![i] is LEnv.Slot cached) {
                        cached.Value = v;
                        return NIL();
                    }
                    var slot = s.SlotOf(k);
                    if (slot != null) {
                        if (!ReferenceEquals(s, _setRoot)) {
                            _setRoot = s;
                            _setSlots = new LEnv.Slot?[_n];
                        }
                        _setSlots![i] = slot;
                        slot.Value = v;
                        return NIL();
                    }
                    if (s.SetIfHere(k, v)) return NIL();
                    return Err($"Unbound Symbol '{k.Text}'");
                }
                if (s.SetIfHere(k, v)) return NIL();
            }
        }

        // The special forms run here: (if test then [else]), (do x...), (and x...), (or x...), (def/set/set! names
        // values...), (while test body...): the items from start on
        private LVal If(LEnv e, int start, int n, bool tail) {
            var t = NodeAt(start).Eval(e);
            if (t.IsErr) return t;
            if (!t.IsNIL) return tail ? NodeAt(start + 1).Tail(e) : NodeAt(start + 1).Eval(e);
            if (n == 2) return NIL();
            return tail ? NodeAt(start + 2).Tail(e) : NodeAt(start + 2).Eval(e);
        }
        private LVal DoForm(LEnv e, int start, bool tail) {
            if (_n == start) return NIL();
            int last = _n - 1;
            for (int k = start; k < last; k++) {
                var v = NodeAt(k).Eval(e);
                if (v.IsErr) return v;
            }
            return tail ? NodeAt(last).Tail(e) : NodeAt(last).Eval(e);
        }
        private LVal And(LEnv e, int start) {
            LVal last = T();
            for (int k = start; k < _n; k++) {
                last = NodeAt(k).Eval(e);
                if (last.IsErr || last.IsNIL) return last;
            }
            return last;
        }
        private LVal Or(LEnv e, int start) {
            for (int k = start; k < _n; k++) {
                var v = NodeAt(k).Eval(e);
                if (v.IsErr || !v.IsNIL) return v;
            }
            return NIL();
        }
        private LVal SetForm(LEnv e, Func<LEnv, LVal, LVal> b, int start, Name[] names) {
            bool bang = ReferenceEquals(b, SetBangFn);
            var func = bang ? "set!" : ReferenceEquals(b, SetFn) ? "set" : "def";
            for (int i = 0; i < names.Length; i++) {
                var v = NodeAt(start + 1 + i).Eval(e);
                var r = bang ? SetBang(e, names[i], i, v) : Builtins.Bind(e, func, names[i], v);
                if (r.IsErr) return r;
            }
            return NIL();
        }
        private LVal While(LEnv e, int start) {
            LVal result = NIL();
            while (true) {
                var intr = CheckInterrupt();
                if (intr != null) return intr;
                var t = RunItem(e, start);
                if (t.IsErr) return t;
                if (t.IsNIL) return result;
                for (int k = start + 1; k < _n; k++) {
                    result = RunItem(e, k);
                    if (result.IsErr) return result;
                }
            }
        }

        // A special form: its items as they're written
        private LVal Special(LEnv e, LVal f, FunInfo fi, int start, int n, bool tail) {
            if (n > fi.MaxArgs) return TooMany(f, n);
            var b = fi.BuiltinVal;
            if (!ReferenceEquals(b, _special)) {
                _special = b;
                _kind = ReferenceEquals(b, IfFn) ? 1 : ReferenceEquals(b, DoFn) ? 2 : ReferenceEquals(b, AndFn) ? 3 : ReferenceEquals(b, OrFn) ? 4
                    : ReferenceEquals(b, SetBangFn) || ReferenceEquals(b, SetFn) || ReferenceEquals(b, DefFn) ? 5 : ReferenceEquals(b, WhileFn) ? 6 : 0;
            }
            switch (_kind) {
                case 1 when n >= 2: return If(e, start, n, tail);
                case 2: return DoForm(e, start, tail);
                case 3: return And(e, start);
                case 4: return Or(e, start);
                case 5 when n >= 1 && Names(start, n).Length > 0: return SetForm(e, b!, start, _names!);
                case 6 when n >= 1: return While(e, start);
            }

            // Any other: the built-in itself, given the items (in tail position, its tail form)
            Site = _info;
            var a = Sexpr();
            for (int k = start; k < _n; k++) a.Cells!.Add(_cells[k]);
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
