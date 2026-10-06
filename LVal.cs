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
    public bool Run;        // Expr a list run as code: its items as an S-expression
    public static TailStep Done(LVal v) => new TailStep { Value = v };
    public static TailStep Next(LEnv e, LVal x) => new TailStep { Env = e, Expr = x };
    public static TailStep RunCode(LEnv e, LVal x) => new TailStep { Env = e, Expr = x, Run = true };

    // The step finished, outside tail position: the expression evaluated (or run), if there is one
    public LVal Finish() => Expr == null ? Value! : Run ? LVal.EvalCode(Env!, Expr) : Expr.Eval(Env!);
}

public partial class LVal {
    public enum LE { ERR, T, NUM, ATOM, SYM, CHAR, STR, FUN, SEXPR, QEXPR, HASH, STREAM, COMMENT, EXIT, TAIL, BUFFER };

    // (A value is small: what every kind has, and one reference to what the rarer kinds have, _x)
    public LE ValType;
    public LEnv? Scope = null;      // a Q-expression's: the scope it was written in, where eval runs it
    private Num? _num;
    private long _long;             // a number's value, when it's an integer that fits (its Num made only when it's wanted)
    public Num? NumVal {
        get => _num ?? (ValType == LE.NUM ? _num = new Int(_long) : null);
        set => _num = value;
    }
    public string Text = string.Empty;  // a string's or a character's text, a symbol's or an atom's name, an error's message
    public List<LVal>? Cells = null;
    private object? _x = null;      // a function's FunInfo, a hash's LHash, a stream's LStream, an error's code, exit's code, a tail call's TailInfo

    public string StrVal { get => Text; set => Text = value; }
    public string SymVal { get => Text; set => Text = value; }
    public string ErrVal { get => Text; set => Text = value; }
    public string? ErrCode { get => (_x as ErrInfo)?.Code; set => ((ErrInfo)(_x ??= new ErrInfo())).Code = value; }
    public ErrInfo? Where => _x as ErrInfo;

    // An error's code, and where it was made: the call it was made in (its code's place) and the calls of functions
    // it was in, the innermost first (for the trace shown when it ends a program: Program)
    public sealed class ErrInfo {
        public string? Code;
        public CodeInfo? Site;
        public CodeInfo[]? Calls;
    }
    public int ExitCode { get => _x is int i ? i : 0; set => _x = value; }
    public LHash? HashValue { get => _x as LHash; set => _x = value; }
    public LStream? StreamValue { get => _x as LStream; set => _x = value; }
    public byte[]? BufferValue { get => _x as byte[]; set => _x = value; }    // a buffer's bytes (changed in place: shared)
    public Name SymName => _x as Name ?? (Name)(_x = Name.Of(Text));     // a symbol's name, interned

    // A function's own: a lambda's scope (the arguments given it so far), the scope it was made in (its calls' parent:
    // lexical scope), whether it's an fexpr (its arguments come unevaluated), its formals and body; a built-in's code,
    // its tail form (if, do ...), its name, the arguments it needs (fewer: partial application) and the most it takes
    // (more: an error), whether it's a special form (its arguments as they're written) or takes errors as values, and,
    // partially applied, the values given it so far
    public sealed class FunInfo {
        public LEnv? Env, Closure;
        public bool IsFexpr, IsSpecial, TakesErrors;
        public Func<LEnv, LVal, TailStep>? TailForm;
        public LVal? Formals, Body;
        public Func<LEnv, LVal, LVal>? BuiltinVal;
        public string? BuiltinName;
        public int MinArgs, MaxArgs = int.MaxValue;
        public List<LVal>? Bound;
        public Func<LVal, LVal?>? Fast1;            // a built-in's fast way with one argument, or two (NIL: the
        public Func<LVal, LVal, LVal?>? Fast2;      //   built-in itself does it): Compiler.cs
        public Func<LVal, LVal, LVal, LVal?>? Fast3;
        public Func<LVal[], LVal?>? FastN;          // (any number of them)
        public FunInfo Clone() => (FunInfo)MemberwiseClone();
    }
    private FunInfo? Fn => _x as FunInfo;
    private FunInfo F => _x as FunInfo ?? (FunInfo)(_x = new FunInfo());
    public LEnv? Env { get => Fn?.Env; set => F.Env = value; }
    public LEnv? Closure { get => Fn?.Closure; set => F.Closure = value; }
    public bool IsFexpr { get => Fn?.IsFexpr ?? false; set => F.IsFexpr = value; }
    public Func<LEnv, LVal, TailStep>? TailForm { get => Fn?.TailForm; set => F.TailForm = value; }
    public LVal? Formals { get => Fn?.Formals; set => F.Formals = value; }
    public LVal? Body { get => Fn?.Body; set => F.Body = value; }
    public Func<LEnv, LVal, LVal>? BuiltinVal { get => Fn?.BuiltinVal; set => F.BuiltinVal = value; }
    public string? BuiltinName { get => Fn?.BuiltinName; set => F.BuiltinName = value; }
    public int MinArgs { get => Fn?.MinArgs ?? 0; set => F.MinArgs = value; }
    public int MaxArgs { get => Fn?.MaxArgs ?? int.MaxValue; set => F.MaxArgs = value; }
    public bool IsSpecial { get => Fn?.IsSpecial ?? false; set => F.IsSpecial = value; }
    public bool TakesErrors { get => Fn?.TakesErrors ?? false; set => F.TakesErrors = value; }
    public List<LVal>? Bound { get => Fn?.Bound; set => F.Bound = value; }

