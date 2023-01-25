public class LVal {
    public enum LE { LVAL_ERR, LVAL_NUM, LVAL_SYM, LVAL_STR, LVAL_FUN, LVAL_SEXPR, LVAL_QEXPR, LVAL_COMMENT };

    public LE ValType;
    public Func<LEnv, LVal, LVal>? BuiltinVal = null;
    public LEnv? Env = null;
    public LVal? Formals = null;
    public LVal? Body = null;
    public Num? NumVal = null;
    public string ErrVal = string.Empty;
    public string SymVal = string.Empty;
    public string StrVal = string.Empty;
    public List<LVal>? Cells = null;

    public int Count => Cells?.Count ?? 0;

    public LVal Copy() {
        LVal x = new LVal();
        x.ValType = ValType;
        switch (ValType) {
            case LE.LVAL_FUN:
                if (BuiltinVal != null) {
                    x.BuiltinVal = BuiltinVal;
                } else {
                    x.BuiltinVal = null;
                    x.Env = Env?.Copy();
                    x.Formals = Formals?.Copy();
                    x.Body = Body?.Copy();
                }
                break;

            case LE.LVAL_NUM: x.NumVal = NumVal; break;
            case LE.LVAL_ERR: x.ErrVal = ErrVal; break;
            case LE.LVAL_SYM: x.SymVal = SymVal; break;
            case LE.LVAL_STR: x.StrVal = StrVal; break;

            case LE.LVAL_SEXPR:
            case LE.LVAL_QEXPR:
                x.Cells = Cells?.Select(c => c.Copy()).ToList();
                break;
        }
        return x;
    }

    public static LVal Number(Num x) {
        LVal v = new LVal();
        v.ValType = LE.LVAL_NUM;
        v.NumVal = x;
        return v;
    }

    public static LVal Err(string errStr) {
        LVal v = new LVal();
        v.ValType = LE.LVAL_ERR;  
        v.ErrVal = errStr;
        return v;
    }

    public static LVal Sym(string s) {
        LVal v = new LVal();
        v.ValType = LE.LVAL_SYM;
        v.SymVal = s;
        return v;
    }

    public static LVal Str(string s) {
        LVal v = new LVal();
        v.ValType = LE.LVAL_STR;
        v.StrVal = s;
        return v;
    }

    public static LVal Builtin(Func<LEnv, LVal, LVal> func) {
        LVal v = new LVal();
        v.ValType = LE.LVAL_FUN;
        v.BuiltinVal = func;
        return v;
    }

    public static LVal Lambda(LVal formals, LVal body) {
        LVal v = new LVal();
        v.ValType = LE.LVAL_FUN;  
        v.BuiltinVal = null;  
        v.Env = new LEnv();  
        v.Formals = formals;
        v.Body = body;
        // v.Println();
        return v;
    }

    public static LVal Sexpr() {
        LVal v = new LVal();
        v.ValType = LE.LVAL_SEXPR;
        v.Cells = new List<LVal>();
        return v;
    }

    public static LVal Qexpr() {
        LVal v = new LVal();
        v.ValType = LE.LVAL_QEXPR;
        v.Cells = new List<LVal>();
        return v;
    }

    public static LVal Comment(string s) {
        LVal v = new LVal();
        v.ValType = LE.LVAL_COMMENT;
        v.StrVal = s;
        return v;
    }

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

    public LVal Pop(int i) {
        if (Cells == null || i >= Cells.Count) return Err($"Popping nonexistent item {i} from Expr");
        LVal x = Cells[i];
        Cells.RemoveAt(i);
        return x;
    }

    public LVal Take(int i) => Pop(i);

    private void PrintExpr(char open, char close) {
        Console.Write(open);
        var con = "";
        if (Cells != null) {
            foreach (var c in Cells) {
                Console.Write(con);
                con = " ";
                c.Print();
            }
        }

        Console.Write(close);
    }

    /* Possible unescapable characters */
    const string str_unescapable = "abfnrtv\\\'\"";

    /* Function to unescape characters */
    private static char str_unescape(char x) {
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
    const string str_escapable = "\a\b\f\n\r\t\v\\\'\"";

    /* Function to escape characters */
    private static string str_escape(char x) {
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

    private void PrintStr() {
        Console.Write('"');
        /* Loop over the characters in the string */
        foreach (char c in StrVal) {
            if (str_escapable.Contains(c)) {
                /* If the character is escapable then escape it */
                Console.Write(str_escape(c));
            } else {
                /* Otherwise print character as it is */
                Console.Write(c);
            }
        }
        Console.Write('"');
    }

    public void Print() {
        switch (ValType) {
            case LE.LVAL_FUN:
                if (BuiltinVal != null) {
                    Console.Write($"<builtin>");
                } else {
                    Console.Write("<function>(fn ");
                    Formals!.Print();
                    Console.Write(' ');
                    Body!.Print();
                    Console.Write(')');
                }
                break;

            case LE.LVAL_NUM:   Console.Write(NumVal); break;
            case LE.LVAL_ERR:   Console.Write($"Error: {ErrVal}"); break;
            case LE.LVAL_SYM:   Console.Write(SymVal); break;
            case LE.LVAL_STR:   PrintStr(); break;
            case LE.LVAL_SEXPR: PrintExpr('(', ')'); break;
            case LE.LVAL_QEXPR: PrintExpr('{', '}'); break;
        }
    }

    public void Println() { Print(); Console.WriteLine(); }

    public override bool Equals(object? o) {
        var y = o as LVal;
        if (y is null) return false;

        if (ValType != y.ValType) return false;
    
        switch (ValType) {
            case LE.LVAL_NUM: return (NumVal!.CompareTo(y.NumVal) == 0);    
            case LE.LVAL_ERR: return (ErrVal == y.ErrVal);
            case LE.LVAL_SYM: return (SymVal == y.SymVal);    
            case LE.LVAL_STR: return (StrVal == y.StrVal);    
            case LE.LVAL_FUN: 
                if (BuiltinVal != null || y.BuiltinVal != null) {
                    return BuiltinVal == y.BuiltinVal;
                }
                return (Formals!.Equals(y.Formals) && Body!.Equals(y.Body));

            case LE.LVAL_QEXPR:
            case LE.LVAL_SEXPR:
                if (Cells!.Count != y.Cells!.Count) return false;
                for (int i = 0; i < Cells!.Count; i++) {
                    if (!Cells[i].Equals(y.Cells[i])) return false;
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

    private static string ltype_name(LE t) {
        switch(t) {
            case LE.LVAL_FUN: return "Function";
            case LE.LVAL_NUM: return "Number";
            case LE.LVAL_ERR: return "Error";
            case LE.LVAL_SYM: return "Symbol";
            case LE.LVAL_STR: return "String";
            case LE.LVAL_SEXPR: return "S-Expression";
            case LE.LVAL_QEXPR: return "Q-Expression";
            default: return "Unknown";
        }
    }

    private static LVal Call(LEnv e, LVal f, LVal a) {
        if (f.BuiltinVal != null) { return f.BuiltinVal(e, a); }
        
        int given = a.Count;
        int total = f.Formals!.Count;
        // Console.Write($"Function passed {given} arguments. Expected {total}. Formals: "); f.Formals?.Print(); Console.WriteLine();
        int i = 0;
        var extras = Qexpr();
        
        while (a.Count > 0) {
            ++i;
            LVal val = a.Pop(0);
            if (f.Formals!.Count == 0) {
                f.Env!.Put($"&{i}", val);
                // Console.Write($"Setting &{i} to "); val.Print(); Console.WriteLine();
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

    public static LVal? eval_sexpr(LEnv e, LVal? v) {
        if (v == null) return null;
        // v.Println();
        for (int i = 0; i < v.Count; i++) { v.Cells![i] = v.Cells![i].Eval(e)!; }
        for (int i = 0; i < v.Count; i++) { if (v.Cells![i].ValType == LE.LVAL_ERR) { return v.Take(i); } }
        
        if (v.Count == 0) { return v; }  
        if (v.Count == 1) { return v.Take(0).Eval(e); }
        
        // Console.Write("Evaluating: ");v.Println();
        LVal f = v.Pop(0);
        if (f.ValType != LE.LVAL_FUN) {
            LVal err = LVal.Err($"S-Expression starts with incorrect type. Got {ltype_name(f.ValType)}, Expected {ltype_name(LE.LVAL_FUN)}.");
            return err;
        }
        
        LVal result = LVal.Call(e, f, v);
        //result.Print();
        return result;
    }

    public static LVal? read_expr_from_tokens(List<Parser.Token> tokens, char? end = null) {
        LVal exp = end == '}' ? Qexpr() : Sexpr();
        var t = tokens.FirstOrDefault();
        while (t != null) {
            tokens.RemoveAt(0);
            LVal? val = t.type switch {
                Parser.Token.Type.EOF => end == null ? null : Err("Missing SExClose for SExpr"),
                Parser.Token.Type.Comment => Comment(t.str!),
                Parser.Token.Type.Error => Err(t.str ?? "Unknown error"),
                Parser.Token.Type.SExOpen => read_expr_from_tokens(tokens, ')'),
                Parser.Token.Type.QExOpen => read_expr_from_tokens(tokens, '}'),
                Parser.Token.Type.SExClose => end != ')' ? Err("SExClose without SExOpen") : null,
                Parser.Token.Type.QExClose => end != '}' ? Err("QExClose without QExOpen") : null,
                Parser.Token.Type.Number => Number(t.num!),
                Parser.Token.Type.String => Str(t.str!),
                Parser.Token.Type.Symbol => Sym(t.str!),
                _ => Err($"Unknown token type {t.type}")
            };

            if (val == null) break;
            
            switch (val.ValType) {
                case LE.LVAL_ERR: throw new Exception(val.ErrVal);
                case LE.LVAL_COMMENT: break;
                default:
                    exp.Cells!.Add(val);
                    break;
            }
            t = tokens.FirstOrDefault();
        }

        return exp;

    }

    public LVal Eval(LEnv e) {
        if (ValType == LE.LVAL_SYM) return e.Get(SymVal!);
        if (ValType == LE.LVAL_SEXPR) return eval_sexpr(e, this)!;
        
        // Print();
        return this;
    }
}