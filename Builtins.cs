using System.Text;
using System.Numerics;

public partial class Builtins
{ 
    // What each built-in takes: the arguments it needs (given fewer, it's partially applied: a function waiting for
    // the rest) and the most it takes (given more, an error).  A special form takes its arguments as they're written,
    // or its own way: it's never partially applied
    private const int Any = int.MaxValue;
    private static readonly Dictionary<string, (int Min, int Max)> Arity = new() {
        ["fn"] = (2, 2), ["fun"] = (2, 2), ["def"] = (1, Any), ["set"] = (1, Any), ["set!"] = (1, Any),
        ["let"] = (1, Any), ["do"] = (0, Any), ["fexpr"] = (2, 2), ["gensym"] = (0, 1),
        ["list"] = (0, Any), ["head"] = (1, 1), ["tail"] = (1, 1), ["init"] = (1, 1), ["end"] = (1, 1),
        ["join"] = (1, Any), ["eval"] = (1, 1), ["len"] = (1, 1), ["item-at"] = (2, 2), ["subset"] = (2, 3),
        ["reverse"] = (1, 1), ["range"] = (1, 3), ["sort"] = (1, 2),
        ["+"] = (0, Any), ["-"] = (0, Any), ["*"] = (0, Any), ["/"] = (0, Any),
        ["rational.n"] = (1, 1), ["rational.d"] = (1, 1), ["random"] = (1, 1),
        ["and"] = (0, Any), ["or"] = (0, Any), ["while"] = (1, Any), ["each"] = (2, Any), ["dotimes"] = (2, Any),
        ["try"] = (1, 2), ["error-message"] = (1, 1), ["error-code"] = (1, 1), ["if"] = (2, 3),
        ["eq"] = (2, 2), ["neq"] = (2, 2), [">"] = (2, 2), ["<"] = (2, 2), ["cmp"] = (2, 2), ["<=>"] = (2, Any),
        ["load"] = (1, Any), ["save"] = (1, Any), ["error"] = (1, 2), ["print"] = (0, Any), ["write"] = (0, Any),
        ["output-of"] = (0, Any), ["index-of"] = (2, 2), ["last-index-of"] = (2, 2), ["substring"] = (2, 3),
        ["char-at"] = (2, 2), ["str-split"] = (1, 2), ["str-upper"] = (1, 1), ["str-lower"] = (1, 1),
        ["str-trim"] = (1, 1), ["str-replace"] = (3, 3), ["str-join"] = (1, 2), ["str-chars"] = (1, 1),
        ["str-pad-left"] = (2, 3), ["str-pad-right"] = (2, 3), ["str-repeat"] = (2, 2), ["format"] = (1, Any),
        ["char-code"] = (1, 1), ["code-char"] = (1, 1), ["alpha?"] = (1, 1), ["digit?"] = (1, 1),
        ["space?"] = (1, 1), ["upper?"] = (1, 1), ["lower?"] = (1, 1),
        ["read"] = (1, 1), ["open"] = (1, 2), ["close"] = (1, 1), ["read-line"] = (0, 1), ["read-byte"] = (0, 1),
        ["read-all"] = (0, 1), ["seek"] = (2, 2), ["tell"] = (1, 1), ["print-to"] = (1, Any), ["write-to"] = (1, Any),
        ["val"] = (1, 1), ["to-fixed"] = (1, 2), ["to-rational"] = (1, 1), ["truncate"] = (1, 1),
        ["complex"] = (2, 2), ["to-str"] = (1, 2), ["repr"] = (1, 1), ["to-sym"] = (1, 1), ["to-atom"] = (1, 1),
        ["fib"] = (1, 1), ["defined?"] = (0, 1), ["type-of"] = (1, 1),
        ["t?"] = (1, 1), ["nil?"] = (1, 1), ["num?"] = (1, 1), ["fixed?"] = (1, 1), ["rational?"] = (1, 1),
        ["int?"] = (1, 1), ["complex?"] = (1, 1), ["atom?"] = (1, 1), ["symbol?"] = (1, 1), ["string?"] = (1, 1),
        ["char?"] = (1, 1), ["function?"] = (1, 1), ["error?"] = (1, 1), ["expr?"] = (0, 1), ["qexpr?"] = (1, 1),
        ["sexpr?"] = (1, 1),
        ["hash-create"] = (0, 1), ["hash-get"] = (2, 2), ["hash-put"] = (2, Any), ["to#"] = (1, 1), ["from#"] = (1, 1),
        ["hash-key?"] = (2, 2), ["hash-keys"] = (1, 1), ["hash-values"] = (1, 1), ["hash-call"] = (2, Any),
        ["hash-clone"] = (1, Any), ["hash-remove"] = (2, 2), ["hash-add-tag"] = (2, Any), ["hash-lock"] = (1, Any),
        ["hash-make-const"] = (1, Any), ["hash-make-private"] = (1, Any), ["hash-make-not-nil"] = (1, Any),
        ["hash-tag?"] = (2, 2), ["hash-locked?"] = (1, 2), ["hash-private?"] = (1, 2), ["hash-const?"] = (1, 2),
        // (The system's: SystemBuiltins.cs)
        ["read-file"] = (1, 1), ["read-lines"] = (1, 1), ["write-file"] = (1, Any), ["append-file"] = (1, Any),
        ["ls"] = (0, 1), ["dir"] = (0, 1), ["stat"] = (1, 1), ["exists?"] = (1, 1), ["dir?"] = (1, 1),
        ["file?"] = (1, 1), ["mkdir"] = (1, 1), ["remove"] = (1, 1), ["rename"] = (2, 2), ["copy-file"] = (2, 2),
        ["cd"] = (0, 1), ["cwd"] = (0, 0), ["glob"] = (1, 1), ["run"] = (1, Any), ["sh"] = (1, 2), ["sh-out"] = (1, 2),
        ["spawn"] = (1, Any), ["wait"] = (1, 1), ["kill"] = (1, 1), ["pid"] = (0, 0), ["env"] = (0, 1),
        ["setenv"] = (2, 2), ["unsetenv"] = (1, 1), ["time"] = (0, 0), ["date"] = (0, 1), ["date-parts"] = (0, 1),
        ["seconds-of"] = (3, 6), ["ticks"] = (0, 0), ["tick-rate"] = (0, 0), ["sleep"] = (1, 1),
        ["bit-and"] = (1, Any), ["bit-or"] = (1, Any), ["bit-xor"] = (1, Any), ["bit-not"] = (1, 1),
        ["shl"] = (2, 2), ["shr"] = (2, 2), ["bit?"] = (2, 2), ["hex"] = (1, 2), ["bin"] = (1, 2), ["lo"] = (1, 1),
        ["hi"] = (1, 1), ["word"] = (2, 2), ["bytes"] = (1, 1), ["from-bytes"] = (1, 1), ["read-bytes"] = (2, 2),
        ["write-bytes"] = (2, 4), ["platform"] = (0, 0), ["hydra?"] = (0, 0), ["clock"] = (0, 0), ["key"] = (0, 0),
        ["key?"] = (0, 0),
        // (Buffers: BufferBuiltins.cs)
        ["buffer"] = (1, 2), ["buffer?"] = (1, 1), ["buffer-get"] = (2, 2), ["buffer-put"] = (3, 3), ["buffer-fill"] = (2, 4),
        ["buffer-copy"] = (3, 5), ["buffer-cmp"] = (3, 5), ["read-buffer"] = (2, 4),
        // (The library's: LibraryBuiltins.cs)
        ["not"] = (1, 1), ["=="] = (2, 2), [">="] = (2, 2), ["<="] = (2, 2), ["neg?"] = (1, 1), ["pos?"] = (1, 1),
        ["zero?"] = (1, 1), ["one?"] = (1, 1), ["1+"] = (1, 1), ["1-"] = (1, 1), ["abs"] = (1, 1), ["cons"] = (2, 2),
        ["fst"] = (1, 1), ["snd"] = (1, 1), ["thd"] = (1, 1), ["nth"] = (2, 2), ["last"] = (1, 1), ["take"] = (2, 2),
        ["drop"] = (2, 2), ["elem?"] = (2, 2), ["in?"] = (2, 2), ["map"] = (2, 2), ["filter"] = (2, 2),
        ["foldl"] = (3, 3), ["foldr"] = (3, 3), ["any?"] = (2, 2), ["all?"] = (2, 2), ["find"] = (2, 2),
        ["count"] = (2, 2), ["sum"] = (1, 1), ["product"] = (1, 1), ["min"] = (1, Any), ["max"] = (1, Any),
    };
    private static readonly HashSet<string> Special = new() {
        "def", "set", "set!", "let", "do", "eval", "if", "and", "or", "while", "each", "dotimes", "try", "<=>",
        "output-of", "defined?", "expr?",
    };
    // The ordinary built-ins given an error as a value, not stopped by it: they look at values, errors too
    private static readonly HashSet<string> TakesErrors = new() {
        "error?", "type-of", "error-message", "error-code", "repr", "print", "write", "print-to", "write-to",
        "t?", "nil?", "num?", "fixed?", "rational?", "int?", "complex?", "atom?", "symbol?", "string?", "char?",
        "function?", "qexpr?", "sexpr?", "buffer?",
    };

    // A built-in, its name and what it takes (Arity) on it
    private static LVal Described(LVal v, string name) {
        if (!Arity.TryGetValue(name, out var ar)) throw new Exception($"The built-in '{name}' has no Arity");
        v.BuiltinName = name;
        v.MinArgs = ar.Min;
        v.MaxArgs = ar.Max;
        v.IsSpecial = Special.Contains(name);
        v.TakesErrors = TakesErrors.Contains(name);
        return v;
    }

    // A built-in: an ordinary one gets its arguments' values (the evaluator evaluates them, and stops at an error, but
    // for one that takes errors); a special form gets them as they're written
    private static void AddBuiltin(LEnv e, string name, Func<LEnv, LVal, LVal> func) {
        LVal k = LVal.Sym(name);
        LVal v = Described(LVal.Builtin(func), name);
        e.Put(k.SymVal!, v);
    }

    private static LVal Lambda(LEnv e, LVal a) => Made(e, a, "fn");

    // A function made here (its closure: this scope): its formals a list of symbols, its body a list, or an error
    private static LVal Made(LEnv e, LVal a, string fn) {
        LVal formals = a.Pop(0);
        LVal body = a.Pop(0);
        if (!formals.IsQExpr || (formals.Cells ?? new List<LVal>()).Any(c => !c.IsSym)) return LVal.Err($"'{fn}' expects a list of symbols first");
        if (!body.IsQExpr) return LVal.Err($"'{fn}' expects a QExpr body");
        return LVal.Lambda(formals, body, e);
    }