    // A function value of its own to change (a method's, a partial application's): this one's copy, its FunInfo too
    public LVal CloneFun() {
        var x = (LVal)MemberwiseClone();
        x.Frozen = false;
        x._x = Fn?.Clone();
        return x;
    }

    // A tail call's: the function and its arguments
    private sealed class TailInfo {
        public LVal? Fn;
        public List<LVal>? Args;
    }
    public LVal? TailFn { get => (_x as TailInfo)?.Fn; set => ((TailInfo)(_x ??= new TailInfo())).Fn = value; }
    public List<LVal>? TailArgs { get => (_x as TailInfo)?.Args; set => ((TailInfo)(_x ??= new TailInfo())).Args = value; }

    // A built-in's fast ways (Compiler.cs)
    public void SetFast(Func<LVal, LVal?>? one, Func<LVal, LVal, LVal?>? two) {
        F.Fast1 = one;
        F.Fast2 = two;
    }
    public void SetFast3(Func<LVal, LVal, LVal, LVal?> three) => F.Fast3 = three;
    public void SetFastN(Func<LVal[], LVal?> any) => F.FastN = any;

    // A value is shared once it's code (the reader's), a variable's, a hash's or a partial application's: then it's
    // frozen, and its list can't be changed in place (a built-in makes a new value, never changes one it's given)
    public bool Frozen = false;

    public LVal Freeze() {
        if (Frozen) return this;
        Frozen = true;
        if (_x == null && Cells == null) return this;
        if (Cells != null) foreach (var c in Cells) c.Freeze();
        if (Bound != null) foreach (var b in Bound) b.Freeze();
        Formals?.Freeze();
        Body?.Freeze();
        return this;
    }

    private void Mutable() {
        if (Frozen) throw new InvalidOperationException("internal error: a shared value was changed");
    }

    // This value as written here, remembering the scope e (a Q-expression's, which eval runs it in; an fexpr's
    // argument's): a copy sharing its list, so the code it's in isn't changed
    public LVal ScopedIn(LEnv e) {
        Freeze();
        if (Cells != null && _x == null) _x = new CodeInfo();    // (its compiled code shared with the copy)
        var x = (LVal)MemberwiseClone();
        x.Scope = e;
        return x;
    }

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
    public bool IsBuffer => ValType == LE.BUFFER;
    public bool IsComment => ValType == LE.COMMENT;

    //  Hashes
    public LVal Copy() {
        if (IsHash || IsStream || IsBuffer) return this;
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
                    x.Env = Env?.Copy();    // (its bindings shared: values are frozen)
                    x.Closure = Closure;
                    x.IsFexpr = IsFexpr;
                    x.Formals = Formals?.Copy();
                    x.Body = Body?.Copy();
                }
                break;

            case LE.NUM: x._num = _num; x._long = _long; break;
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

    // T and NIL: one each, shared (frozen)
    private static readonly LVal _t = new LVal { ValType = LE.T, Frozen = true };
    private static readonly LVal _nil = new LVal { ValType = LE.QEXPR, Cells = new List<LVal>(), Frozen = true };
    public static LVal T() => _t;
    public static LVal NIL() => _nil;
    public static LVal Bool(bool b) => b ? _t : _nil;

    // The small integers, made once each, shared (they're frozen: a value isn't changed): -1024 to 65535
    private const int SmallMin = -1024, SmallMax = 65535;
    private static readonly LVal?[] _small = new LVal?[SmallMax - SmallMin + 1];
    private static LVal Small(int n) => _small[n - SmallMin] ??= new LVal { ValType = LE.NUM, _long = n, Frozen = true };

