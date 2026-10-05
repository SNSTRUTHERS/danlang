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
        ["write-bytes"] = (2, 2), ["platform"] = (0, 0), ["hydra?"] = (0, 0),
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

    // A built-in, its name and what it takes (Arity) on it
    private static LVal Described(LVal v, string name) {
        if (!Arity.TryGetValue(name, out var ar)) throw new Exception($"The built-in '{name}' has no Arity");
        v.BuiltinName = name;
        v.MinArgs = ar.Min;
        v.MaxArgs = ar.Max;
        v.IsSpecial = Special.Contains(name);
        return v;
    }

    private static void AddBuiltin(LEnv e, string name, Func<LEnv, LVal, LVal> func) {
        LVal k = LVal.Sym(name);
        LVal v = Described(LVal.Builtin(func), name);
        e.Put(k.SymVal!, v);
    }

    private static void AddBuiltinEvaluated(LEnv e, string name, Func<LEnv, LVal, LVal> func) {
        LVal k = LVal.Sym(name);
        var f = (LEnv env, LVal a) => {
            for (int i = 0; i < a.Count; i++) {
                var ev = a[i].Eval(env)!;
                if (ev.IsErr) return ev;
                a.Cells![i] = ev;
            }
            return func(env, a);
        };
        LVal v = Described(LVal.Builtin(f), name);
        e.Put(k.SymVal!, v);
    }

    private static LVal Lambda(LEnv e, LVal a) {
        LVal formals = a.Pop(0, e);
        LVal body = a.Pop(0, e);
        return LVal.Lambda(formals, body, e);
    }

    // (fexpr {formals} body): a function whose arguments come unevaluated, each in a Q-expression that remembers the
    // caller's scope, so (eval x) evaluates it there, if and when the function wants
    private static LVal Fexpr(LEnv e, LVal a) {
        var f = Lambda(e, a);
        f.IsFexpr = true;
        return f;
    }

    // (fun {name formals...} body): name defined (globally, as def does) as the function (fn {formals...} body), made
    // here (its closure: this scope)
    private static LVal Fun(LEnv e, LVal a) {
        if (a.Count != 2) return LVal.Err("'fun' expects {name formals...} and a body");
        var spec = a.Pop(0);    // (already evaluated: fun is AddBuiltinEvaluated)
        if (spec.IsErr) return spec;
        var body = a.Pop(0);
        if (body.IsErr) return body;
        if (!spec.IsQExpr || spec.Count == 0 || spec.Cells!.Any(c => !c.IsSym)) return LVal.Err("'fun' expects {name formals...} first");
        var name = spec.Pop(0).SymVal;
        e.Def(name, LVal.Lambda(spec, body, e));
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
        var v = a.Pop(0, e);
        if (v.IsErr) return v;
        if (!v.IsQExpr) return LVal.Err($"'{fn}' expects a QExpr");
        if (nonEmpty && v.Count == 0) return LVal.Err($"'{fn}' passed an empty list");
        return v;
    }

    // (A list made from another keeps the scope it was written in: Scope, which eval runs it in)
    private static LVal Head(LEnv e, LVal a) {
        var v = ListArg(e, a, "head", true);
        if (v.IsErr) return v;
        var ret = LVal.Qexpr();
        ret.Add(v.Pop(0));
        ret.Scope = v.Scope;
        return ret;
    }

    private static LVal Tail(LEnv e, LVal a) {
        LVal v = ListArg(e, a, "tail", true);
        if (v.IsErr) return v;
        v.Pop(0);
        return v;
    }

    private static LVal Init(LEnv e, LVal a) {
        var v = ListArg(e, a, "init", true);
        if (v.IsErr) return v;
        v.Pop(v.Count - 1);
        return v;
    }

    private static LVal End(LEnv e, LVal a) {
        var v = ListArg(e, a, "end", true);
        if (v.IsErr) return v;
        var ret = LVal.Qexpr();
        ret.Add(v.Pop(v.Count - 1));
        ret.Scope = v.Scope;
        return ret;
    }

    // (eval x): a Q-expression run as code, or an expression (an fexpr's argument: an S-expression or a symbol)
    // evaluated, in the scope it was written in (in tail position, as a function's last); anything else is itself
    private static TailStep EvalStep(LEnv e, LVal a) {
        if (a.Count != 1) return TailStep.Done(LVal.Err("Incorrect number of parameters passed to 'eval'"));
        LVal x =  a.Pop(0, e);
        if (x.IsErr) return TailStep.Done(x);
        var scope = x.Scope ?? e;
        if (x.IsQExpr) x.ValType = LVal.LE.SEXPR;
        else if (!x.IsSExpr && !x.IsSym) return TailStep.Done(x);
        return TailStep.Next(scope, x);
    }

    public static LVal Eval(LEnv e, LVal a) => EvalStep(e, a).Finish();

    private static LVal Join(LEnv e, LVal a) {
        if (a.Count == 0) return LVal.Err("Invalid number of parameters passed to 'join'");

        LVal? x = null;
        while (a.Count > 0) {
            var y = a.Pop(0, e);
            if (y.IsErr) return y;
            if (!y.IsQExpr) return LVal.Err("Invalid parameter passed to 'join'.  Expected QExpr.");
            else if (x == null) x = y;
            else {
                x.Scope ??= y.Scope;
                x = x.Join(y);
            }
        }
        return x!;
    }

    private static LVal Op(LEnv e, LVal a, string op) {
        LVal? x = null;
        while (a.Count > 0) {
            LVal y = a.Pop(0, e);
            if (y.IsErr) return y;
            if (!y.IsNum) return LVal.Err($"All parameters to operator '{op}' must be numbers.");
            if (x == null) {
                // one number: (- x) is its negation, (/ x) its reciprocal
                if (a.Count == 0 && op == "-") {
                    y.NumVal = -y.NumVal!;
                    return y;
                }
                if (a.Count == 0 && op == "/") {
                    if (y.NumVal!.IsZero) return LVal.Err("Division by zero.");
                    y.NumVal = (Num)new Int(BigInteger.One) / y.NumVal!;
                    return y;
                }

                // not negation? Just set x to y and continue;
                x = y;
                continue;
            }

            if (op == "-") x.NumVal = x.NumVal! - y.NumVal!;
            else if (op == "*") x.NumVal = x.NumVal! * y.NumVal!;
            else if (op == "/") {
                if (y.NumVal!.IsZero) {
                    return LVal.Err("Division by zero.");
                }
                x.NumVal = x.NumVal! / y.NumVal!;
            }
        }

        // If no arguments were passed, return 0 for "-", and 1 otherwise
        return x ?? LVal.Number(op == "-" ? BigInteger.Zero : BigInteger.One);
    }

    // (+ n...): the numbers' sum; or, the first a string (or a character), the values as print shows them, joined
    // ("n=5" for "n=" and 5); anything else is an error
    private static LVal Add(LEnv e, LVal a) {
        LVal? x = null;
        StringBuilder? sb = null;
        while (a.Count > 0) {
            LVal y = a.Pop(0, e);
            if (y.IsErr) return y;
            if (sb != null) sb.Append(y.ToDisplay());
            else if (x == null && (y.IsStr || y.IsChar)) sb = new StringBuilder(y.StrVal);
            else if (!y.IsNum) return LVal.Err("'+' adds numbers, or joins values to a string");
            else if (x == null) x = y;
            else x.NumVal = x.NumVal! + y.NumVal!;
        }

        if (sb != null) return LVal.Str(sb.ToString());
        return x ?? LVal.Number(BigInteger.Zero);
    }
    private static LVal Sub(LEnv e, LVal a) { return Op(e, a, "-"); }
    private static LVal Mul(LEnv e, LVal a) { return Op(e, a, "*"); }
    private static LVal Div(LEnv e, LVal a) { return Op(e, a, "/"); }

    private static LVal Var(LEnv e, LVal a, string func) {
        //if (a.Count == 0 || !a.IsQExpr) return LVal.Err($"Invalid parameter(s) passed to '{func}'");
        LVal syms = a.Pop(0);
        if (syms.IsSym) {
            var x = LVal.Qexpr();
            x.Add(syms);
            syms = x;
        }
        else if (syms.IsSExpr) syms = syms.Eval(e);

        if (syms.IsErr) return syms;
        if (!syms.IsQExpr || syms.Cells!.Any(v => !v.IsSym)) return LVal.Err($"'{func}' cannot define non-symbols");

        if (syms.Count != a.Count) return LVal.Err($"'{func}' passed too many arguments or symbols.  Expected {syms.Count}, got {a.Count}");
            
        while (syms.Count > 0 && a.Count > 0) {
            var symbol = syms.Pop(0).SymVal!;
            var value = a.Pop(0, e);
            if (func == "def") { e.Def(symbol, value); }
            if (func == "set") { e.Put(symbol, value); }
            if (func == "set!") {
                var r = e.Update(symbol, value);
                if (r.IsErr) return r;
            }
        }
        
        return LVal.NIL();
    }

    private static LVal Def(LEnv e, LVal a) { return Var(e, a, "def"); }
    private static LVal Put(LEnv e, LVal a) { return Var(e, a, "set"); }
    private static LVal Update(LEnv e, LVal a) { return Var(e, a, "set!"); }

    private static LVal Ord(LEnv e, LVal a, string op) {
        if (a.Count != 2) return LVal.Err($"Too few parameters passed to '{op}'");
        // if (!(a[0].IsNum && a[1].IsNum)) return LVal.Err($"'{op}' passed non-number parameter(s)");
        bool r = false;
        var cmp = Cmp(e, a, "cmp").NumVal!.ToInt().num;
        if (op == ">") { r = cmp > 0; } //((a[0].NumVal as Int)!.num > (a[1].NumVal as Int)!.num); }
        if (op == "<") { r = cmp < 0; } //((a[0].NumVal as Int)!.num < (a[1].NumVal as Int)!.num); }
        return LVal.Bool(r);
    }

    private static LVal Gt(LEnv e, LVal a) { return Ord(e, a, ">");  }
    private static LVal Lt(LEnv e, LVal a) { return Ord(e, a, "<");  }

    private static LVal Cmp(LEnv e, LVal a, string op) {
        if (a.Count != 2) return LVal.Err($"Too few parameters passed to '{op}'");
        bool r = false;
        if (op == "eq")       r =  a.Pop(0, e).Equals(a.Pop(0, e));
        else if (op == "neq") r = !a.Pop(0, e).Equals(a.Pop(0, e));
        else if (op == "cmp") return LVal.Number(a.Pop(0, e).CompareTo(a.Pop(0, e)));

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
                    if (IsMatch(cmp, c)) return c.Pop(c.Count - 1, e);
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
        if (a.Count == 0) return LVal.Err("'load' supplied too few parameters");

        LVal lastExpr = LVal.NIL();
        while (a.Count > 0) {
            var v = a.Pop(0, e);
            if (v.IsErr) return v;
            if (!v.IsStr) return LVal.Err("'load' passed non-string parameter(s)");
            var filename = FindFile(v.StrVal!);
            if (filename == null) return SysErr("noent", v.StrVal);

            var input = File.ReadAllText(filename);
            var tokens = Parser.Tokenize(new StringReader(input)).ToList();
            var bad = tokens.FirstOrDefault(t => t.type == Parser.Token.Type.Error || t.type == Parser.Token.Type.More);
            if (bad != null) return LVal.Err($"{filename}: {(bad.type == Parser.Token.Type.More ? $"missing {bad.parens}" : bad.str)}");
            var exprs = LVal.ReadExprFromTokens(tokens)!;

            // a file's expressions are top level: the global environment's
            while (exprs.Count > 0) {
                lastExpr = exprs.Pop(0, e.Root);
                if (lastExpr.IsErr) return LVal.Err($"{filename}: {lastExpr.ErrVal}");
                if (lastExpr.IsExit) throw new ExitException(lastExpr.ExitCode);
            }
        }
        return lastExpr;
    }

    public static LVal Save(LEnv e, LVal a) {
        if (a.Count == 0) return LVal.Err("'save' supplied too few parameters");
        var ob = a.Pop(0, e);
        if (ob.IsErr) return ob;

        if (a.Count > 0) {
            var filename = a.Pop(0, e);
            if (filename.IsErr) return filename;
            if (!filename.IsStr) return LVal.Err("Second parameter to 'save' must be a string");
            var s = new HashSet<string>();
            while (a.Count > 0) {
                var t = a.Pop(0, e);
                if (t.IsErr) return t;
                if (!t.IsAtom) return LVal.Err("Parameters 3+ for function 'save' must be atoms");
                s.Add(t.SymVal);
            }

            try {
                var fi = new FileInfo(filename.StrVal);
                if (fi.Directory == null) return LVal.Err("Folder for 'save' does not exist");
                if (!fi.Directory.Exists) fi.Directory.Create();
                FileStream? fs = null;
                if (fi.Exists) {
                    if (s.Contains("overwrite")) fs = fi.Open(FileMode.Truncate);
                    else if (s.Contains("append")) fs = fi.Open(FileMode.Append);
                    else return LVal.Err("File exists but neither ':append' nor ':overwrite' were specified");
                } else fs = fi.OpenWrite();

                fs.Write(UTF8Encoding.UTF8.GetBytes(ob.Serialize()));
                fs.Write(UTF8Encoding.UTF8.GetBytes(Environment.NewLine));
                fs.Flush();
                fs.Close();
            }
            catch (Exception ex) {
                return SysErr(ex, filename.StrVal);
            }
        }
        else {
            Console.Out.WriteLine(ob.Serialize());
        }

        return LVal.NIL();
    }

    // The values, as print shows them (a string's text as it is), each after sep, then end, to the stream
    private static LVal Output(LEnv e, LVal a, LStream s, string sep, string end) {
        var sb = new StringBuilder();
        var pre = "";
        while (a.Count > 0) {
            sb.Append(pre).Append(a.Pop(0, e).ToDisplay());
            pre = sep;
        }
        return s.Write(sb.Append(end).ToString());
    }

    private static LVal Print(LEnv e, LVal a) => Output(e, a, LStream.StdOut, " ", "\n");
    private static LVal Write(LEnv e, LVal a) => Output(e, a, LStream.StdOut, "", "");

    // (error message [code]): an error, with a code (an atom), if it's given one
    private static LVal Error(LEnv e, LVal a) {
        if (a.Count == 0 || a.Count > 2) return LVal.Err("'error' expects a message and maybe a code");
        var m = a.Pop(0, e);
        if (m.IsErr) return m;
        string? code = null;
        if (a.Count > 0) {
            var c = a.Pop(0, e);
            if (c.IsErr) return c;
            if (!c.IsAtom) return LVal.Err("An error's code must be an atom");
            code = c.SymVal;
        }
        return LVal.Err(m.ToDisplay(), code);
    }

    private static LVal IsType(LEnv e, LVal a, LVal.LE type) {
        return LVal.Bool(a.Pop(0, e).ValType == type);
    }

    private static LVal IsNumber(LEnv e, LVal a) => IsType(e, a, LVal.LE.NUM);
    private static LVal IsString(LEnv e, LVal a) => IsType(e, a, LVal.LE.STR);
    private static LVal IsChar(LEnv e, LVal a) => IsType(e, a, LVal.LE.CHAR);
    private static LVal IsAtom(LEnv e, LVal a) => IsType(e, a, LVal.LE.ATOM);
    private static LVal IsSymbol(LEnv e, LVal a) => LVal.Bool(a.Pop(0, e).IsSym);
    private static LVal IsFunc(LEnv e, LVal a) => IsType(e, a, LVal.LE.FUN);
    private static LVal IsError(LEnv e, LVal a) => IsType(e, a, LVal.LE.ERR);
    private static LVal IsQExpr(LEnv e, LVal a) => IsType(e, a, LVal.LE.QEXPR);
    private static LVal IsSExpr(LEnv e, LVal a) => LVal.Bool(a.Pop(0, e).IsSExpr);
    private static LVal IsNIL(LEnv e, LVal a) => LVal.Bool(a.Pop(0, e).IsNIL);
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
        var v = a.Pop(0, e);
        return LVal.Bool(v.IsNum && kind(v.NumVal!));
    }

    private static LVal IsInt(LEnv e, LVal a) => IsNumKind(e, a, n => n is Int && !(n is Fix) && !(n is Rat));
    private static LVal IsFix(LEnv e, LVal a) => IsNumKind(e, a, n => n is Fix);
    private static LVal IsRat(LEnv e, LVal a) => IsNumKind(e, a, n => n is Rat);
    private static LVal IsComplex(LEnv e, LVal a) => IsNumKind(e, a, n => n is Comp);

    private static LVal IsZero(LEnv e, LVal a) {
        if (a.Count != 1) return LVal.Err("'zero?' requires 1 parameter");
        var v = a.Pop(0, e);
        if (!v.IsNum) return LVal.Err("Parameter passed to 'zero?' is not a Number");
        return LVal.Bool(v.NumVal!.IsZero);
    }

    private static LVal Complex(LEnv e, LVal a) {
        if (a.Count != 2) return LVal.Err("Too few parameters passed to 'complex'");
        var re = a.Pop(0, e);
        if (re.IsErr) return re;
        var im = a.Pop(0, e);
        if (im.IsErr) return im;
        if (!re.IsNum || !im.IsNum) return LVal.Err("One or more parameters passed to 'complex' is not a Number");

        if (re.NumVal is Int r) {
            if (im.NumVal is Int i) return LVal.Number(new Comp(r, i));
            return LVal.Err("Imaginary part of complex must be an int, a rational, or a fixed");
        }
        return LVal.Err("Real part of complex must be an int, a rational, or a fixed");
    }

    private static LVal ToStr(LEnv e, LVal a) {
        if (a.Count < 1) return LVal.Err("Too few parameters passed to 'to-str'");

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
        if (a.Count < 2) return LVal.Err("Too few parameters passed to 'substring'");
        var str = a.Pop(0, e);
        if (str.IsErr) return str;
        if (!str.IsStr) return LVal.Err("First 'substring' parameter must be a String");
        var n = a.Pop(0, e);
        if (n.IsErr) return n;
        if (!n.IsNum) return LVal.Err("Second 'substring' parameter must be a Number");
        var s = str.StrVal;
        var index = n.NumVal!.ToInt().num;
        if (index < 0 || index > s.Length) return LVal.Err($"'substring': index {index} is outside the string");
        var rest = s.Length - (int)index;
        if (a.Count == 0) return LVal.Str(s.Substring((int)index));

        var l = a.Pop(0, e);
        if (l.IsErr) return l;
        if (!l.IsNum) return LVal.Err("Third 'substring' parameter must be a Number");
        var len = l.NumVal!.ToInt().num;
        if (len < 0) return LVal.Err("'substring': a count can't be negative");
        return LVal.Str(s.Substring((int)index, len > rest ? rest : (int)len));
    }

    private static LVal Split(LEnv e, LVal a) {
        if (a.Count < 1) return LVal.Err("Too few parameters passed to 'str-split'");
        var str = a.Pop(0, e);
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

        var sep = a.Pop(0, e);
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
        if (a.Count != 2) return LVal.Err("'char-at' expects a String and an index");
        var s = StrArg(e, a, "char-at");
        if (s.IsErr) return s;
        var i = a.Pop(0, e);
        if (i.IsErr) return i;
        if (!i.IsNum) return LVal.Err("Second 'char-at' parameter must be a Number");
        var index = (int)i.NumVal!.ToInt().num;
        if (index < 0 || index >= s.StrVal.Length) return LVal.Err($"'char-at': index {index} is outside the string");
        return Chr(s.StrVal[index]);
    }

    // (subset list i [n]): the list's items from index i (0 on; its length too, for NIL), n of them (as many as there
    // are), or the rest; an index outside the list, or a count below 0, is an error
    private static LVal Subset(LEnv e, LVal a) {
        if (a.Count < 2) return LVal.Err("Too few parameters passed to 'subset'");

        var q = a.Pop(0, e);
        if (q.IsErr) return q;
        if (!q.IsQExpr) return LVal.Err("First 'subset' parameter must be a QExpr");

        var i = a.Pop(0, e);
        if (i.IsErr) return i;
        if (!i.IsNum) return LVal.Err("Second 'subset' parameter must be a Number");
        var index = i.NumVal!.ToInt().num;
        if (index < 0 || index > q.Count) return LVal.Err($"'subset': the list has no item {index}");
        var count = q.Count - (int)index;
        if (a.Count > 0) {
            var l = a.Pop(0, e);
            if (l.IsErr) return l;
            if (!l.IsNum) return LVal.Err("Third 'subset' parameter must be a Number");
            var n = l.NumVal!.ToInt().num;
            if (n < 0) return LVal.Err("'subset': a count can't be negative");
            if (n < count) count = (int)n;
        }
        var ret = LVal.Qexpr();
        foreach (var c in q.Cells!.Skip((int)index).Take(count)) ret.Add(c.Copy());
        return ret;
    }

    // (item-at list i): the list's item i (0 on), as it's written; an index outside the list is an error
    private static LVal ItemAt(LEnv e, LVal a) {
        if (a.Count < 2) return LVal.Err("Too few parameters passed to 'item-at'");

        var q = a.Pop(0, e);
        if (q.IsErr) return q;
        if (!q.IsQExpr) return LVal.Err("First 'item-at' parameter must be a QExpr");

        var i = a.Pop(0, e);
        if (i.IsErr) return i;
        if (!i.IsNum) return LVal.Err("Second 'item-at' parameter must be a Number");
        var index = i.NumVal!.ToInt().num;
        if (index < 0 || index >= q.Count) return LVal.Err($"'item-at': the list has no item {index}");
        return q[(int)index].Copy();
    }

    private static LVal FastFib(LEnv e, LVal val) {
        if (val.Count == 0) return LVal.Err("Too few parameters passed to 'fib'");
        var f = val.Pop(0, e);
        if (!f.IsNum) return LVal.Err("'fib' expects one parameter of type Number");
        var v = f.NumVal!.ToInt().num;
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
        if (val.Count > 0) return LVal.Hash(val.Pop(0, e), e);
        return LVal.Hash();
    }

    private static LVal HashGet(LEnv e, LVal val) {
        if (val.Count < 2) return LVal.Err("'hash-get' requires two parameters");
        if (val.Count > 2) return LVal.Err($"Too many parameters passed to 'hash-get'.  Expected 2, got {val.Count}");
        var hash = val.Pop(0, e);
        if (hash.IsNIL) return LVal.NIL();
        if (!hash.IsHash) return LVal.Err("First parameter to 'hash-get' must be a hash");

        var key = val.Pop(0, e);
        return hash.HashValue!.Get(key);
    }

    private static LVal HashPut(LEnv e, LVal val) {
        if (val.Count < 2) return LVal.Err("'hash-put' requires two or more parameters");

        var hash = val.Pop(0, e);
        if (hash.IsErr) return hash;
        if (!hash.IsHash) return LVal.Err("First parameter to 'hash-put' must be a hash");

        // an entry, {key value tag...}: its value evaluated (with tags or without)
        LVal Put(LVal p) {
            if (p.IsErr) return p;
            if (!p.IsQExpr || p.Count < 2) return hash.HashValue!.Put(p);
            var v = p[1].Eval(e);
            if (v.IsErr) return v;
            var entry = p.Copy();
            entry.Cells![1] = v;
            return hash.HashValue!.Put(entry);
        }

        if (val.Count == 1) return Put(val.Pop(0, e));

        var ret = LVal.Qexpr();
        while (val.Count > 0) ret.Add(Put(val.Pop(0, e)));

        return ret;
    }

    private static LVal ToHash(LEnv e, LVal val) {
        if (val.Count < 1) return LVal.Err("'to#' requires one parameters");
        var hash = val.Pop(0, e);
        if (!hash.IsQExpr) return LVal.Err("First parameter to 'to#' must be a QExpr");
        return LVal.Hash(hash, e);
    }

    private static LVal FromHash(LEnv e, LVal val) {
        if (val.Count < 1) return LVal.Err("'from#' requires one parameters");
        var hash = val.Pop(0, e);
        if (!hash.IsHash) return LVal.Err("First parameter to 'from#' must be a hash");
        return hash.HashValue!.ToQexpr();
    }

    private static LVal HashHasKey(LEnv e, LVal val) {
        if (val.Count < 2) return LVal.Err("'hash-key?' requires two parameters");
        if (val.Count > 2) return LVal.Err($"Too many parameters passed to 'hash-key?'.  Expected 2, got {val.Count}");
        var hash = val.Pop(0, e);
        if (!hash.IsHash) return LVal.Err("First parameter to 'hash-key?' must be a hash");

        var key = val.Pop(0, e);
        return hash.HashValue!.ContainsKey(key);
    }

    private static LVal HashKeys(LEnv e, LVal val) {
        if (val.Count < 1) return LVal.Err("'hash-keys' requires one parameter");
        var hash = val.Pop(0, e);
        if (!hash.IsHash) return LVal.Err("First parameter to 'hash-keys' must be a hash");
        return hash.HashValue!.Keys;
    }

    private static LVal HashValues(LEnv e, LVal val) {
        if (val.Count < 1) return LVal.Err("'hash-values' requires one parameter");
        var hash = val.Pop(0, e);
        if (!hash.IsHash) return LVal.Err("First parameter to 'hash-values' must be a hash");
        return hash.HashValue!.Values;
    }

    private static LVal HashCall(LEnv e, LVal val) {
        // TODO: check the parameters
        if (val.Count < 2) return LVal.Err("'hash-call' requires two parameters");
        var hash = val.Pop(0, e);
        if (!hash.IsHash) return LVal.Err("First parameter to 'hash-call' must be a hash");
        var fKey = val.Pop(0, e);

        var f = hash.HashValue!.Get(fKey);
        if (f.IsErr) return f;
        if (!f.IsFun) return LVal.Err("Second parameter to 'hash-call' must be a key to a member function");

        // &0, the hash, in the method's own scope
        f.Env?.Put("&0", LVal.Hash(hash.HashValue.PrivateCallProxy));
        var retVal = LVal.Call(e, f, val);
        return retVal;
    }

    private static LVal HashClone(LEnv e, LVal val) {
        if (val.Count < 1) return LVal.Err("'hash-clone' requires one parameters");
        var hash = val.Pop(0, e);
        if (!hash.IsHash) return LVal.Err("First parameter to 'hash-clone' must be a hash");
        return LVal.Hash(hash.HashValue!.Clone(val));
    }

    private static LVal HashAddTag(LEnv e, LVal val) {
        if (val.Count < 2) return LVal.Err("'hash-add-tag' requires two or more parameters");
        var hash = val.Pop(0, e);
        if (!hash.IsHash) return LVal.Err("First parameter to 'hash-add-tag' must be a hash");

        if (val.Count == 1) return hash.HashValue!.AddTag(val.Pop(0, e));

        var ret = LVal.Qexpr();
        while (val.Count > 0) ret.Add(hash.HashValue!.AddTag(val.Pop(0, e)));

        return ret;
    }

    private static LVal _HashApplyTag(LEnv e, LVal val, string tag) {
        var a = LVal.Atom(tag);
        if (val.Count > 1) {
            var v = LVal.Qexpr();
            v.Add(val.Pop(0, e));
            while (val.Count > 0) {
                var q = LVal.Qexpr();
                q.Add(val.Pop(0, e));
                q.Add(a);
                v.Add(q);
            }
            val = v;
        }
        else val.Add(a);
        return HashAddTag(e, val);
    }

    private static LVal HashHasTag(LEnv e, LVal val) {
        if (val.Count < 2) return LVal.Err("'hash-tag?' requires two parameters");
        if (val.Count > 2) return LVal.Err($"Too many parameters passed to 'hash-tag?'.  Expected 2, got {val.Count}");
        var hash = val.Pop(0, e);
        if (!hash.IsHash) return LVal.Err("First parameter to 'hash-tag?' must be a hash");

        return hash.HashValue!.HasTag(val.Pop(0, e));
    }

    // (hash-locked? h [key]), (hash-private? ...), (hash-const? ...): whether the hash, or its entry, has the tag
    private static LVal HashHasNamedTag(LEnv e, LVal val, string fn, string tag) {
        if (val.Count < 1 || val.Count > 2) return LVal.Err($"'{fn}' expects a hash and maybe a key");
        var hash = val.Pop(0, e);
        if (hash.IsErr) return hash;
        if (!hash.IsHash) return LVal.Err($"First parameter to '{fn}' must be a hash");
        if (val.Count == 0) return hash.HashValue!.HasTag(LVal.Atom(tag));
        var key = val.Pop(0, e);
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
    private static LVal Chr(char c) {
        var v = LVal.Character("x");
        v.StrVal = c.ToString();
        return v;
    }

    // A symbol named s, as it is (LVal.Sym reads :x as an atom, \x as a character, nil as NIL ...)
    private static LVal PlainSym(string s) {
        var v = LVal.Sym("x");
        v.SymVal = s.ToLower();
        return v;
    }

    // A string argument for fn (a character counts as a string of one), evaluated; or an error
    private static LVal StrArg(LEnv e, LVal a, string fn) {
        if (a.Count == 0) return LVal.Err($"Too few parameters passed to '{fn}'");
        var v = a.Pop(0, e);
        if (v.IsErr) return v;
        if (!v.IsStr && !v.IsChar) return LVal.Err($"'{fn}' expects a String");
        return v;
    }

    // A number argument for fn, evaluated, as an int; or an error
    private static LVal IntArg(LEnv e, LVal a, string fn, out int n) {
        n = 0;
        if (a.Count == 0) return LVal.Err($"Too few parameters passed to '{fn}'");
        var v = a.Pop(0, e);
        if (v.IsErr) return v;
        if (!v.IsNum) return LVal.Err($"'{fn}' expects a Number");
        n = (int)v.NumVal!.ToInt().num;
        return v;
    }

    // A value run as code: a Q-expression's contents evaluated (as eval does: in the scope it was written in, or
    // e), anything else as it is
    private static LVal RunCode(LEnv e, LVal v) {
        if (!v.IsQExpr) return v;
        var x = v.Copy();
        x.ValType = LVal.LE.SEXPR;
        return x.Eval(v.Scope ?? e);
    }

    // An unevaluated argument evaluated: a Q-expression run as code (here), anything else evaluated
    private static LVal EvalArg(LEnv e, LVal x) => x.IsQExpr ? RunCode(e, x) : x.Copy().Eval(e);

    // f called with values as they are, not evaluated again: a function's are bound as they are, a built-in's passed
    // through names of their own
    public static LVal Apply(LEnv e, LVal f, params LVal[] args) {
        if (f.BuiltinVal == null) return LVal.Apply(f.Copy(), args.ToList());
        var env = new LEnv(e);
        var call = LVal.Sexpr();
        for (int i = 0; i < args.Length; i++) {
            env.Put($"&arg{i}", args[i]);
            call.Add(PlainSym($"&arg{i}"));
        }
        return LVal.Call(env, f.Copy(), call);
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
        if (a.Count != 1) return LVal.Err("'error-message' requires 1 parameter");
        var v = a.Pop(0, e);
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
                var v = b.Count == 2 ? b[1].Copy().Eval(scope) : LVal.NIL();
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
        var x = last.Copy();
        x.ValType = LVal.LE.SEXPR;
        return TailStep.Next(last.Scope ?? scope, x);
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
        return spec[1].Copy().Eval(e);
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
        if (n.IsErr) return n;
        if (!n.IsNum) return LVal.Err("'dotimes' expects a Number");
        var count = n.NumVal!.ToInt().num;
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
            if (s.ContainsKey(v.SymVal)) return true;
            if (s.ContainsKey("&_")) break;
        }
        return false;
    }

    // (type-of x): an atom naming x's type
    private static LVal TypeOf(LEnv e, LVal a) {
        if (a.Count != 1) return LVal.Err("'type-of' requires 1 parameter");
        var v = a.Pop(0, e);
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
            LVal.LE.EXIT   => "exit",
            _              => "unknown"
        });
    }

    // (read text): the expressions in the text, unevaluated, as a list: (eval (read "+ 1 2")) is 3
    private static LVal Read(LEnv e, LVal a) {
        if (a.Count != 1) return LVal.Err("'read' requires 1 parameter");
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
        if (a.Count != 1) return LVal.Err("'reverse' requires 1 parameter");
        var v = a.Pop(0, e);
        if (v.IsErr) return v;
        if (v.IsStr) return LVal.Str(new string(v.StrVal.Reverse().ToArray()));
        if (!v.IsQExpr) return LVal.Err("'reverse' expects a QExpr or a String");
        v.Cells!.Reverse();
        return v;
    }

    // (range n), (range from to) or (range from to step): the numbers from 0 (or from) up to, not including, to; down
    // to it, with a negative step
    private static LVal Range(LEnv e, LVal a) {
        if (a.Count < 1 || a.Count > 3) return LVal.Err("'range' expects 1 to 3 Numbers");
        var n = new List<Num>();
        while (a.Count > 0) {
            var v = a.Pop(0, e);
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
        if (a.Count < 1 || a.Count > 2) return LVal.Err("'sort' expects a list and maybe a function");
        var l = ListArg(e, a, "sort");
        if (l.IsErr) return l;
        LVal? less = null;
        if (a.Count > 0) {
            less = a.Pop(0, e);
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
        if (a.Count != 2) return LVal.Err($"'{fn}' expects 2 parameters");
        var s = a.Pop(0, e);
        if (s.IsErr) return s;
        var x = a.Pop(0, e);
        if (x.IsErr) return x;
        if (s.IsQExpr) return LVal.Number(last ? s.Cells!.FindLastIndex(c => c.Equals(x)) : s.Cells!.FindIndex(c => c.Equals(x)));
        if (!s.IsStr || !(x.IsStr || x.IsChar)) return LVal.Err($"'{fn}' expects a String and a String");
        return LVal.Number(last ? s.StrVal.LastIndexOf(x.StrVal, StringComparison.Ordinal) : s.StrVal.IndexOf(x.StrVal, StringComparison.Ordinal));
    }

    // ---- Strings and characters

    // A string made from one (a character stays a character, if the result is one)
    private static LVal StrMap(LEnv e, LVal a, string fn, Func<string, string> f) {
        if (a.Count != 1) return LVal.Err($"'{fn}' requires 1 parameter");
        var s = StrArg(e, a, fn);
        if (s.IsErr) return s;
        var r = f(s.StrVal);
        return s.IsChar && r.Length == 1 ? Chr(r[0]) : LVal.Str(r);
    }

    // (str-replace s old new): s with each old new
    private static LVal StrReplace(LEnv e, LVal a) {
        if (a.Count != 3) return LVal.Err("'str-replace' expects 3 Strings");
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
        if (a.Count < 1 || a.Count > 2) return LVal.Err("'str-join' expects a list and maybe a separator");
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
        if (a.Count != 1) return LVal.Err("'str-chars' requires 1 parameter");
        var s = StrArg(e, a, "str-chars");
        if (s.IsErr) return s;
        var ret = LVal.Qexpr();
        foreach (var c in s.StrVal) ret.Add(Chr(c));
        return ret;
    }

    // (str-pad-left s n [c]) and (str-pad-right s n [c]): s made n long with c (a space) before or after it
    private static LVal StrPad(LEnv e, LVal a, string fn, bool left) {
        if (a.Count < 2 || a.Count > 3) return LVal.Err($"'{fn}' expects a String, a length and maybe a character");
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
        if (a.Count != 2) return LVal.Err("'str-repeat' expects a String and a Number");
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
                var v = a.Pop(0, e);
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
        if (a.Count != 1) return LVal.Err("'char-code' requires 1 parameter");
        var c = StrArg(e, a, "char-code");
        if (c.IsErr) return c;
        if (c.StrVal.Length != 1) return LVal.Err("'char-code' expects one character");
        return LVal.Number(c.StrVal[0]);
    }

    // (code-char n): the character with that code
    private static LVal CodeChar(LEnv e, LVal a) {
        if (a.Count != 1) return LVal.Err("'code-char' requires 1 parameter");
        var v = IntArg(e, a, "code-char", out var n);
        if (v.IsErr) return v;
        if (n < 0 || n > 0xFFFF) return LVal.Err($"'code-char': {n} isn't a character's code");
        return Chr((char)n);
    }

    // alpha? digit? space? upper? lower?: whether a character is one (or every character of a string that isn't empty)
    private static LVal CharTest(LEnv e, LVal a, string fn, Func<char, bool> test) {
        if (a.Count != 1) return LVal.Err($"'{fn}' requires 1 parameter");
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
        if (a.Count < 1 || a.Count > 2) return LVal.Err("'open' expects a path and maybe a mode");
        var p = StrArg(e, a, "open");
        if (p.IsErr) return p;
        var mode = "read";
        if (a.Count > 0) {
            var m = a.Pop(0, e);
            if (m.IsErr) return m;
            if (!m.IsAtom) return LVal.Err("'open' mode must be :read, :write or :append");
            mode = m.SymVal;
        }
        if (mode != "read" && mode != "write" && mode != "append") return LVal.Err($"'open' mode must be :read, :write or :append, not :{mode}");
        if (Directory.Exists(p.StrVal)) return SysErr("isdir", p.StrVal);
        try {
            Stream s = mode switch {
                "read"   => File.OpenRead(p.StrVal),
                "write"  => File.Create(p.StrVal),
                _        => new FileStream(p.StrVal, FileMode.Append, FileAccess.Write),
            };
            return LVal.Stream(s);
        }
        catch (Exception ex) {
            return SysErr(ex, p.StrVal);
        }
    }

    // A stream's operation: the stream, then n more arguments, evaluated
    private static LVal StreamOp(LEnv e, LVal a, string fn, int n, Func<LStream, LVal[], LVal> op) {
        if (a.Count != n + 1) return LVal.Err($"'{fn}' expects a stream{(n > 0 ? $" and {n} more" : "")}");
        var s = a.Pop(0, e);
        if (s.IsErr) return s;
        if (!s.IsStream) return LVal.Err($"'{fn}' expects a stream");
        var args = new LVal[n];
        for (int i = 0; i < n; i++) {
            args[i] = a.Pop(0, e);
            if (args[i].IsErr) return args[i];
        }
        return op(s.StreamValue!, args);
    }

    // (print-to s args...) and (write-to s args...): print's and write's output, to a stream
    private static LVal OutputTo(LEnv e, LVal a, string fn, string sep, string end) {
        if (a.Count < 1) return LVal.Err($"'{fn}' expects a stream");
        var s = a.Pop(0, e);
        if (s.IsErr) return s;
        if (!s.IsStream) return LVal.Err($"'{fn}' expects a stream first");
        return Output(e, a, s.StreamValue!, sep, end);
    }

    // ---- Hashes

    // (hash-remove h key): the entry taken out of the hash; its value, or NIL if it had none
    private static LVal HashRemove(LEnv e, LVal val) {
        if (val.Count != 2) return LVal.Err("'hash-remove' requires two parameters");
        var hash = val.Pop(0, e);
        if (hash.IsErr) return hash;
        if (!hash.IsHash) return LVal.Err("First parameter to 'hash-remove' must be a hash");
        var key = val.Pop(0, e);
        if (key.IsErr) return key;
        return hash.HashValue!.Remove(key);
    }


    static private Random _rand = new Random();

    // (random n): an integer from 0 up to, not including, n (an integer, 1 or more, of any size)
    private static LVal RandomBelow(LEnv e, LVal a) {
        var v = a.Pop(0, e);
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
        AddBuiltinEvaluated(e, "fun", Fun);
        AddBuiltin(e, "def", Def);
        AddBuiltin(e, "set", Put);
        AddBuiltin(e, "set!", Update);
        AddTailForm(e, "let", LetStep);
        AddTailForm(e, "do", DoStep);
        AddBuiltin(e, "fexpr", Fexpr);
        AddBuiltin(e, "gensym", Gensym);
        
        // list functions
        AddBuiltinEvaluated(e, "list", List);
        AddBuiltin(e, "head", Head);
        AddBuiltin(e, "tail", Tail);
        AddBuiltin(e, "init", Init);
        AddBuiltin(e, "end",  End);
        AddBuiltin(e, "join", Join);
        AddTailForm(e, "eval", EvalStep);
        AddBuiltinEvaluated(e, "len",  (e, a) => a.Count != 1 ? LVal.Err("'len' requires 1 parameter") : a[0].ValType switch {
            LVal.LE.QEXPR => LVal.Number(a[0].Count),
            LVal.LE.STR   => LVal.Number(a[0].StrVal.Length),
            LVal.LE.HASH  => LVal.Number(a[0].HashValue!.Count),
            LVal.LE.ERR   => a[0],
            _             => LVal.Err("'len' requires parameter of type string, list or hash")
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

        AddBuiltinEvaluated(e, "rational.n", (e, a) => a.Count > 0 ?
            (a[0].IsNum ? LVal.Number(Rat.ToRat(a[0].NumVal!).num) : LVal.Err("Argument is not a number")) :
            LVal.Err("One or more arguments required"));

        AddBuiltinEvaluated(e, "rational.d", (e, a) => a.Count > 0 ?
            (a[0].IsNum ? LVal.Number(Rat.ToRat(a[0].NumVal!).den) : LVal.Err("Argument is not a number")) :
            LVal.Err("One or more arguments required"));

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
            if (a.Count != 1) return LVal.Err("'error-code' requires 1 parameter");
            var v = a.Pop(0, e);
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
        AddBuiltin(e, "str-upper", (e, a) => StrMap(e, a, "str-upper", s => s.ToUpperInvariant()));
        AddBuiltin(e, "str-lower", (e, a) => StrMap(e, a, "str-lower", s => s.ToLowerInvariant()));
        AddBuiltin(e, "str-trim",  (e, a) => StrMap(e, a, "str-trim", s => s.Trim()));
        AddBuiltin(e, "str-replace", StrReplace);
        AddBuiltin(e, "str-join", StrJoin);
        AddBuiltin(e, "str-chars", StrChars);
        AddBuiltin(e, "str-pad-left",  (e, a) => StrPad(e, a, "str-pad-left", true));
        AddBuiltin(e, "str-pad-right", (e, a) => StrPad(e, a, "str-pad-right", false));
        AddBuiltin(e, "str-repeat", StrRepeat);
        AddBuiltin(e, "format", Format);
        AddBuiltin(e, "char-code", CharCode);
        AddBuiltin(e, "code-char", CodeChar);
        AddBuiltin(e, "alpha?", (e, a) => CharTest(e, a, "alpha?", char.IsLetter));
        AddBuiltin(e, "digit?", (e, a) => CharTest(e, a, "digit?", c => c >= '0' && c <= '9'));
        AddBuiltin(e, "space?", (e, a) => CharTest(e, a, "space?", char.IsWhiteSpace));
        AddBuiltin(e, "upper?", (e, a) => CharTest(e, a, "upper?", char.IsUpper));
        AddBuiltin(e, "lower?", (e, a) => CharTest(e, a, "lower?", char.IsLower));

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
        AddBuiltinEvaluated(e, "to-fixed", (e, a) => a.Count < 1 || !a[0].IsNum ? LVal.Err("'to-fixed' expects a Number") :
            LVal.Number(Rat.ToRat(a[0].NumVal!).ToFix(a.Count > 1 ? (int)(((a[1].NumVal as Int)?.num ?? BigInteger.Zero)) : 10)));
        AddBuiltinEvaluated(e, "to-rational", (e, a) => a.Count != 1 || !a[0].IsNum ? LVal.Err("'to-rational' expects a Number") :
            LVal.Number(Rat.ToRat(a[0].NumVal!)));
        AddBuiltinEvaluated(e, "truncate", (e, a) => a.Count != 1 || !a[0].IsNum ? LVal.Err("'truncate' expects a Number") :
            LVal.Number(a[0].NumVal!.ToInt()));
        AddBuiltin(e, "complex",     Complex);
        AddBuiltinEvaluated(e, "to-str", ToStr);
        // (repr x): x as the REPL shows it (a string in quotes, with escapes), an error too ("Error: ...")
        AddBuiltin(e, "repr", (e, a) => a.Count != 1 ? LVal.Err("'repr' requires 1 parameter") : LVal.Str(a.Pop(0, e).ToStr()));
        AddBuiltin(e, "to-sym",
            (e, a) => {
                var x = a.Pop(0, e);
                if (x.IsSym)  return x;
                if (x.IsAtom) return LVal.Sym(x.SymVal);
                if (x.IsStr)  return PlainSym(x.StrVal);
                return LVal.Err("Only atoms and strings can be converted into symbols");
            });

        AddBuiltinEvaluated(e, "to-atom",
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
    }
}