    // (fexpr {formals} body): a function whose arguments come unevaluated, each in a Q-expression that remembers the
    // caller's scope, so (eval x) evaluates it there, if and when the function wants
    private static LVal Fexpr(LEnv e, LVal a) {
        var f = Made(e, a, "fexpr");
        if (f.IsErr) return f;
        f.IsFexpr = true;
        return f;
    }

    // (fun {name formals...} body): name defined (globally, as def does) as the function (fn {formals...} body), made
    // here (its closure: this scope)
    private static LVal Fun(LEnv e, LVal a) {
        var spec = a.Pop(0);    // (already evaluated: fun is AddBuiltinEvaluated)
        if (spec.IsErr) return spec;
        var body = a.Pop(0);
        if (body.IsErr) return body;
        if (!spec.IsQExpr || spec.Count == 0 || spec.Cells!.Any(c => !c.IsSym)) return LVal.Err("'fun' expects {name formals...} first");
        if (!body.IsQExpr) return LVal.Err("'fun' expects a QExpr body");
        var name = spec[0].SymVal;
        var formals = LVal.Qexpr();
        for (int i = 1; i < spec.Count; i++) formals.Add(spec[i]);
        var f = LVal.Lambda(formals, body, e);
        if (Warnings) WarnRedef(e, "fun", Name.Of(name), f);
        e.Def(name, f);
        return LVal.NIL();
    }

    public static LVal List(LEnv e, LVal a) {
        if (!a.IsSExpr) return LVal.Err("'list' can only be applied to a SExpr");
        a.ValType = LVal.LE.QEXPR;
        a.Scope = e;
        return a;
    }

    // A list argument for fn: an error if it isn't one (or is empty, when it mustn't be)
    private static LVal ListArg(LEnv e, LVal a, string fn, bool nonEmpty = false) {
        if (a.Count == 0) return LVal.Err($"Too few parameters passed to '{fn}'");
        var v = a.Pop(0);
        if (v.IsErr) return v;
        if (!v.IsQExpr) return LVal.Err($"'{fn}' expects a QExpr");
        if (nonEmpty && v.Count == 0) return LVal.Err($"'{fn}' passed an empty list");
        return v;
    }

    // (A list made from another keeps the scope it was written in: Scope, which eval runs it in.  The other isn't
    // changed: values are shared)
    private static LVal Sublist(LVal v, int from, int count) {
        var ret = LVal.Qexpr();
        ret.Cells!.AddRange(v.Cells!.GetRange(from, count));
        ret.Scope = v.Scope;
        return ret;
    }

    private static LVal Head(LEnv e, LVal a) {
        var v = ListArg(e, a, "head", true);
        if (v.IsErr) return v;
        return Sublist(v, 0, 1);
    }

    private static LVal Tail(LEnv e, LVal a) {
        LVal v = ListArg(e, a, "tail", true);
        if (v.IsErr) return v;
        return Sublist(v, 1, v.Count - 1);
    }

    private static LVal Init(LEnv e, LVal a) {
        var v = ListArg(e, a, "init", true);
        if (v.IsErr) return v;
        return Sublist(v, 0, v.Count - 1);
    }

    private static LVal End(LEnv e, LVal a) {
        var v = ListArg(e, a, "end", true);
        if (v.IsErr) return v;
        return Sublist(v, v.Count - 1, 1);
    }

    // (eval x): a Q-expression run as code, or an expression (an fexpr's argument: an S-expression or a symbol)
    // evaluated, in the scope it was written in (in tail position, as a function's last); anything else is itself
    private static TailStep EvalStep(LEnv e, LVal a) {
        if (a.Count != 1) return TailStep.Done(LVal.Err("Incorrect number of parameters passed to 'eval'"));
        LVal x =  a.Pop(0, e);
        if (x.IsErr) return TailStep.Done(x);
        var scope = x.Scope ?? e;
        if (x.IsQExpr) return TailStep.RunCode(scope, x);
        if (!x.IsSExpr && !x.IsSym) return TailStep.Done(x);
        return TailStep.Next(scope, x);
    }

    public static LVal Eval(LEnv e, LVal a) => EvalStep(e, a).Finish();

    private static LVal Join(LEnv e, LVal a) {

        LVal? x = null;
        while (a.Count > 0) {
            var y = a.Pop(0);
            if (y.IsErr) return y;
            if (!y.IsQExpr) return LVal.Err("Invalid parameter passed to 'join'.  Expected QExpr.");
            if (x == null) x = Sublist(y, 0, y.Count);
            else {
                x.Scope ??= y.Scope;
                x.Join(y);
            }
        }
        return x!;
    }

    // (The numbers given aren't changed: values are shared; the result is a new one)
    private static LVal Op(LEnv e, LVal a, string op) {
        if (a.Count == 2 && op != "/" && a[0].IsPlainInt(out var p) && a[1].IsPlainInt(out var q))
            return LVal.Number(op == "-" ? p - q : p * q);
        Num? x = null;
        LVal? first = null;
        while (a.Count > 0) {
            LVal y = a.Pop(0);
            if (y.IsErr) return y;
            if (!y.IsNum) return LVal.Err($"All parameters to operator '{op}' must be numbers.");
            if (x == null) {
                // one number: (- x) is its negation, (/ x) its reciprocal
                if (a.Count == 0 && op == "-") return LVal.Number(-y.NumVal!);
                if (a.Count == 0 && op == "/") {
                    if (y.NumVal!.IsZero) return LVal.Err("Division by zero.");
                    return LVal.Number((Num)new Int(BigInteger.One) / y.NumVal!);
                }

                // not negation? Just set x to y and continue;
                x = y.NumVal;
                first = y;
                continue;
            }

            if (op == "-") x = x - y.NumVal!;
            else if (op == "*") x = x * y.NumVal!;
            else if (op == "/") {
                if (y.NumVal!.IsZero) {
                    return LVal.Err("Division by zero.");
                }
                x = x / y.NumVal!;
            }
        }

        // If no arguments were passed, return 0 for "-", and 1 otherwise
        if (x == null) return LVal.Number(op == "-" ? BigInteger.Zero : BigInteger.One);
        return ReferenceEquals(x, first!.NumVal) ? first : LVal.Number(x);
    }

    // (+ n...): the numbers' sum; or, the first a string (or a character), the values as print shows them, joined
    // ("n=5" for "n=" and 5); anything else is an error
    private static LVal Add(LEnv e, LVal a) {
        if (a.Count == 2 && a[0].IsPlainInt(out var p) && a[1].IsPlainInt(out var q)) return LVal.Number(p + q);
        Num? x = null;
        LVal? first = null;
        StringBuilder? sb = null;
        while (a.Count > 0) {
            LVal y = a.Pop(0);
            if (y.IsErr) return y;
            if (sb != null) sb.Append(y.ToDisplay());
            else if (x == null && (y.IsStr || y.IsChar)) sb = new StringBuilder(y.StrVal);
            else if (!y.IsNum) return LVal.Err("'+' adds numbers, or joins values to a string");
            else if (x == null) { x = y.NumVal; first = y; }
            else x = x + y.NumVal!;
        }

        if (sb != null) return LVal.Str(sb.ToString());
        if (x == null) return LVal.Number(BigInteger.Zero);
        return ReferenceEquals(x, first!.NumVal) ? first : LVal.Number(x);
    }
    private static LVal Sub(LEnv e, LVal a) { return Op(e, a, "-"); }
    private static LVal Mul(LEnv e, LVal a) { return Op(e, a, "*"); }
    private static LVal Div(LEnv e, LVal a) { return Op(e, a, "/"); }

    private static LVal Var(LEnv e, LVal a, string func) {
        //if (a.Count == 0 || !a.IsQExpr) return LVal.Err($"Invalid parameter(s) passed to '{func}'");
        LVal syms = a.Pop(0);
        if (syms.IsSym) {
            if (a.Count != 1) return LVal.Err($"'{func}' passed too many arguments or symbols.  Expected 1, got {a.Count}");
            return Bind(e, func, syms.SymName, a.Pop(0, e));
        }
        if (syms.IsSExpr) syms = syms.Eval(e);

        if (syms.IsErr) return syms;
        if (!syms.IsQExpr) return LVal.Err($"'{func}' cannot define non-symbols");
        foreach (var s in syms.Cells!) if (!s.IsSym) return LVal.Err($"'{func}' cannot define non-symbols");

        if (syms.Count != a.Count) return LVal.Err($"'{func}' passed too many arguments or symbols.  Expected {syms.Count}, got {a.Count}");

        for (int i = 0; i < syms.Count; i++) {
            var r = Bind(e, func, syms[i].SymName, a.Pop(0, e));
            if (r.IsErr) return r;
        }
        
        return LVal.NIL();
    }

    // A name bound to a value, as def, set or set! do
    internal static LVal Bind(LEnv e, string func, Name symbol, LVal value) {
        if (func == "def") {
            if (Warnings) WarnRedef(e, func, symbol, value);
            e.Def(symbol, value);
        }
        else if (func == "set") e.Put(symbol, value);
        else return e.Update(symbol, value);
        return LVal.NIL();
    }

    // danlang -w: a global's definition that replaces a built-in, or a value of another kind (a function with what
    // isn't one, or the other way round), said on stderr, with where it's made
    public static bool Warnings;
    private static void WarnRedef(LEnv e, string func, Name name, LVal value) {
        if (!e.Root.TryGetLocal(name, out var old)) return;
        string? what = null;
        if (old.BuiltinVal != null && !ReferenceEquals(old.BuiltinVal, value.BuiltinVal)) what = "the built-in " + name.Text;
        else if (old.IsFun != value.IsFun) what = (old.IsFun ? "a function" : "a " + LVal.LEName(old.ValType).ToLower()) + ", " + name.Text + ",";
        if (what == null) return;
        var site = LVal.Site;
        var place = site == null ? "" : (site.File != null ? site.File + ":" + site.Line + ": " : "line " + site.Line + ": ");
        Console.Error.WriteLine($"{place}warning: {func} replaces {what} with {(value.IsFun ? "a function" : "a " + LVal.LEName(value.ValType).ToLower())}");
    }

    private static LVal Def(LEnv e, LVal a) { return Var(e, a, "def"); }
    private static LVal Put(LEnv e, LVal a) { return Var(e, a, "set"); }
    private static LVal Update(LEnv e, LVal a) { return Var(e, a, "set!"); }