    // An integer kept in the value itself (as Number(long) makes one): its value
    internal bool IsLong(out long n) {
        n = _long;
        return ValType == LE.NUM && _num == null;
    }

    public static LVal Number(int x) => Number((long)x);
    public static LVal Number(long x) => x >= SmallMin && x <= SmallMax ? Small((int)x) : new LVal { ValType = LE.NUM, _long = x };
    public static LVal Number(BigInteger x) => x >= long.MinValue && x <= long.MaxValue ? Number((long)x) : Number(new Int(x));
    public static LVal Number(Num x) {
        if (x.GetType() == typeof(Int) && ((Int)x).Fits) return Number(((Int)x).Small);    // (kept in the value)
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
        v._x = new ErrInfo { Code = code, Site = Site, Calls = CallsNow() };
        return v;
    }

    // An error made from another (its message changed): its code, and where the other was made
    public static LVal ErrFrom(LVal e, string errStr) {
        var v = Err(errStr);
        v._x = e._x is ErrInfo w ? new ErrInfo { Code = w.Code, Site = w.Site, Calls = w.Calls } : v._x;
        return v;
    }

    // The trace of an error: where it was made, and the calls it was in (lines, for stderr: empty when none's known)
    public string Trace() {
        if (_x is not ErrInfo w || w.Site == null) return "";
        var sb = new StringBuilder();
        sb.Append("  at ").Append(w.Site.Place());
        foreach (var c in w.Calls ?? Array.Empty<CodeInfo>()) sb.Append('\n').Append("  called at ").Append(c.Place());
        return sb.ToString();
    }

    public static LVal Atom(string s) {
        LVal v = new LVal();
        v.ValType = LE.ATOM;
        v.SymVal = s.ToLower(); // TODO: assess if case-sensitive atoms would be useful
        return v;
    }

    // A character, \x or \name (a name CharOf knows)
    public static LVal Character(string s) {
        var c = CharOf(s) ?? throw new ArgumentException($"Unknown character name \\{s}");
        return c.Length == 1 && c[0] < 256 ? Char(c[0]) : new LVal { ValType = LE.CHAR, StrVal = c };
    }

    // The characters (bytes), one each, shared (frozen)
    private static readonly LVal?[] _chars = new LVal?[256];
    public static LVal Char(char c) => c < 256 ? _chars[c] ??= new LVal { ValType = LE.CHAR, StrVal = c.ToString(), Frozen = true }
        : new LVal { ValType = LE.CHAR, StrVal = c.ToString() };

    // A number that's a plain integer (not fixed, rational or complex) that fits in a long: its value
    public bool IsSmallInt(out long n) {
        if (ValType == LE.NUM) {
            if (_num == null) {
                n = _long;
                return true;
            }
            if (_num.GetType() == typeof(Int)) {
                var i = (Int)_num;
                if (i.Fits) {
                    n = i.Small;
                    return true;
                }
            }
        }
        n = 0;
        return false;
    }

    // A number that's a plain integer (not fixed, rational or complex): its value
    public bool IsPlainInt(out BigInteger n) {
        if (ValType == LE.NUM && _num == null) {
            n = _long;
            return true;
        }
        if (ValType == LE.NUM && NumVal!.GetType() == typeof(Int)) {
            n = ((Int)NumVal).num;
            return true;
        }
        n = default;
        return false;
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
            "escape" or "esc" => "\x1b",
            "delete" or "del" => "\x7f",
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
            "\x1b" => "escape",
            "\x7f" => "delete",
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
            var value = e != null ? entry[1].Eval(e) : entry[1];
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

    // A buffer: bytes, changed in place (so, as a hash is, it's shared, not copied)
    public static LVal Buffer(byte[] bytes) => new LVal { ValType = LE.BUFFER, BufferValue = bytes };

    // Helper methods
    public LVal Add(LVal x) {
        Mutable();
        Cells = Cells ?? new List<LVal>();
        Cells.Add(x);
        return this;
    }

    public LVal Join(LVal y) {
        Mutable();
        Cells = Cells ?? new List<LVal>();
        if (y.Cells != null) Cells?.AddRange(y.Cells);
        return this;
    }

    // Item i taken out of a list of one's own (a built-in's arguments), evaluated in e if it's given
    public LVal Pop(int i, LEnv? e = null) {
        if (i >= Count) return Err($"Popping nonexistent item {i} from Expr");
        Mutable();
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
    const string StrEscapable = "\0\a\b\f\n\r\t\v\x1b\\\"";

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
            case '\x1b': return "\\e";
            case '\\': return "\\\\";
            case '\'': return "\\\'";
            case '\"': return "\\\"";
        }
        return "";
    }

