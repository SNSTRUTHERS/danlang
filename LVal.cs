using System.Numerics;
using System.Text;

// (exit code): thrown to the top (Program), past every built-in
public class ExitException : Exception {
    public int Code;
    public ExitException(int code) : base($"exit {code}") => Code = code;
}

// A built-in's step in tail position (LVal.TailForm): its value, or the expression to evaluate next, and where
public struct TailStep {
    public LVal? Value;
    public LEnv? Env;
    public LVal? Expr;
    public static TailStep Done(LVal v) => new TailStep { Value = v };
    public static TailStep Next(LEnv e, LVal x) => new TailStep { Env = e, Expr = x };

    // The step finished, outside tail position: the expression evaluated, if there is one
    public LVal Finish() => Expr == null ? Value! : Expr.Eval(Env!);
}

public class LVal {
    public enum LE { ERR, T, NUM, ATOM, SYM, CHAR, STR, FUN, SEXPR, QEXPR, HASH, STREAM, COMMENT, EXIT, TAIL };

    public LE ValType;
    public LEnv? Env = null;        // a lambda's own scope: the arguments given it so far
    public LEnv? Closure = null;    // a lambda's: the scope it was made in, its calls' parent (lexical scope)
    public bool IsFexpr = false;    // a lambda's: its arguments come unevaluated, each a Q-expression (fexpr)
    public LEnv? Scope = null;      // a Q-expression's: the scope it was written in, where eval runs it
    public Func<LEnv, LVal, TailStep>? TailForm = null;    // a built-in's, if it has a tail position (if, do ...)
    public int ExitCode = 0;
    public LVal? Formals = null;
    public LVal? Body = null;
    public Num? NumVal = null;
    public Func<LEnv, LVal, LVal>? BuiltinVal = null;
    public string? BuiltinName = null;  // a built-in's name ...
    public int MinArgs = 0;             //   the arguments it needs (fewer: partial application) ...
    public int MaxArgs = int.MaxValue;  //   the most it takes (more: an error) ...
    public bool IsSpecial = false;      //   a special form (its arguments as they're written): never partial ...
    public bool TakesErrors = false;    //   given an error as a value, not stopped by it (error?, type-of ...) ...
    public List<LVal>? Bound = null;    //   and, partially applied, the values given it so far
    public string ErrVal = string.Empty;
    public string? ErrCode = null;
    public string SymVal = string.Empty;
    public string StrVal = string.Empty;
    public LHash? HashValue = null;
    public LStream? StreamValue = null;
    public List<LVal>? Cells = null;

    public int Count => Cells?.Count ?? 0;
    public bool IsNIL => Count == 0 && (IsSExpr || IsQExpr);
    public bool IsT => ValType == LE.T;
    public bool IsNum => ValType == LE.NUM;
    public bool IsAtom => ValType == LE.ATOM;
    public bool IsSym => ValType == LE.SYM;
    public bool IsFun => ValType == LE.FUN;
    public bool IsErr => ValType == LE.ERR;
    public bool IsExit => ValType == LE.EXIT;
    public bool IsStr => ValType == LE.STR;
    public bool IsChar => ValType == LE.CHAR;
    public bool IsSExpr => ValType == LE.SEXPR;
    public bool IsQExpr => ValType == LE.QEXPR;
    public bool IsHash => ValType == LE.HASH;
    public bool IsStream => ValType == LE.STREAM;
    public bool IsComment => ValType == LE.COMMENT;

    //  Hashes
    public LVal Copy() {
        if (IsHash || IsStream) return this;
        LVal x = new LVal();
        x.ValType = ValType;
        switch (ValType) {
            case LE.FUN:
                if (BuiltinVal != null) {
                    x.BuiltinVal = BuiltinVal;
                    x.TailForm = TailForm;
                    x.BuiltinName = BuiltinName;
                    x.MinArgs = MinArgs;
                    x.MaxArgs = MaxArgs;
                    x.IsSpecial = IsSpecial;
                    x.TakesErrors = TakesErrors;
                    x.Bound = Bound == null ? null : new List<LVal>(Bound);
                } else {
                    x.BuiltinVal = null;
                    x.Env = Env?.Copy();
                    x.Closure = Closure;
                    x.IsFexpr = IsFexpr;
                    x.Formals = Formals?.Copy();
                    x.Body = Body?.Copy();
                }
                break;

            case LE.NUM: x.NumVal = NumVal; break;
            case LE.ERR: x.ErrVal = ErrVal; x.ErrCode = ErrCode; break;
            case LE.EXIT: x.ExitCode = ExitCode; break;

            case LE.ATOM:
            case LE.SYM: x.SymVal = SymVal; break;

            case LE.CHAR:
            case LE.STR: x.StrVal = StrVal; break;

            case LE.SEXPR:
            case LE.QEXPR:
                x.Cells = Cells?.Select(c => c.Copy()).ToList();
                break;
        }
        x.Scope = Scope;
        return x;
    }