    private static LVal Ord(LEnv e, LVal a, string op) {
        // if (!(a[0].IsNum && a[1].IsNum)) return LVal.Err($"'{op}' passed non-number parameter(s)");
        var cmp = a[0].CompareTo(a[1]);
        return LVal.Bool(op == ">" ? cmp > 0 : cmp < 0);
    }

    private static LVal Gt(LEnv e, LVal a) { return Ord(e, a, ">");  }
    private static LVal Lt(LEnv e, LVal a) { return Ord(e, a, "<");  }

    private static LVal Cmp(LEnv e, LVal a, string op) {
        bool r = false;
        if (op == "eq")       r =  a.Pop(0).Equals(a.Pop(0));
        else if (op == "neq") r = !a.Pop(0).Equals(a.Pop(0));
        else if (op == "cmp") return LVal.Number(a.Pop(0).CompareTo(a.Pop(0)));

        return LVal.Bool(r);
    }

    private static LVal Eq(LEnv e, LVal a) { return Cmp(e, a, "eq"); }
    private static LVal Neq(LEnv e, LVal a) { return Cmp(e, a, "neq"); }
    private static LVal Cmp(LEnv e, LVal a) { return Cmp(e, a, "cmp"); }

    // (if test then [else]): then's value if test isn't NIL, else else's (NIL if there's none); the branch is in
    // tail position
    private static TailStep IfStep(LEnv e, LVal a) {
        if (a.Count < 2) return TailStep.Done(LVal.Err("'if' supplied too few parameters"));

        // Actual logical check; all non-NIL expressions evaluate to T in the 'if' context
        var t = a.Pop(0, e);
        if (t.IsErr) return TailStep.Done(t);
        if (!t.IsNIL) {
            return TailStep.Next(e, a.Pop(0));
        }

        // if no else, so return NIL
        if (a.Count == 1) return TailStep.Done(LVal.NIL());
        return TailStep.Next(e, a.Pop(1));
    }

    // (do expr...): the expressions in turn (the first error stops them); the last one's value, in tail position
    private static TailStep DoStep(LEnv e, LVal a) {
        if (a.Count == 0) return TailStep.Done(LVal.NIL());
        while (a.Count > 1) {
            var v = a.Pop(0, e);
            if (v.IsErr) return TailStep.Done(v);
        }
        return TailStep.Next(e, a.Pop(0));
    }

    // A built-in with a tail position: its step, finished when it's called anywhere else
    private static void AddTailForm(LEnv e, string name, Func<LEnv, LVal, TailStep> step) {
        LVal v = Described(LVal.Builtin((env, a) => step(env, a).Finish()), name);
        v.TailForm = step;
        e.Put(name, v);
    }

    // (and x...): the first that's NIL (the rest aren't evaluated), or the last value (T, for none); an error stops it
    private static LVal And(LEnv e, LVal a) {
        LVal last = LVal.T();
        while (a.Count > 0) {
            last = a.Pop(0, e);
            if (last.IsErr || last.IsNIL) return last;
        }
        return last;
    }

    // (or x...): the first value that isn't NIL (the rest aren't evaluated), or NIL; an error stops it
    private static LVal Or(LEnv e, LVal a) {
        while (a.Count > 0) {
            var la = a.Pop(0, e);
            if (la.IsErr || !la.IsNIL) return la;
        }
        return LVal.NIL();
    }

    private static LVal SpaceShip(LEnv e, LVal a) {
        bool IsMatch(Num n, LVal l) {
            if (l.Count == 1) return true;
            switch (l[0].SymVal) {
                case "=":
                    return n.CompareTo(Num.Zero) == 0;
                case "<>": // not equal
                    return n.CompareTo(Num.Zero) != 0;
                case ">":
                    return n.CompareTo(Num.Zero) > 0;
                case "<":
                    return n.CompareTo(Num.Zero) < 0;
                case ">=": // greater than or equal
                case "=>":
                    return n.CompareTo(Num.Zero) >= 0;
                case "<=": // less than or equal
                case "=<":
                    return n.CompareTo(Num.Zero) <= 0;
                default:
                    throw new InvalidOperationException($"Unknown comparison test {l[0].SymVal}");
            }
        }

        if (a.Count < 2) return LVal.Err("Too few parameters passed to '<=>' operator");

        for (var i = 1; i < a.Count; ++i) if (!a[i].IsQExpr) return LVal.Err("Operator '<=>' received one or more invalid case blocks (not QExpr)");

        var cmpVal = a.Pop(0, e);
        if (cmpVal.IsErr) return cmpVal;
        if (!cmpVal.IsNum) return LVal.Err("First parameter to '<=>' must evaluate to a Number");

        var cmp = cmpVal.NumVal;
        if (cmp != null) {
            var i = 0;
            while (a.Count > 0) {
                var c = a.Pop(0);
                if (a.Count != 0 && c.Count < 2) return LVal.Err("All but last body block of a '<=>' operator must have comparator");
                try {
                    if (IsMatch(cmp, c)) return c[c.Count - 1].Eval(e);
                } catch (InvalidOperationException ex) {
                    return LVal.Err(ex.Message);
                }
                ++i;
            }
        }

        return LVal.NIL();
    }

