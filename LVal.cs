using System.Numerics;
using System.Text;

public class LVal {
    public enum LE { ERR, T, NUM, ATOM, SYM, CHAR, STR, FUN, SEXPR, QEXPR, HASH, STREAM, COMMENT, EXIT };

    public LE ValType;
    public LEnv? Env = null;
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
                } else {
                    x.BuiltinVal = null;
                    x.Env = Env?.Copy();
                    x.Formals = Formals?.Copy();
                    x.Body = Body?.Copy();
                }
                break;

            case LE.NUM: x.NumVal = NumVal; break;
            case LE.ERR: x.ErrVal = ErrVal; break;

            case LE.ATOM:
            case LE.SYM: x.SymVal = SymVal; break;

            case LE.CHAR:
            case LE.STR: x.StrVal = StrVal; break;

            case LE.SEXPR:
            case LE.QEXPR:
                x.Cells = Cells?.Select(c => c.Copy()).ToList();
                break;
        }
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

    public static LVal Lambda(LVal formals, LVal body) {
        LVal v = new LVal();
        v.ValType = LE.FUN;  
        v.BuiltinVal = null;  
        v.Env = new LEnv();  
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

    public static LVal Exit() {
        LVal v = new LVal();
        v.ValType = LE.EXIT;
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
                } else if (entry.Count > 1 && entry[0].IsAtom && e != null) {
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
    const string StrEscapable = "\a\b\f\n\r\t\v\\\'\"";

    /* Function to escape characters */
    private static string StrEscape(char x) {
        switch (x) {
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
            case LE.STR:   s.Append('"').Append(StrVal).Append('"'); break;
            case LE.HASH:  s.Append("<hash>").Append(HashValue!.ToQexpr().ToStr()); break;
            case LE.SEXPR: return ExprAsString('(', ')');
            case LE.QEXPR: return ExprAsString('{', '}');
        }
        return s.ToString();
    }

    public string Serialize() {
        if (IsHash) return HashValue!.Serialize();

        var sb = new StringBuilder();
        if (IsQExpr) sb.Append('{');
        else if (IsSExpr) sb.Append('(');

        var i = 0;
        var pre = "";
        if (Count > 0) {
            sb.Append(pre).Append(Cells![i++].Serialize());
            pre = "\n";
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

            case LE.HASH: return base.Equals(o);  // TODO: do a memberwise check?

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

    public static LVal Call(LEnv e, LVal f, LVal a) {
        if (f.BuiltinVal != null) { return f.BuiltinVal(e, a); }
        
        int given = a.Count;
        int total = f.Formals!.Count;
        int i = 0;
        var extras = Qexpr();
        
        while (a.Count > 0) {
            ++i;
            LVal val = a.Pop(0, e);
            if (f.Formals!.Count == 0) {
                f.Env!.Put($"&{i}", val);
                extras.Add(val);
            } else {
                LVal sym = f.Formals.Pop(0);
                f.Env!.Put(sym.SymVal!, val);
            }
        }
        
        f.Env!.Put("&_", extras);
        if (f.Formals!.Count == 0) {
            f.Env!.Parent = e;
            return Builtins.Eval(f.Env, LVal.Sexpr().Add(f.Body!.Copy()));
        } else {
            return f.Copy()!;
        }
    }

    public static LVal? EvalSExpr(LEnv e, LVal? v) {
        if (v == null) return null;
        if (v.Count == 0) return NIL();

        LVal f = v.Pop(0, e);
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
        
        // Print();
        return this;
    }

    public int CompareTo(LVal v) {
        if (NumVal != null && v.NumVal != null) return NumVal.CompareTo(v.NumVal);
        if (IsStr && v.IsStr) return string.Compare(StrVal, v.StrVal, StringComparison.CurrentCulture);
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
        return string.Compare(ToStr(), v.ToStr(), StringComparison.CurrentCulture);
    }
}