    public static LVal T() {
        LVal v = new LVal();
        v.ValType = LE.T;
        return v;
    }

    public static LVal NIL() => Qexpr();
    public static LVal Bool(bool b) => b ? T() : NIL();

    public static LVal Number(int x) => Number(new Int(x));
    public static LVal Number(BigInteger x) => Number(new Int(x));
    public static LVal Number(Num x) {
        LVal v = new LVal();
        v.ValType = LE.NUM;
        v.NumVal = x;
        return v;
    }

    // An error: its message, and its code, if it has one (an atom's name: the Hydra's error codes, noent ...)
    public static LVal Err(string errStr, string? code = null) {
        LVal v = new LVal();
        v.ValType = LE.ERR;
        v.ErrVal = errStr;
        v.ErrCode = code;
        return v;
    }

    public static LVal Atom(string s) {
        LVal v = new LVal();
        v.ValType = LE.ATOM;
        v.SymVal = s.ToLower(); // TODO: assess if case-sensitive atoms would be useful
        return v;
    }

    // A character, \x or \name (a name CharOf knows)
    public static LVal Character(string s) {
        LVal v = new LVal();
        v.ValType = LE.CHAR;
        v.StrVal = CharOf(s) ?? throw new ArgumentException($"Unknown character name \\{s}");
        return v;
    }

    // The character a name names: one character itself, or a name for one; null if it isn't one
    public static string? CharOf(string s) {
        if (s.Length == 1) return s;
        return s.ToLower().Replace("-", "") switch {
            "backslash" or "bslash" or "bs" => "\\",
            "backtick" or "btick" or "bt" => "`",
            "bell" => "\a",
            "backspace" or "bksp" => "\b",
            "linefeed" or "newline" or "lf" or "nl" => "\n",
            "formfeed" or "ff" => "\f",
            "null" => "\0",
            "quote" or "doublequote" => "\"",
            "return" or "carriagereturn" or "cr"=> "\r",
            "rparen" or "rightparen" or "rp" => ")",
            "lparen" or "leftparen" or "lp" => "(",
            "rbrace" or "rightcurly" or "rcurly" or "rc" => "}",
            "lbrace" or "leftcurly" or "lcurly" or "lc" => "{",
            "rbracket" or "rightbracket" or "rb" => "]",
            "lbracket" or "leftbracket" or "lb" => "[",
            "slash" or "sl" => "/",
            "space" or "sp" => " ",
            "tab" => "\t",
            "tick" or "singlequote"=> "'",
            "tilde" => "~",
            "verticaltab" or "vtab" or "vt" or "verticalspace" or "vspace" or "vs" => "\v",
            "bang" or "warning" or "exclaim" or "exclamation" or "exclamationpoint" => "!",
            "at" => "@",
            "poundsign" or "pound" or "hash" => "#",
            "dollarsign" or "dollar" or "dollars" or "ds" => "$",
            "percentsign" or "percent" or "mod" or "modulo" or "modulus" or "pc" => "%",
            "caret" or "uparrow" => "^",
            "amp" or "and" or "ampersand" => "&",
            "pipe" or "or" or "vbar" or "verticalbar" or "vb" => "|",
            "star" or "splat" or "mult" or "st" => "*",
            "gt" or "greater" or "greaterthan" => ">",
            "lt" or "less" or "lessthan" => "<",
            "comma" => ",",
            "colon" => ":",
            "semicolon" or "semi" or "sc" => ";",
            "dot" or "period" or "point" => ".",
            "qmark" or "question" or "questionmark" or "qm" => "?",
            "underbar" or "ub" or "underscore" => "_",
            "minus" or "hyphen" or "dash" or "sub" or "subtract" => "-",
            "plus" or "add" => "+",
            _ => null
        };
    }