    // The file 'load' means by name: as it is, or with .dl added; then, for a bare name, in lib/, and in the lib/
    // folder of danlang itself (found above the program)
    public static string? FindFile(string name) {
        var names = name.EndsWith(".dl") ? new[] {name} : new[] {name, name + ".dl"};
        foreach (var n in names) if (File.Exists(n)) return n;
        if (Path.GetFileName(name) != name) return null;

        var dirs = new List<string> {"lib"};
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent) {
            if (File.Exists(Path.Join(d.FullName, "lib", "globals.dl"))) {
                dirs.Add(Path.Join(d.FullName, "lib"));
                break;
            }
        }
        foreach (var dir in dirs)
            foreach (var n in names)
                if (File.Exists(Path.Join(dir, n))) return Path.Join(dir, n);
        return null;
    }

    public static LVal Load(LEnv e, LVal a) {

        LVal lastExpr = LVal.NIL();
        while (a.Count > 0) {
            var v = a.Pop(0);
            if (v.IsErr) return v;
            if (!v.IsStr) return LVal.Err("'load' passed non-string parameter(s)");
            var filename = FindFile(ToHost(v.StrVal!));
            if (filename == null) return SysErr("noent", v.StrVal);

            var input = File.ReadAllText(filename, Encoding.Latin1);
            var tokens = Parser.Tokenize(new StringReader(input)).ToList();
            var bad = tokens.FirstOrDefault(t => t.type == Parser.Token.Type.Error || t.type == Parser.Token.Type.More);
            if (bad != null) return LVal.Err($"{FromHost(filename)}:{bad.line}: {(bad.type == Parser.Token.Type.More ? $"missing {bad.parens}" : bad.str)}");
            var outer = LVal.SourceFile;
            LVal.SourceFile = FromHost(filename);
            LVal exprs;
            try {
                exprs = LVal.ReadExprFromTokens(tokens)!.Freeze();
            }
            finally {
                LVal.SourceFile = outer;
            }

            // a file's expressions are top level: the global environment's (the code shared, frozen)
            foreach (var x in exprs.Cells!) {
                lastExpr = x.Eval(e.Root);
                if (lastExpr.IsErr) return LVal.ErrFrom(lastExpr, $"{FromHost(filename)}: {lastExpr.ErrVal}");
                if (lastExpr.IsExit) throw new ExitException(lastExpr.ExitCode);
            }
        }
        return lastExpr;
    }

    public static LVal Save(LEnv e, LVal a) {
        var ob = a.Pop(0);
        if (ob.IsErr) return ob;

        if (a.Count > 0) {
            var filename = a.Pop(0);
            if (filename.IsErr) return filename;
            if (!filename.IsStr) return LVal.Err("Second parameter to 'save' must be a string");
            var s = new HashSet<string>();
            while (a.Count > 0) {
                var t = a.Pop(0);
                if (t.IsErr) return t;
                if (!t.IsAtom) return LVal.Err("Parameters 3+ for function 'save' must be atoms");
                s.Add(t.SymVal);
            }

            try {
                var fi = new FileInfo(ToHost(filename.StrVal));
                // (its folder is there, or it's the system's error: as write-file's)
                if (fi.Directory == null || !fi.Directory.Exists) return SysErr("noent", filename.StrVal);
                FileStream? fs = null;
                if (fi.Exists) {
                    if (s.Contains("overwrite")) fs = fi.Open(FileMode.Truncate);
                    else if (s.Contains("append")) fs = fi.Open(FileMode.Append);
                    else return LVal.Err("File exists but neither ':append' nor ':overwrite' were specified");
                } else fs = fi.OpenWrite();

                fs.Write(Encoding.Latin1.GetBytes(ob.Serialize() + "\n"));
                fs.Flush();
                fs.Close();
            }
            catch (Exception ex) {
                return SysErr(ex, filename.StrVal);
            }
        }
        else {
            Console.Out.Write(ob.Serialize() + "\n");     // (LF, as a file gets: not the host's line end)
        }

        return LVal.NIL();
    }

    // The values, as print shows them (a string's text as it is), each after sep, then end, to the stream
    private static LVal Output(LEnv e, LVal a, LStream s, string sep, string end) {
        var sb = new StringBuilder();
        var pre = "";
        while (a.Count > 0) {
            sb.Append(pre).Append(a.Pop(0).ToDisplay());
            pre = sep;
        }
        return s.Write(sb.Append(end).ToString());
    }

    private static LVal Print(LEnv e, LVal a) => Output(e, a, LStream.StdOut, " ", "\n");
    private static LVal Write(LEnv e, LVal a) => Output(e, a, LStream.StdOut, "", "");

    // (error message [code]): an error, with a code (an atom), if it's given one
    private static LVal Error(LEnv e, LVal a) {
        var m = a.Pop(0);
        if (m.IsErr) return m;
        string? code = null;
        if (a.Count > 0) {
            var c = a.Pop(0);
            if (c.IsErr) return c;
            if (!c.IsAtom) return LVal.Err("An error's code must be an atom");
            code = c.SymVal;
        }
        return LVal.Err(m.ToDisplay(), code);
    }

    private static LVal IsType(LEnv e, LVal a, LVal.LE type) {
        return LVal.Bool(a.Pop(0).ValType == type);
    }

    private static LVal IsNumber(LEnv e, LVal a) => IsType(e, a, LVal.LE.NUM);
    private static LVal IsString(LEnv e, LVal a) => IsType(e, a, LVal.LE.STR);
    private static LVal IsChar(LEnv e, LVal a) => IsType(e, a, LVal.LE.CHAR);
    private static LVal IsAtom(LEnv e, LVal a) => IsType(e, a, LVal.LE.ATOM);
    private static LVal IsSymbol(LEnv e, LVal a) => LVal.Bool(a.Pop(0).IsSym);
    private static LVal IsFunc(LEnv e, LVal a) => IsType(e, a, LVal.LE.FUN);
    private static LVal IsError(LEnv e, LVal a) => IsType(e, a, LVal.LE.ERR);
    private static LVal IsQExpr(LEnv e, LVal a) => IsType(e, a, LVal.LE.QEXPR);
    private static LVal IsSExpr(LEnv e, LVal a) => LVal.Bool(a.Pop(0).IsSExpr);
    private static LVal IsNIL(LEnv e, LVal a) => LVal.Bool(a.Pop(0).IsNIL);
    private static LVal IsT(LEnv e, LVal a) => IsType(e, a, LVal.LE.T);

    private static LVal IsExpr(LEnv e, LVal a) {
        switch (a.Pop(0).ValType) {
            case LVal.LE.STR:
            case LVal.LE.CHAR:
            case LVal.LE.NUM:
            case LVal.LE.T:
            case LVal.LE.SYM:
            case LVal.LE.ATOM:
            case LVal.LE.FUN:
            case LVal.LE.QEXPR:
            case LVal.LE.SEXPR: return LVal.T();
        }
        return LVal.NIL();
    }

    // int?, fixed?, rational? and complex?: whether x is a number of that kind (NIL for anything else, as num? is)
    private static LVal IsNumKind(LEnv e, LVal a, Func<Num, bool> kind) {
        var v = a.Pop(0);
        return LVal.Bool(v.IsNum && kind(v.NumVal!));
    }

    private static LVal IsInt(LEnv e, LVal a) => IsNumKind(e, a, n => n is Int && !(n is Fix) && !(n is Rat));
    private static LVal IsFix(LEnv e, LVal a) => IsNumKind(e, a, n => n is Fix);
    private static LVal IsRat(LEnv e, LVal a) => IsNumKind(e, a, n => n is Rat);
    private static LVal IsComplex(LEnv e, LVal a) => IsNumKind(e, a, n => n is Comp);

    private static LVal Complex(LEnv e, LVal a) {
        var re = a.Pop(0);
        if (re.IsErr) return re;
        var im = a.Pop(0);
        if (im.IsErr) return im;
        if (!re.IsNum || !im.IsNum) return LVal.Err("One or more parameters passed to 'complex' is not a Number");

        if (re.NumVal is Int r) {
            if (im.NumVal is Int i) return LVal.Number(Num.Norm(new Comp(r, i)));     // (a real number, its imaginary part 0)
            return LVal.Err("Imaginary part of complex must be an int, a rational, or a fixed");
        }
        return LVal.Err("Real part of complex must be an int, a rational, or a fixed");
    }

    private static LVal ToStr(LEnv e, LVal a) {

        var n = a.Pop(0);   // (already evaluated: to-str is AddBuiltinEvaluated)
        if (!n.IsNum) {
            // anything else: as print shows it
            if (a.Count > 0) return LVal.Err("Only a Number has a base for 'to-str'");
            return LVal.Str(n.ToDisplay());
        }
        if (a.Count == 1) {
            var str = a.Pop(0);
            if (!str.IsStr) return LVal.Err("Second 'to-str' parameter must be a String");
            return LVal.Str(NumberParser.ToBase(n.NumVal!, str.StrVal!));
        }

        return LVal.Str(n.ToStr());
    }

    // (substring s i [n]): s from index i (0 on; its length too, for ""), n characters of it (as many as there are),
    // or the rest; an index outside the string, or a count below 0, is an error
    private static LVal Substring(LEnv e, LVal a) {
        var str = a.Pop(0);
        if (str.IsErr) return str;
        if (!str.IsStr) return LVal.Err("First 'substring' parameter must be a String");
        var n = Whole(a.Pop(0), "substring", "an index", out var index);
        if (n.IsErr) return n;
        var s = str.StrVal;
        if (index < 0 || index > s.Length) return LVal.Err($"'substring': index {index} is outside the string");
        var rest = s.Length - (int)index;
        if (a.Count == 0) return LVal.Str(s.Substring((int)index));

        var l = Whole(a.Pop(0), "substring", "a count", out var len);
        if (l.IsErr) return l;
        if (len < 0) return LVal.Err("'substring': a count can't be negative");
        return LVal.Str(s.Substring((int)index, len > rest ? rest : (int)len));
    }

    private static LVal Split(LEnv e, LVal a) {
        var str = a.Pop(0);
        if (!str.IsStr) return LVal.Err("First 'str-split' parameter must be a string");

        // if only one param, add the default whitespace separator list
        if (a.Count == 0) {
            var q = LVal.Qexpr();
            q.Add(LVal.Str(" "));
            q.Add(LVal.Str("\t"));
            q.Add(LVal.Str("\n"));
            q.Add(LVal.Str("\r"));
            a.Add(q);
        }

        var sep = a.Pop(0);
        if (!(sep.IsStr || sep.IsQExpr) || (sep.IsQExpr && (sep.Count == 0 || !sep.Cells!.All(c => c.IsStr))))
            return LVal.Err("Second 'str-split' parameter must be a list of strings or a single string");

        string[] separators = sep.IsStr ? 
            new [] {sep.StrVal} :
            sep.Cells!.Select(c => c.StrVal).ToArray();

        var ret = LVal.Qexpr();
        foreach (var s in str.StrVal.Split(separators, StringSplitOptions.None)) ret.Add(LVal.Str(s));
        return ret;
    }

    private static LVal CharAt(LEnv e, LVal a) {
        var s = StrArg(e, a, "char-at");
        if (s.IsErr) return s;
        var i = Whole(a.Pop(0), "char-at", "an index", out var index);
        if (i.IsErr) return i;
        if (index < 0 || index >= s.StrVal.Length) return LVal.Err($"'char-at': index {index} is outside the string");
        return Chr(s.StrVal[(int)index]);
    }

    // (subset list i [n]): the list's items from index i (0 on; its length too, for NIL), n of them (as many as there
    // are), or the rest; an index outside the list, or a count below 0, is an error
    private static LVal Subset(LEnv e, LVal a) {

        var q = a.Pop(0);
        if (q.IsErr) return q;
        if (!q.IsQExpr) return LVal.Err("First 'subset' parameter must be a QExpr");

        var i = Whole(a.Pop(0), "subset", "an index", out var index);
        if (i.IsErr) return i;
        if (index < 0 || index > q.Count) return LVal.Err($"'subset': the list has no item {index}");
        var count = q.Count - (int)index;
        if (a.Count > 0) {
            var l = Whole(a.Pop(0), "subset", "a count", out var n);
            if (l.IsErr) return l;
            if (n < 0) return LVal.Err("'subset': a count can't be negative");
            if (n < count) count = (int)n;
        }
        var ret = LVal.Qexpr();
        ret.Cells!.AddRange(q.Cells!.GetRange((int)index, count));
        return ret;
    }

    // (item-at list i): the list's item i (0 on), as it's written; an index outside the list is an error
    private static LVal ItemAt(LEnv e, LVal a) {

        var q = a.Pop(0);
        if (q.IsErr) return q;
        if (!q.IsQExpr) return LVal.Err("First 'item-at' parameter must be a QExpr");

        var i = Whole(a.Pop(0), "item-at", "an index", out var index);
        if (i.IsErr) return i;
        if (index < 0 || index >= q.Count) return LVal.Err($"'item-at': the list has no item {index}");
        return q[(int)index];
    }

    private static LVal FastFib(LEnv e, LVal val) {
        var f = Whole(val.Pop(0), "fib", "n", out var v);
        if (f.IsErr) return f;
        if (v < 0) return LVal.Err("'fib' parameter 'n' cannot be negative");
        var n = (ulong)v;
        BigInteger a = 0;
        BigInteger b = 1;
        for (int i = 63; i >= 0; --i) {
            var t = a * (b * 2 - a);
            b = a * a + b * b;
            a = t;
            if (((n >> i) & 1) != 0) {
                t = a + b;
                a = b;
                b = t;
            }
        }
        return LVal.Number(a);
    }

    // Hash functions
    private static LVal HashCreate(LEnv e, LVal val) {
        if (val.Count > 0) return LVal.Hash(val.Pop(0), e);
        return LVal.Hash();
    }

    private static LVal HashGet(LEnv e, LVal val) {
        var hash = val.Pop(0);
        if (hash.IsNIL) return LVal.NIL();
        if (!hash.IsHash) return LVal.Err("First parameter to 'hash-get' must be a hash");

        var key = val.Pop(0);
        return hash.HashValue!.Get(key);
    }

    private static LVal HashPut(LEnv e, LVal val) {

        var hash = val.Pop(0);
        if (hash.IsErr) return hash;
        if (!hash.IsHash) return LVal.Err("First parameter to 'hash-put' must be a hash");

        // an entry, {key value tag...}: its value evaluated (with tags or without)
        LVal Put(LVal p) {
            if (p.IsErr) return p;
            if (!p.IsQExpr || p.Count < 2) return hash.HashValue!.Put(p);
            var v = p[1].Eval(e);
            if (v.IsErr) return v;
            var entry = LVal.Qexpr();
            entry.Cells!.AddRange(p.Cells!);
            entry.Cells![1] = v;
            return hash.HashValue!.Put(entry);
        }

        if (val.Count == 1) return Put(val.Pop(0));

        var ret = LVal.Qexpr();
        while (val.Count > 0) ret.Add(Put(val.Pop(0)));

        return ret;
    }

    private static LVal ToHash(LEnv e, LVal val) {
        var hash = val.Pop(0);
        if (!hash.IsQExpr) return LVal.Err("First parameter to 'to#' must be a QExpr");
        return LVal.Hash(hash, e);
    }

    private static LVal FromHash(LEnv e, LVal val) {
        var hash = val.Pop(0);
        if (!hash.IsHash) return LVal.Err("First parameter to 'from#' must be a hash");
        return hash.HashValue!.ToQexpr();
    }

    private static LVal HashHasKey(LEnv e, LVal val) {
        var hash = val.Pop(0);
        if (!hash.IsHash) return LVal.Err("First parameter to 'hash-key?' must be a hash");

        var key = val.Pop(0);
        return hash.HashValue!.ContainsKey(key);
    }

    private static LVal HashKeys(LEnv e, LVal val) {
        var hash = val.Pop(0);
        if (!hash.IsHash) return LVal.Err("First parameter to 'hash-keys' must be a hash");
        return hash.HashValue!.Keys;
    }

    private static LVal HashValues(LEnv e, LVal val) {
        var hash = val.Pop(0);
        if (!hash.IsHash) return LVal.Err("First parameter to 'hash-values' must be a hash");
        return hash.HashValue!.Values;
    }

    // (hash-call h key x...): the method at key applied to the values, &0 the hash, as (h key x...) is
    private static LVal HashCall(LEnv e, LVal val) {
        var hash = val.Pop(0);
        if (!hash.IsHash) return LVal.Err("First parameter to 'hash-call' must be a hash");
        var fKey = val.Pop(0);

        var f = hash.HashValue!.Get(fKey);
        if (f.IsErr) return f;
        if (!f.IsFun) return LVal.Err("Second parameter to 'hash-call' must be a key to a member function");

        // &0, the hash, in the method's own scope
        return Apply(e, LVal.Method(f, hash), val.Cells!.ToArray());
    }

    private static LVal HashClone(LEnv e, LVal val) {
        var hash = val.Pop(0);
        if (!hash.IsHash) return LVal.Err("First parameter to 'hash-clone' must be a hash");
        var clone = hash.HashValue!.Clone(val);
        return clone.OverrideError ?? LVal.Hash(clone);
    }

    private static LVal HashAddTag(LEnv e, LVal val) {
        var hash = val.Pop(0);
        if (!hash.IsHash) return LVal.Err("First parameter to 'hash-add-tag' must be a hash");

        if (val.Count == 1) return hash.HashValue!.AddTag(val.Pop(0));

        var ret = LVal.Qexpr();
        while (val.Count > 0) ret.Add(hash.HashValue!.AddTag(val.Pop(0)));

        return ret;
    }

    private static LVal _HashApplyTag(LEnv e, LVal val, string tag) {
        var a = LVal.Atom(tag);
        if (val.Count > 1) {
            var v = LVal.Qexpr();
            v.Add(val.Pop(0));
            while (val.Count > 0) {
                var q = LVal.Qexpr();
                q.Add(val.Pop(0));
                q.Add(a);
                v.Add(q);
            }
            val = v;
        }
        else val.Add(a);
        return HashAddTag(e, val);
    }

    private static LVal HashHasTag(LEnv e, LVal val) {
        var hash = val.Pop(0);
        if (!hash.IsHash) return LVal.Err("First parameter to 'hash-tag?' must be a hash");

        return hash.HashValue!.HasTag(val.Pop(0));
    }

    // (hash-locked? h [key]), (hash-private? ...), (hash-const? ...): whether the hash, or its entry, has the tag
    private static LVal HashHasNamedTag(LEnv e, LVal val, string fn, string tag) {
        var hash = val.Pop(0);
        if (hash.IsErr) return hash;
        if (!hash.IsHash) return LVal.Err($"First parameter to '{fn}' must be a hash");
        if (val.Count == 0) return hash.HashValue!.HasTag(LVal.Atom(tag));
        var key = val.Pop(0);
        if (key.IsErr) return key;
        var q = LVal.Qexpr();
        q.Add(key);
        q.Add(LVal.Atom(tag));
        return hash.HashValue!.HasTag(q);
    }

    private static LVal HashIsLocked(LEnv e, LVal val) => HashHasNamedTag(e, val, "hash-locked?", LHash.TAG_LOCKED);
    private static LVal HashIsPrivate(LEnv e, LVal val) => HashHasNamedTag(e, val, "hash-private?", LHash.TAG_PRIV);
    private static LVal HashIsConst(LEnv e, LVal val) => HashHasNamedTag(e, val, "hash-const?", LHash.TAG_RO);

    // ---- Helpers for the built-ins below

    // A character value
    private static LVal Chr(char c) => LVal.Char(c);

    // A symbol named s, as it is (LVal.Sym reads :x as an atom, \x as a character, nil as NIL ...)
    private static LVal PlainSym(string s) {
        var v = LVal.Sym("x");
        v.SymVal = s.ToLower();
        return v;
    }

    // A string argument for fn (a character counts as a string of one), evaluated; or an error
    private static LVal StrArg(LEnv e, LVal a, string fn) {
        if (a.Count == 0) return LVal.Err($"Too few parameters passed to '{fn}'");
        var v = a.Pop(0);
        if (v.IsErr) return v;
        if (!v.IsStr && !v.IsChar) return LVal.Err($"'{fn}' expects a String");
        return v;
    }

    // A real number: a number that isn't complex
    private static bool IsReal(LVal v) => v.IsNum && !(v.NumVal is Comp);

    // A whole number argument for fn (an integer, or a number equal to one: 2.0, 4/2), already evaluated: the number
    // (n its value), or an error (what it is to fn: "an index", "a count" ...)
    public static LVal Whole(LVal v, string fn, string what, out BigInteger n) {
        if (v.IsPlainInt(out n)) return v;
        n = 0;
        if (v.IsErr) return v;
        if (v.IsNum && !(v.NumVal is Comp)) {
            var r = Rat.ToRat(v.NumVal!);
            if (r.den == 1) {
                n = r.num;
                return v;
            }
        }
        return LVal.Err($"'{fn}': {what} must be a whole number, not {v.ToStr()}");
    }

    // A whole number argument for fn, evaluated, as an int; or an error
    private static LVal IntArg(LEnv e, LVal a, string fn, out int n) {
        n = 0;
        if (a.Count == 0) return LVal.Err($"Too few parameters passed to '{fn}'");
        var v = Whole(a.Pop(0), fn, "a number", out var w);
        if (v.IsErr) return v;
        if (w < int.MinValue || w > int.MaxValue) return LVal.Err($"'{fn}': {w} is too big");
        n = (int)w;
        return v;
    }

    // A value run as code: a Q-expression's contents evaluated (as eval does: in the scope it was written in, or
    // e), anything else as it is
    private static LVal RunCode(LEnv e, LVal v) {
        if (!v.IsQExpr) return v;
        return LVal.EvalCode(v.Scope ?? e, v);
    }

    // An unevaluated argument evaluated: a Q-expression run as code (here), anything else evaluated
    private static LVal EvalArg(LEnv e, LVal x) => x.IsQExpr ? RunCode(e, x) : x.Eval(e);

    // f called with values as they are, not evaluated again; anything but a function is the evaluator's error
    public static LVal Apply(LEnv e, LVal f, params LVal[] args) {
        if (!f.IsFun) return LVal.Err($"S-Expression starts with incorrect type. Got {LVal.LEName(f.ValType)}, Expected {LVal.LEName(LVal.LE.FUN)}.");
        if (f.BuiltinVal == null) return LVal.Apply(f, args.ToList());
        return LVal.ApplyBuiltin(e, f, args.ToList());
    }

    // ---- Control, errors and scope

    // (while test body...): the body's expressions in turn, as long as test isn't NIL (a Q-expression is run as
    // code); the last value, or NIL if the body never ran
    private static LVal While(LEnv e, LVal a) {
        if (a.Count < 1) return LVal.Err("'while' supplied too few parameters");
        LVal result = LVal.NIL();
        while (true) {
            var intr = LVal.CheckInterrupt();
            if (intr != null) return intr;
            var t = EvalArg(e, a[0]);
            if (t.IsErr) return t;
            if (t.IsNIL) return result;
            for (int i = 1; i < a.Count; i++) {
                result = EvalArg(e, a[i]);
                if (result.IsErr) return result;
            }
        }
    }

    // (try expr [handler]): expr's value; if that's an error, NIL, or the handler's value: the handler is evaluated
    // with &err the error's message and &code its code (an atom, or NIL; a function is called with the message).  A
    // Q-expression is run as code
    private static LVal Try(LEnv e, LVal a) {
        if (a.Count < 1 || a.Count > 2) return LVal.Err("'try' expects an expression and maybe a handler");
        var v = EvalArg(e, a[0]);
        if (!v.IsErr) return v;
        if (a.Count == 1) return LVal.NIL();
        var h = new LEnv(e);
        h.Put("&err", LVal.Str(v.ErrVal));
        h.Put("&code", v.ErrCode != null ? LVal.Atom(v.ErrCode) : LVal.NIL());
        var r = EvalArg(h, a[1]);
        if (r.IsFun) return Apply(h, r, LVal.Str(v.ErrVal));
        return r;
    }

    // (error-message x): an error's message, or NIL if x isn't an error
    private static LVal ErrorMessage(LEnv e, LVal a) {
        var v = a.Pop(0);
        return v.IsErr ? LVal.Str(v.ErrVal) : LVal.NIL();
    }

    // (let body) or (let {{name value} ...} body...): the body in a scope of its own (a Q-expression's value run as
    // code), the names bound in it first, each value evaluated there in turn; the last body's value, in tail position
    private static TailStep LetStep(LEnv e, LVal a) {
        if (a.Count == 0) return TailStep.Done(LVal.Err("'let' supplied too few parameters"));
        var scope = new LEnv(e);
        if (a.Count > 1) {
            var bindings = a.Pop(0);
            if (!bindings.IsQExpr) return TailStep.Done(LVal.Err("'let' expects a list of bindings first"));
            foreach (var b in bindings.Cells!) {
                if (!b.IsQExpr || b.Count < 1 || b.Count > 2 || !b[0].IsSym) return TailStep.Done(LVal.Err("A 'let' binding is {name value}"));
                var v = b.Count == 2 ? b[1].Eval(scope) : LVal.NIL();
                if (v.IsErr) return TailStep.Done(v);
                scope.Put(b[0].SymVal, v);
            }
        }
        while (a.Count > 1) {
            var r = LetBody(scope, a.Pop(0));
            if (r.IsErr) return TailStep.Done(r);
        }

        // the last: a Q-expression run, an S-expression's value, in tail position; anything else's value (a name's, as
        // it is, a list too)
        var last = a.Pop(0);
        if (last.IsSExpr) return TailStep.Next(scope, last);
        if (!last.IsQExpr) return TailStep.Done(last.Eval(scope));
        return TailStep.RunCode(last.Scope ?? scope, last);
    }

    // A let's body expression: a Q-expression run, anything else's value
    private static LVal LetBody(LEnv scope, LVal x) {
        if (x.IsQExpr) return RunCode(scope, x);
        return x.Eval(scope);
    }

    // The loops' binding form, {name x} ({i 10}, {x list}): the name, and x evaluated; or an error
    private static LVal LoopSpec(LEnv e, LVal spec, string fn, out string name) {
        name = "";
        if (spec.Count != 2 || !spec[0].IsSym) return LVal.Err($"'{fn}' expects {{name value}} or a function first");
        name = spec[0].SymVal;
        return spec[1].Eval(e);
    }

    // The loops' body, run with name item, in a scope of its own; an error, if one comes
    private static LVal LoopBody(LEnv e, LVal a, string name, LVal item) {
        var intr = LVal.CheckInterrupt();
        if (intr != null) return intr;
        var scope = new LEnv(e);
        scope.Put(name, item);
        for (int i = 0; i < a.Count; i++) {
            var r = EvalArg(scope, a[i]);
            if (r.IsErr) return r;
        }
        return LVal.NIL();
    }

    // (each f list), or (each {x list} body...): f called with each item (or character of a string) in turn, or the
    // body run with x each of them; NIL (or the first error)
    private static LVal Each(LEnv e, LVal a) {
        if (a.Count < 2) return LVal.Err("'each' supplied too few parameters");
        if (a[0].IsQExpr) {
            var l = LoopSpec(e, a.Pop(0), "each", out var name);
            if (l.IsErr) return l;
            if (!l.IsQExpr && !l.IsStr) return LVal.Err("'each' expects a list or a String");
            foreach (var item in l.IsStr ? l.StrVal.Select(Chr).ToList() : l.Cells!) {
                var r = LoopBody(e, a, name, item);
                if (r.IsErr) return r;
            }
            return LVal.NIL();
        }
        if (a.Count != 2) return LVal.Err("'each' expects a function and a list");
        var f = a.Pop(0, e);
        if (f.IsErr) return f;
        if (!f.IsFun) return LVal.Err("First 'each' parameter must be a function or {name list}");
        var lst = a.Pop(0, e);
        if (lst.IsErr) return lst;
        if (!lst.IsQExpr && !lst.IsStr) return LVal.Err("'each' expects a list or a String");
        foreach (var item in lst.IsStr ? lst.StrVal.Select(Chr).ToList() : lst.Cells!) {
            var r = Apply(e, f, item);
            if (r.IsErr) return r;
        }
        return LVal.NIL();
    }

    // (dotimes n f), or (dotimes {i n} body...): f called with 0, 1 ... n-1 in turn, or the body run with i each of
    // them; NIL (or the first error)
    private static LVal Dotimes(LEnv e, LVal a) {
        if (a.Count < 2) return LVal.Err("'dotimes' supplied too few parameters");
        string? name = null;
        LVal n;
        LVal? f = null;
        if (a[0].IsQExpr) {
            n = LoopSpec(e, a.Pop(0), "dotimes", out var nm);
            name = nm;
        }
        else {
            if (a.Count != 2) return LVal.Err("'dotimes' expects a Number and a function");
            n = a.Pop(0, e);
            if (n.IsErr) return n;
            f = a.Pop(0, e);
            if (f.IsErr) return f;
            if (!f.IsFun) return LVal.Err("Second 'dotimes' parameter must be a function");
        }
        n = Whole(n, "dotimes", "a count", out var count);
        if (n.IsErr) return n;
        for (BigInteger i = 0; i < count; i++) {
            var r = f != null ? Apply(e, f, LVal.Number(i)) : LoopBody(e, a, name!, LVal.Number(i));
            if (r.IsErr) return r;
        }
        return LVal.NIL();
    }

    // (gensym [prefix]): a symbol no other has (g__1, g__2 ...; or prefix__1 ...)
    private static int _gensym = 0;
    private static LVal Gensym(LEnv e, LVal a) {
        var prefix = "g";
        if (a.Count > 0) {
            var p = StrArg(e, a, "gensym");
            if (p.IsErr) return p;
            prefix = p.StrVal;
        }
        return PlainSym($"{prefix}__{++_gensym}");
    }

    // defined?: T always is; a name a call makes (&1, &_ ...) in the scopes of this call (up to its function's own),
    // so (defined? &1) says whether this call had a first extra argument; any other name, wherever it's visible
    private static bool IsDefined(LEnv e, LVal v) {
        if (v.IsT) return true;
        if (!v.IsSym) return false;
        if (!v.SymVal.StartsWith('&')) return e.Find(v.SymVal) != null;
        for (var s = e; s != null; s = s.Parent) {
            if (s.ContainsKey(v.SymName)) return true;
            if (s.IsCall) break;
        }
        return false;
    }

    // (type-of x): an atom naming x's type
    private static LVal TypeOf(LEnv e, LVal a) {
        var v = a.Pop(0);
        return LVal.Atom(v.ValType switch {
            LVal.LE.NUM    => "number",
            LVal.LE.STR    => "string",
            LVal.LE.CHAR   => "char",
            LVal.LE.ATOM   => "atom",
            LVal.LE.SYM    => "symbol",
            LVal.LE.FUN    => "function",
            LVal.LE.ERR    => "error",
            LVal.LE.T      => "t",
            LVal.LE.QEXPR  => "list",
            LVal.LE.SEXPR  => "sexpr",
            LVal.LE.HASH   => "hash",
            LVal.LE.STREAM => "stream",
            LVal.LE.BUFFER => "buffer",
            LVal.LE.EXIT   => "exit",
            _              => "unknown"
        });
    }

    // (read text): the expressions in the text, unevaluated, as a list: (eval (read "+ 1 2")) is 3
    private static LVal Read(LEnv e, LVal a) {
        var s = StrArg(e, a, "read");
        if (s.IsErr) return s;
        var tokens = Parser.Tokenize(new StringReader(s.StrVal)).ToList();
        var bad = tokens.FirstOrDefault(t => t.type == Parser.Token.Type.Error || t.type == Parser.Token.Type.More);
        if (bad != null) return LVal.Err($"'read': {(bad.type == Parser.Token.Type.More ? $"missing {bad.parens}" : bad.str)}");
        var x = LVal.ReadExprFromTokens(tokens)!;
        x.ValType = LVal.LE.QEXPR;
        return x;
    }

    // ---- Lists

    // (reverse x): a list's items, or a string's characters, the other way round
    private static LVal Reverse(LEnv e, LVal a) {
        var v = a.Pop(0);
        if (v.IsErr) return v;
        if (v.IsStr) return LVal.Str(new string(v.StrVal.Reverse().ToArray()));
        if (!v.IsQExpr) return LVal.Err("'reverse' expects a QExpr or a String");
        var r = Sublist(v, 0, v.Count);
        r.Cells!.Reverse();
        return r;
    }

    // (range n), (range from to) or (range from to step): the numbers from 0 (or from) up to, not including, to; down
    // to it, with a negative step
    private static LVal Range(LEnv e, LVal a) {
        var n = new List<Num>();
        while (a.Count > 0) {
            var v = a.Pop(0);
            if (v.IsErr) return v;
            if (!v.IsNum) return LVal.Err("'range' expects Numbers");
            n.Add(v.NumVal!);
        }
        var from = n.Count > 1 ? n[0] : Num.Zero;
        var to = n.Count > 1 ? n[1] : n[0];
        var step = n.Count > 2 ? n[2] : new Int(BigInteger.One);
        var dir = step.CompareTo(Num.Zero);
        if (dir == 0) return LVal.Err("'range' step cannot be 0");
        var ret = LVal.Qexpr();
        for (var x = from; x.CompareTo(to) * dir < 0; x = x + step) {
            if (ret.Count >= 1000000) return LVal.Err("'range' is too long");
            ret.Add(LVal.Number(x));
        }
        return ret;
    }

    // (sort list [less]): the list's items in order (the sort is stable): by cmp, or by less, a function of two
    // items that's T when the first goes before the second
    private static LVal Sort(LEnv e, LVal a) {
        var l = ListArg(e, a, "sort");
        if (l.IsErr) return l;
        LVal? less = null;
        if (a.Count > 0) {
            less = a.Pop(0);
            if (less.IsErr) return less;
            if (!less.IsFun) return LVal.Err("Second 'sort' parameter must be a function");
        }
        LVal? err = null;
        bool Less(LVal x, LVal y) {
            var r = Apply(e, less!, x, y);
            if (r.IsErr) {
                err ??= r;
                return false;
            }
            return !r.IsNIL;
        }
        int Compare(LVal x, LVal y) => less == null ? x.CompareTo(y) : Less(x, y) ? -1 : Less(y, x) ? 1 : 0;
        var sorted = l.Cells!.OrderBy(x => x, Comparer<LVal>.Create(Compare)).ToList();
        if (err != null) return err;
        var ret = LVal.Qexpr();
        foreach (var x in sorted) ret.Add(x);
        return ret;
    }

    // (index-of s sub) and (last-index-of s sub): where sub first (last) is in the string s, -1 if it isn't; or, for
    // a list, where the item equal to sub is
    private static LVal IndexOf(LEnv e, LVal a, bool last) {
        var fn = last ? "last-index-of" : "index-of";
        var s = a.Pop(0);
        if (s.IsErr) return s;
        var x = a.Pop(0);
        if (x.IsErr) return x;
        if (s.IsQExpr) return LVal.Number(last ? s.Cells!.FindLastIndex(c => c.Equals(x)) : s.Cells!.FindIndex(c => c.Equals(x)));
        if (!s.IsStr || !(x.IsStr || x.IsChar)) return LVal.Err($"'{fn}' expects a String and a String");
        return LVal.Number(last ? s.StrVal.LastIndexOf(x.StrVal, StringComparison.Ordinal) : s.StrVal.IndexOf(x.StrVal, StringComparison.Ordinal));
    }

    // ---- Strings and characters

    // A string made from one (a character stays a character, if the result is one)
    private static LVal StrMap(LEnv e, LVal a, string fn, Func<string, string> f) {
        var s = StrArg(e, a, fn);
        if (s.IsErr) return s;
        var r = f(s.StrVal);
        return s.IsChar && r.Length == 1 ? Chr(r[0]) : LVal.Str(r);
    }

    // (str-replace s old new): s with each old new
    private static LVal StrReplace(LEnv e, LVal a) {
        var s = StrArg(e, a, "str-replace");
        if (s.IsErr) return s;
        var o = StrArg(e, a, "str-replace");
        if (o.IsErr) return o;
        var n = StrArg(e, a, "str-replace");
        if (n.IsErr) return n;
        if (o.StrVal.Length == 0) return LVal.Err("'str-replace' cannot replace an empty String");
        return LVal.Str(s.StrVal.Replace(o.StrVal, n.StrVal, StringComparison.Ordinal));
    }

    // (str-join list [sep]): the items, as print shows them, joined, sep between them
    private static LVal StrJoin(LEnv e, LVal a) {
        var l = ListArg(e, a, "str-join");
        if (l.IsErr) return l;
        var sep = "";
        if (a.Count > 0) {
            var s = StrArg(e, a, "str-join");
            if (s.IsErr) return s;
            sep = s.StrVal;
        }
        return LVal.Str(string.Join(sep, l.Cells!.Select(c => c.ToDisplay())));
    }

    // (str-chars s): the string's characters, a list
    private static LVal StrChars(LEnv e, LVal a) {
        var s = StrArg(e, a, "str-chars");
        if (s.IsErr) return s;
        var ret = LVal.Qexpr();
        foreach (var c in s.StrVal) ret.Add(Chr(c));
        return ret;
    }

    // (str-pad-left s n [c]) and (str-pad-right s n [c]): s made n long with c (a space) before or after it
    private static LVal StrPad(LEnv e, LVal a, string fn, bool left) {
        var s = StrArg(e, a, fn);
        if (s.IsErr) return s;
        var v = IntArg(e, a, fn, out var n);
        if (v.IsErr) return v;
        if (n < 0) return LVal.Err($"'{fn}' length cannot be negative");
        var c = ' ';
        if (a.Count > 0) {
            var p = StrArg(e, a, fn);
            if (p.IsErr) return p;
            if (p.StrVal.Length != 1) return LVal.Err($"'{fn}' pads with one character");
            c = p.StrVal[0];
        }
        return LVal.Str(left ? s.StrVal.PadLeft(n, c) : s.StrVal.PadRight(n, c));
    }

    // (str-repeat s n): s n times
    private static LVal StrRepeat(LEnv e, LVal a) {
        var s = StrArg(e, a, "str-repeat");
        if (s.IsErr) return s;
        var v = IntArg(e, a, "str-repeat", out var n);
        if (v.IsErr) return v;
        if (n < 0) return LVal.Err("'str-repeat' count cannot be negative");
        return LVal.Str(string.Concat(Enumerable.Repeat(s.StrVal, n)));
    }

    // (format text args...): the text with each {} the next argument, as print shows it ({{ and }}: a brace)
    private static LVal Format(LEnv e, LVal a) {
        var f = StrArg(e, a, "format");
        if (f.IsErr) return f;
        var sb = new StringBuilder();
        var s = f.StrVal;
        for (int i = 0; i < s.Length; i++) {
            var next = i + 1 < s.Length ? s[i + 1] : '\0';
            if (s[i] == '{' && next == '{') { sb.Append('{'); i++; }
            else if (s[i] == '}' && next == '}') { sb.Append('}'); i++; }
            else if (s[i] == '{' && next == '}') {
                if (a.Count == 0) return LVal.Err("'format' has too few arguments for its {}s");
                var v = a.Pop(0);
                if (v.IsErr) return v;
                sb.Append(v.ToDisplay());
                i++;
            }
            else sb.Append(s[i]);
        }
        if (a.Count > 0) return LVal.Err("'format' has more arguments than {}s");
        return LVal.Str(sb.ToString());
    }

    // (char-code c): a character's code (or a string of one's)
    private static LVal CharCode(LEnv e, LVal a) {
        var c = StrArg(e, a, "char-code");
        if (c.IsErr) return c;
        if (c.StrVal.Length != 1) return LVal.Err("'char-code' expects one character");
        return LVal.Number(c.StrVal[0]);
    }

    // (code-char n): the character with that code
    private static LVal CodeChar(LEnv e, LVal a) {
        var v = IntArg(e, a, "code-char", out var n);
        if (v.IsErr) return v;
        if (n < 0 || n > 255) return LVal.Err($"'code-char': {n} isn't a character's code (0-255)");
        return Chr((char)n);
    }

    // alpha? digit? space? upper? lower?: whether a character is one (or every character of a string that isn't empty)
    private static LVal CharTest(LEnv e, LVal a, string fn, Func<char, bool> test) {
        var s = StrArg(e, a, fn);
        if (s.IsErr) return s;
        return LVal.Bool(s.StrVal.Length > 0 && s.StrVal.All(test));
    }

    // ---- Output and streams

    // (output-of expr...): what the expressions printed (to stdout), as a string; an error, if one is one
    private static LVal OutputOf(LEnv e, LVal a) {
        var saved = Console.Out;
        var sw = new StringWriter();
        Console.SetOut(sw);
        try {
            while (a.Count > 0) {
                var v = a.Pop(0, e);
                if (v.IsErr) return v;
            }
        }
        finally {
            Console.SetOut(saved);
        }
        return LVal.Str(sw.ToString());
    }

    // (open path [mode]): a stream on the file: :read (the default), :write (made, or emptied first) or :append
    private static LVal Open(LEnv e, LVal a) {
        var p = StrArg(e, a, "open");
        if (p.IsErr) return p;
        var mode = "read";
        if (a.Count > 0) {
            var m = a.Pop(0);
            if (m.IsErr) return m;
            if (!m.IsAtom) return LVal.Err("'open' mode must be :read, :write, :append or :update");
            mode = m.SymVal;
        }
        if (mode != "read" && mode != "write" && mode != "append" && mode != "update")
            return LVal.Err($"'open' mode must be :read, :write, :append or :update, not :{mode}");
        var path = ToHost(p.StrVal);
        if (Directory.Exists(path)) return SysErr("isdir", p.StrVal);
        try {
            Stream s = mode switch {
                "read"   => File.OpenRead(path),
                "write"  => File.Create(path),
                "update" => new FileStream(path, FileMode.Open, FileAccess.ReadWrite),
                _        => new FileStream(path, FileMode.Append, FileAccess.Write),
            };
            return LVal.Stream(s);
        }
        catch (Exception ex) {
            return SysErr(ex, p.StrVal);
        }
    }

    // A stream's operation: the stream, then n more arguments, evaluated
    private static LVal StreamOp(LEnv e, LVal a, string fn, int n, Func<LStream, LVal[], LVal> op) {
        var s = a.Pop(0);
        if (s.IsErr) return s;
        if (!s.IsStream) return LVal.Err($"'{fn}' expects a stream");
        var args = new LVal[n];
        for (int i = 0; i < n; i++) {
            args[i] = a.Pop(0);
            if (args[i].IsErr) return args[i];
        }
        return op(s.StreamValue!, args);
    }

    // (print-to s args...) and (write-to s args...): print's and write's output, to a stream
    private static LVal OutputTo(LEnv e, LVal a, string fn, string sep, string end) {
        var s = a.Pop(0);
        if (s.IsErr) return s;
        if (!s.IsStream) return LVal.Err($"'{fn}' expects a stream first");
        return Output(e, a, s.StreamValue!, sep, end);
    }

    // ---- Hashes

    // (hash-remove h key): the entry taken out of the hash; its value, or NIL if it had none
    private static LVal HashRemove(LEnv e, LVal val) {
        var hash = val.Pop(0);
        if (hash.IsErr) return hash;
        if (!hash.IsHash) return LVal.Err("First parameter to 'hash-remove' must be a hash");
        var key = val.Pop(0);
        if (key.IsErr) return key;
        return hash.HashValue!.Remove(key);
    }


    static private Random _rand = new Random();

    // (random n): an integer from 0 up to, not including, n (an integer, 1 or more, of any size)
    private static LVal RandomBelow(LEnv e, LVal a) {
        var v = a.Pop(0);
        if (v.IsErr) return v;
        if (!v.IsNum || !(v.NumVal is Int i) || i is Rat || (i is Fix f && f.dec != 0)) return LVal.Err("'random' expects an integer");
        var n = i.num;
        if (n <= 0) return LVal.Err("'random' expects a number above 0");
        if (n <= long.MaxValue) return LVal.Number(_rand.NextInt64((long)n));

        // past a long: random bits, as many as n has, till they're below it
        var bits = (int)n.GetBitLength();
        var buf = new byte[(bits + 7) / 8 + 1];
        BigInteger r;
        do {
            _rand.NextBytes(buf);
            buf[^1] = 0;
            buf[^2] &= (byte)(0xFF >> ((buf.Length - 1) * 8 - bits));
            r = new BigInteger(buf);
        } while (r >= n);
        return LVal.Number(r);
    }

    // Add Builtins to an Environment
    public static void AddBuiltins(LEnv e) {
        // variable definition functions
        AddBuiltin(e, "fn",  Lambda);
        AddBuiltin(e, "fun", Fun);
        AddBuiltin(e, "def", Def);
        AddBuiltin(e, "set", Put);
        AddBuiltin(e, "set!", Update);
        AddTailForm(e, "let", LetStep);
        AddTailForm(e, "do", DoStep);
        AddBuiltin(e, "fexpr", Fexpr);
        AddBuiltin(e, "gensym", Gensym);
        
        // list functions
        AddBuiltin(e, "list", List);
        AddBuiltin(e, "head", Head);
        AddBuiltin(e, "tail", Tail);
        AddBuiltin(e, "init", Init);
        AddBuiltin(e, "end",  End);
        AddBuiltin(e, "join", Join);
        AddTailForm(e, "eval", EvalStep);
        AddBuiltin(e, "len",  (e, a) => a[0].ValType switch {
            LVal.LE.QEXPR => LVal.Number(a[0].Count),
            LVal.LE.STR   => LVal.Number(a[0].StrVal.Length),
            LVal.LE.HASH  => LVal.Number(a[0].HashValue!.Count),
            LVal.LE.BUFFER => LVal.Number(a[0].BufferValue!.Length),
            LVal.LE.ERR   => a[0],
            _             => LVal.Err("'len' requires parameter of type string, list, hash or buffer")
        });
        AddBuiltin(e, "item-at", ItemAt);
        AddBuiltin(e, "subset", Subset);
        AddBuiltin(e, "reverse", Reverse);
        AddBuiltin(e, "range", Range);
        AddBuiltin(e, "sort", Sort);

        // math functions
        AddBuiltin(e, "+", Add);
        AddBuiltin(e, "-", Sub);
        AddBuiltin(e, "*", Mul);
        AddBuiltin(e, "/", Div);

        AddBuiltin(e, "rational.n", (e, a) => IsReal(a[0]) ? LVal.Number(Rat.ToRat(a[0].NumVal!).num) :
            LVal.Err("'rational.n' expects a real number"));

        AddBuiltin(e, "rational.d", (e, a) => IsReal(a[0]) ? LVal.Number(Rat.ToRat(a[0].NumVal!).den) :
            LVal.Err("'rational.d' expects a real number"));

        AddBuiltin(e, "random",     RandomBelow);
 
        // logical funcions
        AddBuiltin(e, "and", And);
        AddBuiltin(e, "or",  Or);

        // control and errors
        AddBuiltin(e, "while", While);
        AddBuiltin(e, "each", Each);
        AddBuiltin(e, "dotimes", Dotimes);
        AddBuiltin(e, "try", Try);
        AddBuiltin(e, "error-message", ErrorMessage);
        AddBuiltin(e, "error-code", (e, a) => {
            var v = a.Pop(0);
            return v.IsErr && v.ErrCode != null ? LVal.Atom(v.ErrCode) : LVal.NIL();
        });

        // comparison functions
        AddTailForm(e, "if", IfStep);
        AddBuiltin(e, "eq",  Eq);
        AddBuiltin(e, "neq", Neq);

        AddBuiltin(e, ">",   Gt);
        AddBuiltin(e, "<",   Lt);
        AddBuiltin(e, "cmp", Cmp);
        AddBuiltin(e, "<=>", SpaceShip);

        // string functions
        AddBuiltin(e, "load",  Load);
        AddBuiltin(e, "save",  Save);
        AddBuiltin(e, "error", Error);
        AddBuiltin(e, "print", Print);
        AddBuiltin(e, "write", Write);
        AddBuiltin(e, "output-of", OutputOf);
        AddBuiltin(e, "index-of",      (e, a) => IndexOf(e, a, false));
        AddBuiltin(e, "last-index-of", (e, a) => IndexOf(e, a, true));
        AddBuiltin(e, "substring", Substring);
        AddBuiltin(e, "char-at",   CharAt);
        AddBuiltin(e, "str-split", Split);
        AddBuiltin(e, "str-upper", (e, a) => StrMap(e, a, "str-upper", s => new string(s.Select(c => c >= 'a' && c <= 'z' ? (char)(c - 32) : c).ToArray())));
        AddBuiltin(e, "str-lower", (e, a) => StrMap(e, a, "str-lower", s => new string(s.Select(c => c >= 'A' && c <= 'Z' ? (char)(c + 32) : c).ToArray())));
        AddBuiltin(e, "str-trim",  (e, a) => StrMap(e, a, "str-trim", s => s.Trim(' ', '\t', '\n', '\v', '\f', '\r')));
        AddBuiltin(e, "str-replace", StrReplace);
        AddBuiltin(e, "str-join", StrJoin);
        AddBuiltin(e, "str-chars", StrChars);
        AddBuiltin(e, "str-pad-left",  (e, a) => StrPad(e, a, "str-pad-left", true));
        AddBuiltin(e, "str-pad-right", (e, a) => StrPad(e, a, "str-pad-right", false));
        AddBuiltin(e, "str-repeat", StrRepeat);
        AddBuiltin(e, "format", Format);
        AddBuiltin(e, "char-code", CharCode);
        AddBuiltin(e, "code-char", CodeChar);
        AddBuiltin(e, "alpha?", (e, a) => CharTest(e, a, "alpha?", c => (c | 32) >= 'a' && (c | 32) <= 'z'));
        AddBuiltin(e, "digit?", (e, a) => CharTest(e, a, "digit?", c => c >= '0' && c <= '9'));
        AddBuiltin(e, "space?", (e, a) => CharTest(e, a, "space?", c => c == ' ' || (c >= '\t' && c <= '\r')));
        AddBuiltin(e, "upper?", (e, a) => CharTest(e, a, "upper?", c => c >= 'A' && c <= 'Z'));
        AddBuiltin(e, "lower?", (e, a) => CharTest(e, a, "lower?", c => c >= 'a' && c <= 'z'));

        // reading, and streams
        AddBuiltin(e, "read", Read);
        AddBuiltin(e, "open", Open);
        AddBuiltin(e, "close", (e, a) => StreamOp(e, a, "close", 0, (s, x) => s.Close()));
        AddBuiltin(e, "read-line", (e, a) => a.Count == 0 ? LStream.StdIn.ReadLine() : StreamOp(e, a, "read-line", 0, (s, x) => s.ReadLine()));
        AddBuiltin(e, "read-byte", (e, a) => a.Count == 0 ? LStream.StdIn.ReadByte() : StreamOp(e, a, "read-byte", 0, (s, x) => s.ReadByte()));
        AddBuiltin(e, "read-all",  (e, a) => a.Count == 0 ? LStream.StdIn.ReadAll() : StreamOp(e, a, "read-all", 0, (s, x) => s.ReadAll()));
        AddBuiltin(e, "seek", (e, a) => StreamOp(e, a, "seek", 1, (s, x) => s.SetPosition(x[0])));
        AddBuiltin(e, "tell", (e, a) => StreamOp(e, a, "tell", 0, (s, x) => s.Position));
        AddBuiltin(e, "print-to", (e, a) => OutputTo(e, a, "print-to", " ", "\n"));
        AddBuiltin(e, "write-to", (e, a) => OutputTo(e, a, "write-to", "", ""));
        e.Put("stdin",  LVal.Stream(LStream.StdIn));
        e.Put("stdout", LVal.Stream(LStream.StdOut));
        e.Put("stderr", LVal.Stream(LStream.StdErr));

        // conversion functions
        // TODO: move the bodies of these definitions to static methods with error checking
        AddBuiltin(e, "val", (e, a) => {
            var s = StrArg(e, a, "val");
            if (s.IsErr) return s;
            Num? n;
            try {
                n = NumberParser.ParseString(s.StrVal);
            }
            catch (DivideByZeroException) {
                return LVal.Err("Division by zero.");
            }
            return n == null ? LVal.Err($"'val': \"{s.StrVal}\" isn't a number") : LVal.Number(n);
        });
        // (to-fixed x [places]): x as a fixed decimal of that many places (10), cut short (not rounded)
        AddBuiltin(e, "to-fixed", (e, a) => {
            if (!IsReal(a[0])) return LVal.Err("'to-fixed' expects a real number");
            BigInteger places = 10;
            if (a.Count > 1) {
                var p = Whole(a[1], "to-fixed", "a number of places", out places);
                if (p.IsErr) return p;
                if (places < 0 || places > 10000) return LVal.Err($"'to-fixed': {places} places is outside 0 to 10000");
            }
            return LVal.Number(Rat.ToRat(a[0].NumVal!).ToFix((int)places));
        });
        AddBuiltin(e, "to-rational", (e, a) => !IsReal(a[0]) ? LVal.Err("'to-rational' expects a real number") :
            LVal.Number(Num.Norm(Rat.ToRat(a[0].NumVal!))));
        AddBuiltin(e, "truncate", (e, a) => !IsReal(a[0]) ? LVal.Err("'truncate' expects a real number") :
            LVal.Number(a[0].NumVal!.ToInt()));
        AddBuiltin(e, "complex",     Complex);
        AddBuiltin(e, "to-str", ToStr);
        // (repr x): x as the REPL shows it (a string in quotes, with escapes), an error too ("Error: ...")
        AddBuiltin(e, "repr", (e, a) => LVal.Str(a.Pop(0).ToStr()));
        AddBuiltin(e, "to-sym",
            (e, a) => {
                var x = a.Pop(0);
                if (x.IsSym)  return x;
                if (x.IsAtom) return LVal.Sym(x.SymVal);
                if (x.IsStr)  return PlainSym(x.StrVal);
                return LVal.Err("Only atoms and strings can be converted into symbols");
            });

        AddBuiltin(e, "to-atom",
            (e, a) => {
                var x = a.Pop(0);
                if (x.IsAtom) return x;
                if (x.IsSym)  return LVal.Atom(x.SymVal);
                if (x.IsNum)  return LVal.Atom(x.NumVal!.ToString()!);
                if (x.IsStr)  return LVal.Atom(x.StrVal);
                return LVal.Err("Only symbols, strings and numbers can be converted to atoms");
            });

        // fun functions
        AddBuiltin(e, "fib",       FastFib);

        // helper
        AddBuiltin(e, "defined?",  (e, a) => LVal.Bool(a.Count > 0 && (IsDefined(e, a[0]) || (a[0].IsQExpr && a[0].Cells!.All(pp => IsDefined(e, pp))))));
        AddBuiltin(e, "type-of",   TypeOf);

        // type checking
        AddBuiltin(e, "t?",        IsT);
        AddBuiltin(e, "nil?",      IsNIL);
        AddBuiltin(e, "num?",      IsNumber);
        AddBuiltin(e, "fixed?",    IsFix);
        AddBuiltin(e, "rational?", IsRat);
        AddBuiltin(e, "int?",      IsInt);
        AddBuiltin(e, "complex?",  IsComplex);
        AddBuiltin(e, "atom?",     IsAtom);
        AddBuiltin(e, "symbol?",   IsSymbol);
        AddBuiltin(e, "string?",   IsString);
        AddBuiltin(e, "char?",     IsChar);
        AddBuiltin(e, "function?", IsFunc);
        AddBuiltin(e, "error?",    IsError);
        AddBuiltin(e, "expr?",     IsExpr);
        AddBuiltin(e, "qexpr?",    IsQExpr);
        AddBuiltin(e, "sexpr?",    IsSExpr);

        // Hash functions
        AddBuiltin(e, "hash-create",  HashCreate);
        AddBuiltin(e, "hash-get",     HashGet);
        AddBuiltin(e, "hash-put",     HashPut);
        AddBuiltin(e, "to#",          ToHash);
        AddBuiltin(e, "from#",        FromHash);
        AddBuiltin(e, "hash-key?",    HashHasKey);
        AddBuiltin(e, "hash-keys",    HashKeys);
        AddBuiltin(e, "hash-values",  HashValues);
        AddBuiltin(e, "hash-call",    HashCall);
        AddBuiltin(e, "hash-clone",   HashClone);
        AddBuiltin(e, "hash-remove",  HashRemove);

        // the system: files, programs, the environment, the clock, bits and bytes (SystemBuiltins.cs)
        AddSystemBuiltins(e);
        AddBuiltin(e, "hash-add-tag", HashAddTag);
        AddBuiltin(e, "hash-lock",         (e, a) => _HashApplyTag(e, a, LHash.TAG_LOCKED));
        AddBuiltin(e, "hash-make-const",   (e, a) => _HashApplyTag(e, a, LHash.TAG_RO));
        AddBuiltin(e, "hash-make-private", (e, a) => _HashApplyTag(e, a, LHash.TAG_PRIV));
        AddBuiltin(e, "hash-make-not-nil", (e, a) => _HashApplyTag(e, a, LHash.TAG_NOT_NIL));
        AddBuiltin(e, "hash-tag?",     HashHasTag);
        AddBuiltin(e, "hash-locked?",  HashIsLocked);
        AddBuiltin(e, "hash-private?", HashIsPrivate);
        AddBuiltin(e, "hash-const?",   HashIsConst);

        // the library's most used functions
        AddLibraryBuiltins(e);

        // buffers (BufferBuiltins.cs)
        AddBufferBuiltins(e);

        // the most used ones' fast ways, and the special forms the compiled code runs itself (FastBuiltins.cs)
        AddFastWays(e);
    }
}