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
    public string ErrVal = string.Empty;
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
            case LE.ERR: x.ErrVal = ErrVal; break;
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
        v.NumVal = new Int(BigInteger.One);
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

    public static LVal Err(string errStr) {
        LVal v = new LVal();
        v.ValType = LE.ERR;  
        v.ErrVal = errStr;
        return v;
    }

    public static LVal Atom(string s) {
        LVal v = new LVal();
        v.ValType = LE.ATOM;
        v.SymVal = s.ToLower(); // TODO: assess if case-sensitive atoms would be useful
        return v;
    }

    public static LVal Character(string s) {
        LVal v = new LVal();
        v.ValType = LE.CHAR;
        v.StrVal = s.ToLower().Replace("-", "") switch {
            "backslash" or "bslash" or "bs" => "\\",
            "backtick" or "btick" or "bt" => "`",
            "bell" => "\b",
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
            "verticalspace" or "vspace" or "vs" => "\a",
            "verticaltab" or "vtab" or "vt" => "\v",
            "bang" or "warning" or "exclaim" or "exclamation" or "exclamationpoint" => "!",
            "at" => "@",
            "poundsign" or "pound" or "hash" or "lb" => "#",
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
            "dot" or "peroid" or "point" => ".",
            "qmark" or "question" or "questionmark" or "qm" => "?",
            "underbar" or "ub" or "underscore" => "_",
            "minus" or "hyphen" or "dash" or "sub" or "subtract" => "-",
            "plus" or "add" => "+",
            _ => s.Substring(0, 1)
        };

        return v;
    }

    public static string CharName(string s) {
        return s switch {
            "\\" => "backslash",
            "`" => "backtick",
            "\b" => "bell",
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
            "\a" => "vspace",
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

        // TODO: ensure the shape of the intialValues is correct, i.e. {{:1 a} {:2 b} :tag1 :tag2}
        // if (initialValues != null) Console.WriteLine($"Creating hash: initialValues = {initialValues.ToStr()}, type: {LVal.LEName(initialValues.ValType)}");
        if (initialValues != null && initialValues.IsQExpr) {
            foreach (var entry in initialValues.Cells!) {
                if (entry.IsSExpr && e != null) {
                    //Console.WriteLine($"adding entry {entry.ToStr()}");
                    var en = entry.Eval(e);
                    //Console.WriteLine($"entry evaluated to {en.ToStr()}");
                    hash.Put(en[0], en[1].Eval(e), true);

                    // add any tags
                    while (en.Count > 2) hash.AddTag(en[0], en.Pop(2));
                } else if (entry.Count > 1 && LHash.IsKey(entry[0]) && e != null) {
                    //Console.WriteLine($"adding entry {entry.ToStr()}");
                    hash.Put(entry[0], entry[1].Eval(e), true);

                    // add any tags
                    while (entry.Count > 2) hash.AddTag(entry[0], entry.Pop(2));
                }
                else if (entry.IsAtom) hash.AddTag(entry);
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

    /* Possible unescapable characters */
    const string str_unescapable = "abfnrtv\\\'\"";

    /* Function to unescape characters */
    private static char StrUnescape(char x) {
        switch (x) {
            case 'a':  return '\a';
            case 'b':  return '\b';
            case 'f':  return '\f';
            case 'n':  return '\n';
            case 'r':  return '\r';
            case 't':  return '\t';
            case 'v':  return '\v';
            case '\\': return '\\';
            case '\'': return '\'';
            case '\"': return '\"';
        }
        return '\0';
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
                    return "<builtin>";
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
                    return BuiltinVal == y.BuiltinVal;
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

    public override int GetHashCode() {
        return ValType.GetHashCode()
            ^ StrVal.GetHashCode()
            ^ ErrVal.GetHashCode()
            ^ (SymVal?.GetHashCode() ?? 0)
            ^ (NumVal?.ToString()?.GetHashCode() ?? 0);
        // TODO: include Cells
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
            _ => "Unknown"
        };

    // How deep calls may nest (a call in tail position doesn't count: it takes its caller's place); deeper is an
    // error, not the host's stack overflowing (Program runs the interpreter on a thread with a stack big enough)
    public const int MaxDepth = 10000;
    [ThreadStatic] private static int _depth;

    // A built-in called: an exception it throws comes back as an error value (but exit's)
    private static LVal CallBuiltin(LEnv e, LVal f, LVal a) {
        try {
            return f.BuiltinVal!(e, a);
        }
        catch (ExitException) { throw; }
        catch (Exception ex) {
            return LVal.Err(ex.Message);
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

    public static LVal Call(LEnv e, LVal f, LVal a) {
        if (f.BuiltinVal != null) return CallBuiltin(e, f, a);
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
                if (!f.IsFexpr) {
                    var err = vals.FirstOrDefault(v => v.IsErr);
                    if (err != null) return err;
                }

                int i = 0;
                var extras = Qexpr();
                foreach (var val in vals) {
                    ++i;
                    if (f.Formals!.Count == 0) {
                        f.Env!.Put($"&{i}", val);
                        extras.Add(val);
                    } else {
                        LVal sym = f.Formals.Pop(0);
                        f.Env!.Put(sym.SymVal!, val);
                    }
                }

                f.Env!.Put("&_", extras);
                if (f.Formals!.Count > 0) return f.Copy();

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
            if (!v.IsSExpr) return v.Eval(e);
            if (v.Count == 0) return NIL();

            LVal f = v.Pop(0, e);
            if (f.IsErr) return f;
            if (f.IsExit && v.Count > 0) return ExitWith(e, v);
            if (v.Count == 0 && !f.IsFun) return f;
            if (!f.IsFun) return LVal.Err($"S-Expression starts with incorrect type. Got {LEName(f.ValType)}, Expected {LEName(LE.FUN)}.");

            if (f.BuiltinVal != null) {
                if (f.TailForm == null) return CallBuiltin(e, f, v);
                TailStep step;
                try {
                    step = f.TailForm(e, v);
                }
                catch (ExitException) { throw; }
                catch (Exception ex) {
                    return LVal.Err(ex.Message);
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

        if (!f.IsFun) return LVal.Err($"S-Expression starts with incorrect type. Got {LEName(f.ValType)}, Expected {LEName(LE.FUN)}.");

        return LVal.Call(e, f, v);
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

    public int CompareTo(LVal v) {
        if (NumVal != null && v.NumVal != null) return NumVal.CompareTo(v.NumVal);
        if ((IsStr || IsChar) && (v.IsStr || v.IsChar)) return Math.Sign(string.CompareOrdinal(StrVal, v.StrVal));
        if (!string.IsNullOrEmpty(SymVal) && !string.IsNullOrEmpty(v.SymVal)) return string.Compare(SymVal, v.SymVal, StringComparison.OrdinalIgnoreCase);
        if (!string.IsNullOrEmpty(ErrVal) && !string.IsNullOrEmpty(v.ErrVal)) return ErrVal.CompareTo(v.ErrVal);
        if (Count > 0 && v.Count > 0 && Count == v.Count && ValType == v.ValType) {
            var cmp = 0;
            for (int i = 0; cmp == 0 && i < Count; ++i) {
                cmp = this[i].CompareTo(v[i]);
                if (cmp != 0) break;
            }
            return cmp;
        }
        return Math.Sign(string.CompareOrdinal(ToStr(), v.ToStr()));
    }
}