    public static string CharName(string s) {
        return s switch {
            "\\" => "backslash",
            "`" => "backtick",
            "\a" => "bell",
            "\b" => "backspace",
            "\n" => "lf",
            "\f" => "ff",
            "\0" => "null",
            "\"" => "quote",
            "\r" => "cr",
            ")" => "rparen",
            "(" => "lparen",
            "}" => "rcurly",
            "{" => "lcurly",
            "]" => "rbracket",
            "[" => "lbracket",
            "/" => "slash",
            " " => "space",
            "\t" => "tab",
            "'" => "tick",
            "~" => "tilde",
            "\v" => "vtab",
            "!" => "bang",
            "@" => "at",
            "#" => "hash",
            "$" => "dollar",
            "%" => "percent",
            "^" => "caret",
            "&" => "amp",
            "|" => "pipe",
            "*" => "star",
            ">" => "gt",
            "<" => "lt",
            "," => "comma",
            ":" => "colon",
            ";" => "semicolon",
            "." => "dot",
            "?" => "qmark",
            "_" => "underbar",
            "-" => "minus",
            "+" => "plus",
            _ => s
        };
    }

    public static LVal Sym(string s) {
        // special cases
        if (s.StartsWith(':')) return Atom(s.Substring(1));
        if (s.StartsWith('\\')) return Character(s.Substring(1));

        s = s.ToLower();
        switch (s) {
            case "t": return T();
            case "nil": return NIL();
            case "exit": return Exit();
        }

        LVal v = new LVal();
        v.ValType = LE.SYM;
        v.SymVal = s;

        return v;
    }

    public static LVal Str(string s) {
        LVal v = new LVal();
        v.ValType = LE.STR;
        v.StrVal = s;
        return v;
    }

    public static LVal Builtin(Func<LEnv, LVal, LVal> func) {
        LVal v = new LVal();
        v.ValType = LE.FUN;
        v.BuiltinVal = func;
        return v;
    }

    public static LVal Lambda(LVal formals, LVal body, LEnv? closure = null) {
        LVal v = new LVal();
        v.ValType = LE.FUN;
        v.BuiltinVal = null;
        v.Env = new LEnv();
        v.Closure = closure;
        v.Formals = formals;
        v.Body = body;
        return v;
    }

    public static LVal Sexpr() {
        LVal v = new LVal();
        v.ValType = LE.SEXPR;
        v.Cells = new List<LVal>();
        return v;
    }

    public static LVal Qexpr() {
        LVal v = new LVal();
        v.ValType = LE.QEXPR;
        v.Cells = new List<LVal>();
        return v;
    }

    public static LVal Comment(string s) {
        LVal v = new LVal();
        v.ValType = LE.COMMENT;
        v.StrVal = s;
        return v;
    }

    public static LVal Exit(int code = 0) {
        LVal v = new LVal();
        v.ValType = LE.EXIT;
        v.ExitCode = code;
        return v;
    }

    public static LVal Hash(LHash hash) {
        LVal v = new LVal();
        v.ValType = LE.HASH;
        v.HashValue = hash;
        return v;
    }
    public static LVal Hash(LVal? initialValues = null, LEnv? e = null) {
        LVal v = new LVal();
        v.ValType = LE.HASH;
        var hash = new LHash();
        v.HashValue = hash;

        // the entries, {{key value tag...} ... tag...}: each key as it's written, each value evaluated (in e), each tag
        // an atom; an entry that's an S-expression is evaluated first (to such a list); anything else is an error
        if (initialValues == null) return v;
        if (!initialValues.IsQExpr) return Err("A hash's entries are a list");
        foreach (var item in initialValues.Cells!) {
            if (item.IsAtom) {
                var t = hash.AddTag(item);
                if (t.IsErr) return t;
                continue;
            }
            var entry = item.IsSExpr && e != null ? item.Eval(e) : item;
            if (entry.IsErr) return entry;
            if (!entry.IsQExpr || entry.Count < 2 || !LHash.IsKey(entry[0]))
                return Err($"A hash's entry is {{key value tag...}}, not {entry.ToStr()}");
            var value = e != null ? entry[1].Copy().Eval(e) : entry[1];
            if (value.IsErr) return value;
            var put = hash.Put(entry[0], value, true);
            if (put.IsErr) return put;
            for (int i = 2; i < entry.Count; i++) {
                var t = hash.AddTag(entry[0], entry[i]);
                if (t.IsErr) return t;
            }
        }
        return v;
    }

    public static LVal Stream(LStream stream) {
        LVal v = new LVal();
        v.ValType = LE.STREAM;
        v.StreamValue = stream;
        return v;
    }
    public static LVal Stream(Stream stream) => Stream(new LStream(stream));