    // A string as it's written, so it reads back as itself: escapes for the control characters (\xHH for those
    // without a letter), its other bytes as they are
    private string StrAsString() {
        var s = new StringBuilder();
        s.Append('"');
        /* Loop over the characters in the string */
        foreach (char c in StrVal) {
            if (StrEscapable.Contains(c)) {
                /* If the character is escapable then escape it */
                s.Append(StrEscape(c));
            } else if (c < ' ' || c == 127) {
                s.Append($"\\x{(int)c:X2}");
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
            case LE.BUFFER: s.Append("<buffer>{").AppendJoin(' ', BufferValue!).Append('}'); break;
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
        if (IsBuffer) return "(buffer {" + string.Join(' ', BufferValue!) + "})";

        var sb = new StringBuilder();
        if (IsQExpr) sb.Append('{');
        else if (IsSExpr) sb.Append('(');

        var pre = "";
        if (Count > 0) {
            foreach (var c in Cells!) {
                sb.Append(pre).Append(c.Serialize());
                pre = " ";
            }
        } else if (!IsQExpr && !IsSExpr) {
            // (an empty list is its brackets alone, {} or (), so it reads back as itself)
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
            case LE.BUFFER: return BufferValue.AsSpan().SequenceEqual(y.BufferValue);

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
            LE.BUFFER => "Buffer",
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
    private static int _depth;      // (the interpreter runs on one thread, Program's, as Site and the calls' stack say)

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

    // A function's arguments, cells[start...]: evaluated in e, left to right (the first error stops them: err); or,
    // for an fexpr, each as it is, the expression the caller wrote, remembering e (its Scope), so (eval x) evaluates
    // it there.  The code isn't changed: it's shared
    private static List<LVal> Args(LEnv e, LVal f, List<LVal> cells, int start, out LVal? err) {
        err = null;
        var vals = new List<LVal>(cells.Count - start);
        for (int i = start; i < cells.Count; i++) {
            if (f.IsFexpr) {
                vals.Add(cells[i].ScopedIn(e));
                continue;
            }
            var v = cells[i].Eval(e);
            if (v.IsErr) {
                err = v;
                break;
            }
            vals.Add(v);
        }
        return vals;
    }

    // A built-in's arguments, cells[start...].  A special form's are as they're written (more than it takes is an
    // error), a new list of the shared code.  An ordinary built-in's are evaluated, left to right, the first error the
    // call's value (but for one that takes errors as values: the type tests, type-of, error-message ...), then checked
    // as values (ValueArgs).  OUT: the call's value, if that's it (null: the call goes on, with a its arguments)
    private static LVal? BuiltinArgs(LEnv e, LVal f, List<LVal> cells, int start, out LVal a) {
        int bound = f.Bound?.Count ?? 0, n = bound + cells.Count - start;
        a = null!;
        if (n > f.MaxArgs) return TooMany(f, n);
        if (f.IsSpecial) {
            a = Sexpr();
            for (int i = start; i < cells.Count; i++) a.Cells!.Add(cells[i]);
            return null;
        }
        var vals = new List<LVal>(n);
        for (int i = start; i < cells.Count; i++) {
            var v = cells[i].Eval(e);
            if (v.IsErr && !f.TakesErrors) return v;
            vals.Add(v);
        }
        return ValueArgs(f, vals, out a);
    }

    private static LVal TooMany(LVal f, int n) =>
        Err($"'{f.BuiltinName}' takes {(f.MaxArgs == f.MinArgs ? "" : "at most ")}{f.MaxArgs} argument{(f.MaxArgs == 1 ? "" : "s")}, not {n}");

    // An ordinary built-in's values (a partially applied one's first): more than its most is an error, an error is the
    // call's value (as above), fewer than it needs is the built-in with those given, waiting for the rest (partial
    // application).  Values are shared: a built-in makes new ones, never changes those it's given.  OUT: the call's
    // value, if that's it (null: the call goes on, with a the values)
    private static LVal? ValueArgs(LVal f, List<LVal> vals, out LVal a) {
        a = Sexpr();
        if (f.Bound != null) a.Cells!.AddRange(f.Bound);
        foreach (var v in vals) {
            if (v.IsErr && !f.TakesErrors) return v;
            a.Cells!.Add(v);
        }
        if (a.Count > f.MaxArgs) return TooMany(f, a.Count);
        if (a.Count < f.MinArgs) {
            var p = f.Copy();
            p.Bound = new List<LVal>(a.Cells!);
            foreach (var b in p.Bound) b.Freeze();
            return p;
        }
        return null;
    }

    // A built-in applied to values (map's f, sort's less ...), as a call of it with them would be, not evaluated again
    public static LVal ApplyBuiltin(LEnv e, LVal f, List<LVal> vals) {
        if (f.IsSpecial) return Err($"'{f.BuiltinName}' can't be applied to values: it's a special form");
        return ValueArgs(f, vals, out var a) ?? CallBuiltin(e, f, a);
    }

    // f called with the arguments cells[start...] (evaluated as f wants them)
    public static LVal Call(LEnv e, LVal f, List<LVal> cells, int start) {
        if (f.BuiltinVal != null) return BuiltinArgs(e, f, cells, start, out var a) ?? CallBuiltin(e, f, a);
        var vals = Args(e, f, cells, start, out var err);
        if (err != null) return err;
        return Apply(f, vals);
    }

    // A function applied to values: they're bound in a new scope of its call (whose parent is the scope the function
    // was made in: lexical scope), after those a partial application bound (its Env), the formals first, then &1, &2
    // ... and &_ for the rest; if it has all it needs, its body runs there; if not, it's a new function with those
    // bound (partial application).  The function isn't changed.  A call in the body's tail position comes back as a
    // TAIL value and runs here, in its place
    public static LVal Apply(LVal f, List<LVal> vals, bool argsChecked = false) {
        if (_depth >= MaxDepth) return LVal.Err($"Too deep: more than {MaxDepth} calls nested");
        ++_depth;
        try {
            while (true) {
                var intr = CheckInterrupt();
                if (intr != null) return intr;
                var fi = f.Fn!;
                if (!argsChecked && !fi.IsFexpr) {
                    for (int i = 0; i < vals.Count; i++) if (vals[i].IsErr) return vals[i];
                }
                argsChecked = false;

                var formals = fi.Formals!.Cells!;
                var scope = new LEnv(fi.Closure) { IsCall = true };
                if (fi.Env != null && fi.Env.Count > 0) scope.CopyFrom(fi.Env);

                // (the arguments past the formals: &1, &2 ..., and &_ the list of them)
                LVal? extras = null;
                int k = 0;
                for (int i = 0; i < vals.Count; i++) {
                    var val = vals[i];
                    if (k < formals.Count) scope.Put(formals[k++].SymName, val);
                    else {
                        (extras ??= Qexpr()).Add(val);
                        scope.Put($"&{extras.Count}", val);
                    }
                }
                if (extras != null) scope.Put(Name.Rest, extras);     // (none: NIL, the call scope's own)

                if (k < formals.Count) {
                    var p = f.CloneFun();
                    p.Formals = Qexpr();
                    for (int i = k; i < formals.Count; i++) p.Formals.Add(formals[i]);
                    p.Formals.Freeze();
                    p.Env = new LEnv();
                    foreach (var kv in scope.Entries) p.Env.SetLocal(kv.Key, kv.Value);
                    return p;
                }
                var body = fi.Body!;
                if (extras != null && !body.Mentions(Uses.Extras)) {
                    var takes = vals.Count - extras.Count;
                    return LVal.Err($"The function takes {takes} argument{(takes == 1 ? "" : "s")}, not {vals.Count}");
                }

                if (!body.IsQExpr) return LVal.Err("A function's body must be a QExpr");
                var r = body.CallOf().Tail(scope);
                if (r.ValType != LE.TAIL) return r;
                f = r.TailFn!;
                vals = r.TailArgs!;
            }
        }
        finally {
            --_depth;
        }
    }

    // (exit code): the program ends with that status
    private static LVal ExitWith(LEnv e, LVal x) {
        var code = x.Eval(e);
        if (code.IsErr) return code;
        if (!code.IsNum) return LVal.Err("'exit' expects a Number");
        throw new ExitException((int)code.NumVal!.ToInt().num);
    }

    // An S-expression evaluated (or a list's items, as one: a function's body, code run): its compiled code's value
    // (Compiler.cs); the code isn't changed
    public static LVal? EvalSExpr(LEnv e, LVal? v) => v?.CallOf().Eval(e);

    // A value run as code: a list's items as an S-expression, anything else evaluated
    public static LVal EvalCode(LEnv e, LVal x) => x.IsQExpr ? x.CallOf().Eval(e) : x.Eval(e);

    // A hash called, (h key arg...): the value at key (cells[start], evaluated; start moves past it); a function there
    // is a method, given back with &0 the hash (through which its private entries are had), to be applied to the
    // arguments that follow as any function is (fewer than its formals: partially applied); anything else is the value,
    // and arguments after it are an error
    private static LVal HashAt(LEnv e, LVal h, List<LVal> cells, ref int start) {
        var key = cells[start++].Eval(e);
        if (key.IsErr) return key;
        var value = h.HashValue!.Get(key);
        if (value.IsErr) return value;
        if (value.IsFun) return Method(value, h);
        if (start < cells.Count) return Err($"{key.ToStr()} isn't a method: its value isn't a function");
        return value;
    }

    // A hash's function as a method: a copy of it with &0 the hash (a built-in as it is)
    public static LVal Method(LVal f, LVal h) {
        if (f.BuiltinVal != null || !f.Body!.Mentions(Uses.Self)) return f;    // (&0 nowhere in its body: none bound)
        var m = f.CloneFun();
        m.Env = new LEnv();
        if (f.Env != null) foreach (var kv in f.Env.Entries) m.Env.SetLocal(kv.Key, kv.Value);
        m.Env.Put("&0", Hash(h.HashValue!.PrivateCallProxy));
        return m;
    }

    // The file being read (load's), for the code's places
    public static string? SourceFile;

    // The expressions the tokens make: a list, as the reader's lists are (each S- or Q-expression's place kept: the
    // file and the line it starts on)
    public static LVal? ReadExprFromTokens(List<Parser.Token> tokens, char? end = null) {
        int at = 0;
        var r = ReadExprFromTokens(tokens, ref at, end, tokens.Count > 0 ? tokens[0].line : 0);
        tokens.RemoveRange(0, at);
        return r;
    }

    private static LVal? ReadExprFromTokens(List<Parser.Token> tokens, ref int at, char? end, int line) {
        LVal exp = end == '}' ? Qexpr() : Sexpr();
        exp._x = new CodeInfo { File = SourceFile, Line = line, Code = exp };
        var t = at < tokens.Count ? tokens[at] : null;
        while (t != null) {
            at++;
            LVal? val = t.type switch {
                Parser.Token.Type.EOF => end == null ? null : Err($"Missing {end} for {(end == '}' ? 'Q' : 'S')}Expr"),
                Parser.Token.Type.Comment => Comment(t.str!),
                Parser.Token.Type.Error => Err(t.str ?? "Unknown error"),
                Parser.Token.Type.SExOpen => ReadExprFromTokens(tokens, ref at, ')', t.line),
                Parser.Token.Type.QExOpen => ReadExprFromTokens(tokens, ref at, '}', t.line),
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
            t = at < tokens.Count ? tokens[at] : null;
        }

        return exp;
    }

    // A value: a symbol's, an S-expression's (a call), or this one.  A Q-expression remembers the scope it's written
    // in: eval runs it there (so code passed to a function runs where it was written, and a function's own variables
    // are its alone); the code isn't changed, so a copy of it remembers it
    public LVal Eval(LEnv e) {
        switch (ValType) {
            case LE.SYM:   return e.Get(SymName);
            case LE.SEXPR: return CallOf().Eval(e);
            case LE.QEXPR: return Scope == null && Count > 0 ? ScopedIn(e) : this;
            default:       return this;
        }
    }

    // The order of values (cmp, <, >, sort): one for all of them, by kind first (numbers, characters, strings, atoms,
    // symbols, lists, T, functions, hashes, streams, errors), then by value: numbers by value (whatever their kinds),
    // characters and strings by their codes, atoms and symbols by name, lists item by item (a shorter one first, when
    // it's the start of the other).  Equal values (Equals) are in the same place
    public int CompareTo(LVal v) {
        int Rank(LVal x) => x.ValType switch {
            LE.NUM => 0, LE.CHAR => 1, LE.STR => 2, LE.ATOM => 3, LE.SYM => 4, LE.QEXPR => 5, LE.SEXPR => 6,
            LE.T => 7, LE.FUN => 8, LE.HASH => 9, LE.BUFFER => 10, LE.STREAM => 11, LE.ERR => 12, _ => 13
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
            case LE.BUFFER:   return Math.Sign(BufferValue.AsSpan().SequenceCompareTo(v.BufferValue));
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