    // Helper methods
    public LVal Add(LVal x) {
        Cells = Cells ?? new List<LVal>();
        Cells.Add(x);
        return this;
    }

    public LVal Join(LVal y) {
        Cells = Cells ?? new List<LVal>();
        if (y.Cells != null) Cells?.AddRange(y.Cells); 
        return this;
    }

    public LVal Pop(int i, LEnv? e = null) {
        if (i >= Count) return Err($"Popping nonexistent item {i} from Expr");
        LVal x = this[i];
        if (e != null) x = x.Eval(e);
        Cells!.RemoveAt(i);
        return x;
    }

    private string ExprAsString(char open, char close) {
        // Special case for nil
        if (Count == 0) {
            return "NIL";
        }

        var s = new StringBuilder();
        s.Append(open);
        var con = "";
        if (Count > 0) {
            foreach (var c in Cells!) {
                s.Append(con);
                con = " ";
                s.Append(c.ToStr());
            }
        }

        s.Append(close);
        return s.ToString();
    }

    public LVal this[int i] {
        get => (i >= 0 && Count > i) ? Cells![i] : Err($"Invalid item number {i}"); 
    }

    /* List of possible escapable characters */
    const string StrEscapable = "\0\a\b\f\n\r\t\v\\\"";

    /* Function to escape characters */
    private static string StrEscape(char x) {
        switch (x) {
            case '\0': return "\\0";
            case '\a': return "\\a";
            case '\b': return "\\b";
            case '\f': return "\\f";
            case '\n': return "\\n";
            case '\r': return "\\r";
            case '\t': return "\\t";
            case '\v': return "\\v";
            case '\\': return "\\\\";
            case '\'': return "\\\'";
            case '\"': return "\\\"";
        }
        return "";
    }

    private string StrAsString() {
        var s = new StringBuilder();
        s.Append('"');
        /* Loop over the characters in the string */
        foreach (char c in StrVal) {
            if (StrEscapable.Contains(c)) {
                /* If the character is escapable then escape it */
                s.Append(StrEscape(c));
            } else {
                /* Otherwise print character as it is */
                s.Append(c);
            }
        }
        s.Append('"');
        return s.ToString();
    }

    public string ToStr() {
        var s = new StringBuilder();
        switch (ValType) {
            case LE.FUN:
                if (BuiltinVal != null) {
                    // (its name, and, partially applied, the values given it so far: the call so far)
                    s.Append("<function>(").Append(BuiltinName);
                    foreach (var b in Bound ?? new List<LVal>()) s.Append(' ').Append(b.ToStr());
                    s.Append(')');
                } else {
                    s.Append("<function>(fn ")
                        .Append(Formals!.ToStr())
                        .Append(' ')
                        .Append(Body!.ToStr())
                        .Append(')');
                }
                break;

            case LE.NUM:   return NumVal?.ToString() ?? "NIL";
            case LE.T:     return "T";
            case LE.ERR:   s.Append("Error: ").Append(ErrVal); break;
            case LE.ATOM:  s.Append(':').Append(SymVal); break;
            case LE.SYM:   s.Append(SymVal); break;
            case LE.CHAR:   s.Append('\\').Append(CharName(StrVal)); break;
            case LE.STR:   return StrAsString();
            case LE.HASH:  s.Append("<hash>").Append(HashValue!.ToQexpr().ToStr()); break;
            case LE.STREAM: s.Append("<stream>"); break;
            case LE.EXIT:  s.Append("exit"); break;
            case LE.SEXPR: return ExprAsString('(', ')');
            case LE.QEXPR: return ExprAsString('{', '}');
        }
        return s.ToString();
    }

    // The value as print shows it: a string's or a character's text as it is, anything else as ToStr has it
    public string ToDisplay() => ValType switch {
        LE.STR or LE.CHAR => StrVal,
        _ => ToStr()
    };

    public string Serialize() {
        if (IsHash) return HashValue!.Serialize();

        var sb = new StringBuilder();
        if (IsQExpr) sb.Append('{');
        else if (IsSExpr) sb.Append('(');

        var pre = "";
        if (Count > 0) {
            foreach (var c in Cells!) {
                sb.Append(pre).Append(c.Serialize());
                pre = " ";
            }
        } else {
            if (IsErr) sb.Append("error ").Append(ErrVal);
            else sb.Append(ToStr());
        }

        if (IsQExpr) sb.Append('}');
        else if (IsSExpr) sb.Append(')');
        return sb.ToString();
    }

    public void Print() {
        Console.Write(ToStr());
    }

    public void Println() { Print(); Console.WriteLine(); }

    public override bool Equals(object? o) {
        var y = o as LVal;
        if (y is null) return false;

        if (ValType != y.ValType) return false;
    
        switch (ValType) {
            case LE.T:   return true;   // we already checked that the Types are the same for these, so we are good.
            case LE.NUM: return (NumVal!.CompareTo(y.NumVal) == 0);
            case LE.ERR: return (ErrVal == y.ErrVal);
            case LE.ATOM:
            case LE.SYM: return (SymVal == y.SymVal);
            case LE.CHAR:
            case LE.STR: return (StrVal == y.StrVal);
            case LE.FUN: 
                if (BuiltinVal != null || y.BuiltinVal != null) {
                    if (BuiltinVal != y.BuiltinVal) return false;
                    var bx = Bound ?? new List<LVal>();
                    var by = y.Bound ?? new List<LVal>();
                    return bx.Count == by.Count && bx.Zip(by).All(p => p.First.Equals(p.Second));
                }
                return (Formals!.Equals(y.Formals) && Body!.Equals(y.Body));

            case LE.HASH: return ReferenceEquals(HashValue, y.HashValue) || HashValue!.EqualTo(y.HashValue!);

            case LE.QEXPR:
            case LE.SEXPR:
                if (Count != y.Count) return false;
                for (int i = 0; i < Count; i++) {
                    if (!this[i].Equals(y[i])) return false;
                }

                return true;
        }

        return false;
    }

    // (Equal values have equal hashes: a number's is its value's, whatever its kind, as Equals has it)
    public override int GetHashCode() {
        return ValType.GetHashCode()
            ^ StrVal.GetHashCode()
            ^ ErrVal.GetHashCode()
            ^ (SymVal?.GetHashCode() ?? 0)
            ^ (NumVal != null ? NumHash(NumVal) : 0);
        // TODO: include Cells
    }

    private static int NumHash(Num n) {
        if (n is Comp c) return c.im.IsZero ? NumHash(c.r) : HashCode.Combine(NumHash(c.r), NumHash(c.im));
        var r = Rat.ToRat(n);
        return HashCode.Combine(r.num, r.den);
    }

    public static string LEName(LE t) =>
        t switch {
            LE.FUN => "Function",
            LE.NUM => "Number",
            LE.ERR => "Error",
            LE.ATOM => "Atom",
            LE.SYM => "Symbol",
            LE.CHAR => "Character",
            LE.STR => "String",
            LE.HASH => "Hash",
            LE.STREAM => "Stream",
            LE.SEXPR => "S-Expression",
            LE.QEXPR => "Q-Expression",
            LE.T => "T",
            LE.EXIT => "Exit",
            LE.TAIL => "Tail call",
            LE.COMMENT => "Comment",
            _ => "Unknown"
        };

    // How deep calls may nest (a call in tail position doesn't count: it takes its caller's place); deeper is an
    // error, not the host's stack overflowing (Program runs the interpreter on a thread with a stack big enough)
    public const int MaxDepth = 10000;
    [ThreadStatic] private static int _depth;

    // Ctrl-C was pressed (Program's handler sets it, the REPL clears it): each call, and each loop's step, stops with
    // the error "interrupted" (:intr) till then
    public static volatile bool Interrupted;
    public static LVal? CheckInterrupt() => Interrupted ? Builtins.SysErr("intr") : null;

    // A built-in called: an exception it throws comes back as an error value (but exit's)
    private static LVal CallBuiltin(LEnv e, LVal f, LVal a) {
        var intr = CheckInterrupt();
        if (intr != null) return intr;
        try {
            return f.BuiltinVal!(e, a);
        }
        catch (ExitException) { throw; }
        catch (Exception ex) {
            return LVal.Err(Builtins.FromHost(ex.Message));
        }
    }

    // A function's arguments: evaluated in e, left to right (the first error stops them: err); or, for an fexpr,
    // each as it is, the expression the caller wrote, remembering e (its Scope), so (eval x) evaluates it there
    private static List<LVal> Args(LEnv e, LVal f, LVal a, out LVal? err) {
        err = null;
        var vals = new List<LVal>();
        while (a.Count > 0) {
            if (f.IsFexpr) {
                var x = a.Pop(0);
                x.Scope = e;
                vals.Add(x);
            }
            else {
                var v = a.Pop(0, e);
                if (v.IsErr) {
                    err = v;
                    break;
                }
                vals.Add(v);
            }
        }
        return vals;
    }

    // A built-in's arguments.  A special form's are as they're written (more than it takes is an error).  An ordinary
    // built-in's are evaluated, left to right, the first error the call's value (but for one that takes errors as
    // values: the type tests, type-of, error-message ...), then checked as values (ValueArgs).  OUT: the call's value,
    // if that's it (null: the call goes on, with a its values)
    private static LVal? BuiltinArgs(ref LEnv e, LVal f, ref LVal a) {
        int bound = f.Bound?.Count ?? 0, n = bound + a.Count;
        if (n > f.MaxArgs) return TooMany(f, n);
        if (f.IsSpecial) return null;
        var vals = new List<LVal>();
        while (a.Count > 0) {
            var v = a.Pop(0, e);
            if (v.IsErr && !f.TakesErrors) return v;
            vals.Add(v);
        }
        return ValueArgs(f, vals, out a, false);
    }

    private static LVal TooMany(LVal f, int n) =>
        Err($"'{f.BuiltinName}' takes {(f.MaxArgs == f.MinArgs ? "" : "at most ")}{f.MaxArgs} argument{(f.MaxArgs == 1 ? "" : "s")}, not {n}");

    // An ordinary built-in's values (a partially applied one's first): more than its most is an error, an error is the
    // call's value (as above), fewer than it needs is the built-in with those given, waiting for the rest (partial
    // application).  A built-in may change the values it's given, so those kept or given from outside (a partial
    // one's, vals when copy) are copies.  OUT: the call's value, if that's it (null: the call goes on, with a the values)
    private static LVal? ValueArgs(LVal f, List<LVal> vals, out LVal a, bool copy) {
        a = Sexpr();
        if (f.Bound != null) foreach (var b in f.Bound) a.Add(b.Copy());
        foreach (var v in vals) {
            if (v.IsErr && !f.TakesErrors) return v;
            a.Add(copy ? v.Copy() : v);
        }
        if (a.Count > f.MaxArgs) return TooMany(f, a.Count);
        if (a.Count < f.MinArgs) {
            var p = f.Copy();
            p.Bound = new List<LVal>(a.Cells!);
            return p;
        }
        return null;
    }

    // A built-in applied to values (map's f, sort's less ...), as a call of it with them would be, not evaluated again
    public static LVal ApplyBuiltin(LEnv e, LVal f, List<LVal> vals) {
        if (f.IsSpecial) return Err($"'{f.BuiltinName}' can't be applied to values: it's a special form");
        return ValueArgs(f, vals, out var a, true) ?? CallBuiltin(e, f, a);
    }

    // Whether a function's body takes extra arguments: it names &_ or &1, &2 ...
    private static bool TakesExtras(LVal v) =>
        (v.IsSym && (v.SymVal == "&_" || (v.SymVal.Length > 1 && v.SymVal[0] == '&' && v.SymVal.Skip(1).All(char.IsDigit))))
        || (v.Cells != null && v.Cells.Any(TakesExtras));

    public static LVal Call(LEnv e, LVal f, LVal a) {
        if (f.BuiltinVal != null) return BuiltinArgs(ref e, f, ref a) ?? CallBuiltin(e, f, a);
        var vals = Args(e, f, a, out var err);
        if (err != null) return err;
        return Apply(f, vals);
    }

    // A function (a copy of its own, which this changes) applied to values: they're bound in its own scope, the
    // formals first, then &1, &2 ... and &_ for the rest; if it has all it needs, its body runs there, with the scope
    // the function was made in as its parent (lexical scope); if not, it's the function with those bound (partial
    // application).  A call in the body's tail position comes back as a TAIL value and runs here, in its place
    public static LVal Apply(LVal f, List<LVal> vals) {
        if (_depth >= MaxDepth) return LVal.Err($"Too deep: more than {MaxDepth} calls nested");
        ++_depth;
        try {
            while (true) {
                var intr = CheckInterrupt();
                if (intr != null) return intr;
                if (!f.IsFexpr) {
                    var err = vals.FirstOrDefault(v => v.IsErr);
                    if (err != null) return err;
                }

                // (the arguments past the formals: &1, &2 ..., and &_ the list of them)
                var extras = Qexpr();
                foreach (var val in vals) {
                    if (f.Formals!.Count == 0) {
                        extras.Add(val);
                        f.Env!.Put($"&{extras.Count}", val);
                    } else {
                        LVal sym = f.Formals.Pop(0);
                        f.Env!.Put(sym.SymVal!, val);
                    }
                }

                f.Env!.Put("&_", extras);
                if (f.Formals!.Count > 0) return f.Copy();
                if (extras.Count > 0 && !TakesExtras(f.Body!)) {
                    var takes = vals.Count - extras.Count;
                    return LVal.Err($"The function takes {takes} argument{(takes == 1 ? "" : "s")}, not {vals.Count}");
                }

                if (!f.Body!.IsQExpr) return LVal.Err("A function's body must be a QExpr");
                f.Env.Parent = f.Closure;
                var body = f.Body.Copy();
                body.ValType = LE.SEXPR;
                var r = EvalTail(f.Env, body);
                if (r.ValType != LE.TAIL) return r;
                f = r.TailFn!;
                vals = r.TailArgs!;
            }
        }
        finally {
            --_depth;
        }
    }

    public LVal? TailFn = null;         // a TAIL value's function ...
    public List<LVal>? TailArgs = null; //   and its arguments

    // (exit code): the program ends with that status
    private static LVal ExitWith(LEnv e, LVal v) {
        var code = v.Pop(0, e);
        if (code.IsErr) return code;
        if (!code.IsNum) return LVal.Err("'exit' expects a Number");
        throw new ExitException((int)code.NumVal!.ToInt().num);
    }

    // An expression evaluated in a function body's tail position: a call to a function there comes back as a TAIL
    // value, for Apply to run in the caller's place; a built-in with a tail position (if, do, let, eval) goes on with
    // the expression there
    private static LVal EvalTail(LEnv e, LVal v) {
        while (true) {
            var intr = CheckInterrupt();
            if (intr != null) return intr;
            if (!v.IsSExpr) return v.Eval(e);
            if (v.Count == 0) return NIL();

            LVal f = v.Pop(0, e);
            if (f.IsErr) return f;
            if (f.IsExit && v.Count > 0) return ExitWith(e, v);
            if (v.Count == 0 && !f.IsFun) return f;
            if (f.IsHash) {
                f = HashAt(e, f, v);
                if (!f.IsFun) return f;
            }
            if (!f.IsFun) return LVal.Err($"S-Expression starts with incorrect type. Got {LEName(f.ValType)}, Expected {LEName(LE.FUN)}.");

            if (f.BuiltinVal != null) {
                var done = BuiltinArgs(ref e, f, ref v);
                if (done != null) return done;
                if (f.TailForm == null) return CallBuiltin(e, f, v);
                TailStep step;
                try {
                    step = f.TailForm(e, v);
                }
                catch (ExitException) { throw; }
                catch (Exception ex) {
                    return LVal.Err(Builtins.FromHost(ex.Message));
                }
                if (step.Expr == null) return step.Value!;
                e = step.Env!;
                v = step.Expr;
                continue;
            }

            var vals = Args(e, f, v, out var err);
            if (err != null) return err;
            return new LVal { ValType = LE.TAIL, TailFn = f, TailArgs = vals };
        }
    }

    public static LVal? EvalSExpr(LEnv e, LVal? v) {
        if (v == null) return null;
        if (v.Count == 0) return NIL();

        LVal f = v.Pop(0, e);
        if (f.IsErr) return f;
        if (f.IsExit && v.Count > 0) return ExitWith(e, v);
        if (v.Count == 0 && !f.IsFun) { return f; }
        if (f.IsHash) {
            f = HashAt(e, f, v);
            if (!f.IsFun) return f;
        }

        if (!f.IsFun) return LVal.Err($"S-Expression starts with incorrect type. Got {LEName(f.ValType)}, Expected {LEName(LE.FUN)}.");

        return LVal.Call(e, f, v);
    }

    // A hash called, (h key arg...): the value at key (its first item, v's, evaluated and taken off); a function there
    // is a method, given back with &0 the hash (through which its private entries are had), to be applied to the
    // arguments that follow as any function is (fewer than its formals: partially applied); anything else is the value,
    // and arguments after it are an error
    private static LVal HashAt(LEnv e, LVal h, LVal v) {
        var key = v.Pop(0, e);
        if (key.IsErr) return key;
        var value = h.HashValue!.Get(key);
        if (value.IsErr) return value;
        if (value.IsFun) {
            value.Env?.Put("&0", Hash(h.HashValue.PrivateCallProxy));
            return value;
        }
        if (v.Count > 0) return Err($"{key.ToStr()} isn't a method: its value isn't a function");
        return value;
    }

    public static LVal? ReadExprFromTokens(List<Parser.Token> tokens, char? end = null) {
        LVal exp = end == '}' ? Qexpr() : Sexpr();
        var t = tokens.FirstOrDefault();
        while (t != null) {
            tokens.RemoveAt(0);
            LVal? val = t.type switch {
                Parser.Token.Type.EOF => end == null ? null : Err($"Missing {end} for {(end == '}' ? 'Q' : 'S')}Expr"),
                Parser.Token.Type.Comment => Comment(t.str!),
                Parser.Token.Type.Error => Err(t.str ?? "Unknown error"),
                Parser.Token.Type.SExOpen => ReadExprFromTokens(tokens, ')'),
                Parser.Token.Type.QExOpen => ReadExprFromTokens(tokens, '}'),
                Parser.Token.Type.SExClose => end != ')' ? Err("SExClose without SExOpen") : null,
                Parser.Token.Type.QExClose => end != '}' ? Err("QExClose without QExOpen") : null,
                Parser.Token.Type.Number => Number(t.num!),
                Parser.Token.Type.String => Str(t.str!),
                Parser.Token.Type.Symbol => Sym(t.str!), // inludes ATOMS and special-case symbols, like T, NIL, and EXIT
                _ => Err($"Unknown token type {t.type}")
            };

            if (val == null) break;
            
            switch (val.ValType) {
                case LE.ERR: throw new Exception(val.ErrVal);
                case LE.COMMENT: break;
                default:
                    exp.Add(val);
                    break;
            }
            t = tokens.FirstOrDefault();
        }

        return exp;
    }

    public LVal Eval(LEnv e) {
        if (IsSym) return e.Get(SymVal!);
        if (IsSExpr) return EvalSExpr(e, this)!;

        // A Q-expression remembers the scope it's written in: eval runs it there (so code passed to a function runs
        // where it was written, and a function's own variables are its alone)
        if (IsQExpr && Scope == null) Scope = e;
        return this;
    }

    // The order of values (cmp, <, >, sort): one for all of them, by kind first (numbers, characters, strings, atoms,
    // symbols, lists, T, functions, hashes, streams, errors), then by value: numbers by value (whatever their kinds),
    // characters and strings by their codes, atoms and symbols by name, lists item by item (a shorter one first, when
    // it's the start of the other).  Equal values (Equals) are in the same place
    public int CompareTo(LVal v) {
        int Rank(LVal x) => x.ValType switch {
            LE.NUM => 0, LE.CHAR => 1, LE.STR => 2, LE.ATOM => 3, LE.SYM => 4, LE.QEXPR => 5, LE.SEXPR => 6,
            LE.T => 7, LE.FUN => 8, LE.HASH => 9, LE.STREAM => 10, LE.ERR => 11, _ => 12
        };
        var kind = Rank(this).CompareTo(Rank(v));
        if (kind != 0) return Math.Sign(kind);
        switch (ValType) {
            case LE.NUM:   return Math.Sign(NumVal!.CompareTo(v.NumVal));
            case LE.CHAR:
            case LE.STR:   return Math.Sign(string.CompareOrdinal(StrVal, v.StrVal));
            case LE.ATOM:
            case LE.SYM:   return Math.Sign(string.CompareOrdinal(SymVal, v.SymVal));
            case LE.ERR:   return Math.Sign(string.CompareOrdinal(ErrVal, v.ErrVal));
            case LE.T:     return 0;
            case LE.QEXPR:
            case LE.SEXPR:
                for (int i = 0; i < Count && i < v.Count; ++i) {
                    var cmp = this[i].CompareTo(v[i]);
                    if (cmp != 0) return cmp;
                }
                return Math.Sign(Count.CompareTo(v.Count));
            case LE.HASH:
                if (Equals(v)) return 0;
                break;
            case LE.STREAM:
                if (ReferenceEquals(StreamValue, v.StreamValue)) return 0;
                return Math.Sign(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(StreamValue)
                    .CompareTo(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(v.StreamValue)));
            case LE.FUN:
                if (Equals(v)) return 0;
                if (BuiltinVal != null && v.BuiltinVal != null && BuiltinName != v.BuiltinName)
                    return Math.Sign(string.CompareOrdinal(BuiltinName, v.BuiltinName));
                break;
        }
        // (two different functions or hashes: as they print)
        return Math.Sign(string.CompareOrdinal(ToStr(), v.ToStr()));
    }
}