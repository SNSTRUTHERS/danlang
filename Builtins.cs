using System.Numerics;
using System.Threading;
public class Builtins
{ 
    private static void AddBuiltin(LEnv e, string name, Func<LEnv, LVal, LVal> func) {
        LVal k = LVal.Sym(name);
        LVal v = LVal.Builtin(func);
        e.Put(k.SymVal!, v);
    }

    private static LVal Lambda(LEnv e, LVal a) {
        LVal formals = a.Pop(0);
        LVal body = a.Pop(0);        
        return LVal.Lambda(formals, body);
    }

    public static LVal List(LEnv e, LVal a) {
        if (a.ValType != LVal.LE.SEXPR) return LVal.Err("'list' can only be applied to a SExpr");
        a.ValType = LVal.LE.QEXPR;
        return a;
    }

    private static LVal Head(LEnv e, LVal a) {
        LVal v = a.Pop(0);  
        while (v.Count > 1) v.Pop(1);
        return v;
    }

    private static LVal Tail(LEnv e, LVal a) {
        LVal v = a.Pop(0);  
        v.Pop(0);
        return v;
    }

    private static LVal Init(LEnv e, LVal a) {
        if (a == null || a.Count == 0) return LVal.NIL();
        LVal v = a.Pop(0);
        v.Pop(v.Count - 1);
        return v;
    }

    private static LVal End(LEnv e, LVal a) {
        LVal v = a.Pop(0);  
        while (v.Count > 1) v.Pop(0);
        return v;
    }

    public static LVal Eval(LEnv e, LVal a) {
        if (a.Count != 1 || a[0].ValType != LVal.LE.QEXPR) return LVal.Err("Invalid parameter(s) passed to 'eval'");
        LVal x =  a.Pop(0);
        x.ValType = LVal.LE.SEXPR;
        return x.Eval(e)!;
    }

    private static LVal Join(LEnv e, LVal a) {
        if (a.Cells!.Any(v => v.ValType != LVal.LE.QEXPR)) return LVal.Err("Non-QExpr in parameters list for 'join'");
        LVal x =  a.Pop(0);
        
        while (a.Count > 0) {
            LVal y = a.Pop(0);
            x = x.Join(y);
        }
        return x;
    }

    private static LVal Op(LEnv e, LVal a, string op) {
        if (a.Cells!.Any(v => v.ValType != LVal.LE.NUM)) return LVal.Err($"Non-NumVal in parameters list for '{op}'");
        LVal x =  a.Pop(0);
        
        if ((op == "-") && a.Count == 0) { x.NumVal = -x.NumVal!; }
        
        while (a.Count > 0) {  
            LVal y = a.Pop(0);
            
            if      (op == "+") { x.NumVal = x.NumVal! + y.NumVal!; }
            else if (op == "-") { x.NumVal = x.NumVal! - y.NumVal!; }
            else if (op == "*") { x.NumVal = x.NumVal! * y.NumVal!; }
            else if (op == "/") {
                if (y.NumVal!.ToInt().num == 0) {
                    x = LVal.Err("Division By Zero.");
                    break;
                }
                x.NumVal = x.NumVal! / y.NumVal;
            }
        }

        return x;
    }

    private static LVal Add(LEnv e, LVal a) { return Op(e, a, "+"); }
    private static LVal Sub(LEnv e, LVal a) { return Op(e, a, "-"); }
    private static LVal Mul(LEnv e, LVal a) { return Op(e, a, "*"); }
    private static LVal Div(LEnv e, LVal a) { return Op(e, a, "/"); }

    private static LVal Var(LEnv e, LVal a, string func) {
        //if (a.Count == 0 || a.ValType != LVal.LE.QEXPR) return LVal.Err($"Invalid parameter(s) passed to '{func}'");
        LVal syms = a[0];
        if (syms.Cells!.Any(v => v.ValType != LVal.LE.SYM)) return LVal.Err($"'{func}' cannot define non-symbols");
        if (syms.Count != a.Count - 1) return LVal.Err($"'{func}' passed too many arguments or symbols.  Expected {syms.Count}, got {a.Count - 1}");
            
        for (int i = 0; i < syms.Count; i++) {
            var symbol = syms[i].SymVal!;
            var value = a[i+1].Eval(e);
            if (func == "def") { e.Def(symbol, value) ; }
            if (func == "set") { e.Put(symbol, value); } 
        }
        
        return LVal.NIL();
    }

    private static LVal Def(LEnv e, LVal a) { return Var(e, a, "def"); }
    private static LVal Put(LEnv e, LVal a) { return Var(e, a, "set"); }

    private static LVal Ord(LEnv e, LVal a, string op) {
        if (a.Count != 2) return LVal.Err($"Too few parameters passed to '{op}'");
        if (a[1].ValType != LVal.LE.NUM || a[2].ValType != LVal.LE.NUM) return LVal.Err($"'{op}' passed non-number parameter(s)");
        bool r = false;
        if (op == ">") { r = ((a[0].NumVal as Int)!.num > (a[1].NumVal as Int)!.num); }
        if (op == "<") { r = ((a[0].NumVal as Int)!.num < (a[1].NumVal as Int)!.num); }
        return LVal.Bool(r);
    }

    private static LVal Gt(LEnv e, LVal a) { return Ord(e, a, ">");  }
    private static LVal Lt(LEnv e, LVal a) { return Ord(e, a, "<");  }

    private static LVal Cmp(LEnv e, LVal a, string op) {
        if (a.Count != 2) return LVal.Err($"Too few parameters passed to '{op}'");
        bool r = false;
        if (op == "eq")       r =  a[0].Equals(a[1]);
        else if (op == "neq") r = !a[0].Equals(a[1]);
        else if (op == "cmp") return LVal.Number(a[0].CompareTo(a[1]));

        return LVal.Bool(r);
    }

    private static LVal Eq(LEnv e, LVal a) { return Cmp(e, a, "eq"); }
    private static LVal Neq(LEnv e, LVal a) { return Cmp(e, a, "neq"); }
    private static LVal Cmp(LEnv e, LVal a) { return Cmp(e, a, "cmp"); }

    private static LVal If(LEnv e, LVal a) {
        if (a.Count < 2) return LVal.Err("'if' supplied too few parameters");
        if (a[1].ValType != LVal.LE.QEXPR) return LVal.Err("'if' body elements must be QExpr");

        a[1].ValType = LVal.LE.SEXPR;
        if (a.Count == 2) { // no else, so add an empty else
            a.Add(LVal.NIL());
        }

        if (a[2].ValType != LVal.LE.QEXPR) return LVal.Err("'if' body elements must be QExpr");
        a[2].ValType = LVal.LE.SEXPR;

        if (!a[0].IsNIL) {
            return a.Pop(1).Eval(e)!;
        } else {
            return a.Pop(2).Eval(e)!;
        }
    }

    private static Num IntZero = new Int(0);
    private static LVal SpaceShip(LEnv e, LVal a) {
        bool IsMatch(Num n, LVal l) {
            if (l.Count == 1) return true;
            switch (l[0].SymVal) {
                case "=":
                    return n.CompareTo(IntZero) == 0;
                case "<>":
                    return n.CompareTo(IntZero) != 0;
                case ">":
                    return n.CompareTo(IntZero) > 0;
                case "<":
                    return n.CompareTo(IntZero) < 0;
                case ">=":
                case "=>":
                    return n.CompareTo(IntZero) >= 0;
                case "<=":
                case "=<":
                    return n.CompareTo(IntZero) <= 0;
                default:
                    throw new InvalidOperationException($"Unknown comparison test {l[0].SymVal}");
            }
        }

        if (a.Count < 2) return LVal.Err("Too few parameters passed to '<=>' operator");
        if (a.Count > 4) return LVal.Err("Too many parameters passed to '<=>' operator");

        for (var i = 1; i < a.Count; ++i) if (a[i].ValType != LVal.LE.QEXPR) return LVal.Err("Operator '<=>' received one or more invalid case blocks (not QExpr)");

        var cmpVal = a.Pop(0);
        if (cmpVal.ValType != LVal.LE.NUM) return LVal.Err("First parameter to '<=>' must evaluate to a Number");

        // TODO: check the structure of the "cases" to ensure that there is no duplication, that the first one always has a compare value, and only the last one has no comparator

        var cmp = cmpVal.NumVal;
        if (cmp != null) {
            var i = 0;
            while (a.Count > 0) {
                var c = a.Pop(0);
                if (a.Count != 0 && c.Count < 2) return LVal.Err("All but last body block of a '<=>' operator must have comparator");
                try {
                    if (IsMatch(cmp, c)) return c.Pop(c.Count - 1).Eval(e);
                } catch (InvalidOperationException ex) {
                    return LVal.Err(ex.Message);
                }
                ++i;
            }
        }

        return LVal.NIL();
    }

    public static LVal Load(LEnv e, LVal a) {
        if (a.Count == 0) return LVal.Err("'if' supplied too few parameters");
        if (a.Cells!.Any(c => c.ValType != LVal.LE.STR)) return LVal.Err("'load' passed non-string parameter(s)"); 
        
        /* Open file and check it exists */
        while (a.Count > 0) {
            var v = a.Pop(0);
            var filename = v.StrVal!;
            if (!File.Exists(filename)) {
                if (!filename.EndsWith(".dl")) filename += ".dl";
                if (!File.Exists(filename)) {
                    if (!filename.Contains(Path.PathSeparator)) filename = Path.Join("lib", filename);
                    if (!File.Exists(filename)) return LVal.Err($"File {filename} does not exist");
                }
            }

            using var f = File.Open(filename, FileMode.Open);
            if (f == null) return LVal.Err($"Could not load Library {filename}");
        
            /* Read File Contents */
            Console.Write($"Loading '{filename}'...");
            var input = new StreamReader(f).ReadToEnd();
            var tokens = Parser.Tokenize(new StringReader(input)).ToList();
            var expr = LVal.ReadExprFromTokens(tokens);

            if (expr != null && expr.ValType != LVal.LE.ERR) {
                while (expr.Count > 0) {
                    LVal x = expr.Pop(0).Eval(e)!;
                    if (x.ValType == LVal.LE.ERR) { x.Println(); }
                }
            } else {
                expr?.Println();
            }

            Console.WriteLine("Complete!");
        }
        return LVal.NIL();
    }

    private static LVal Print(LEnv e, LVal a) {  
        for (int i = 0; i < a.Count; i++) {
            a[i].Print(); Console.Write(' ');
        }
        Console.Write('\n');
        return LVal.NIL();
    }

    private static LVal Error(LEnv e, LVal a) {
        // LASSERT_NUM("error", a, 1);
        // LASSERT_TYPE("error", a, 0, LE.STR);
        return LVal.Err(a[0].StrVal!);  
    }

    private static LVal IsType(LVal a, LVal.LE type) {
        return LVal.Bool(a[0].ValType == type);
    }

    private static LVal IsNumber(LEnv e, LVal a) => IsType(a, LVal.LE.NUM);
    private static LVal IsString(LEnv e, LVal a) => IsType(a, LVal.LE.STR);
    private static LVal IsSymbol(LEnv e, LVal a) => IsType(a, LVal.LE.SYM);
    private static LVal IsFunc(LEnv e, LVal a) => IsType(a, LVal.LE.FUN);
    private static LVal IsError(LEnv e, LVal a) => IsType(a, LVal.LE.ERR);
    private static LVal IsQExpr(LEnv e, LVal a) => IsType(a, LVal.LE.QEXPR);
    private static LVal IsSExpr(LEnv e, LVal a) => IsType(a, LVal.LE.SEXPR);
    private static LVal IsNIL(LEnv e, LVal a) => LVal.Bool(a[0].IsNIL);
    private static LVal IsT(LEnv e, LVal a) => IsType(a, LVal.LE.T);
    private static LVal IsExpr(LEnv e, LVal a) {
        switch (a[0].ValType) {
            case LVal.LE.STR:
            case LVal.LE.NUM:
            case LVal.LE.T:
            case LVal.LE.SYM:
            case LVal.LE.QEXPR:
            case LVal.LE.SEXPR: return LVal.T();
        }
        return LVal.NIL();
    }
    private static LVal IsZero(LEnv e, LVal a) {
        if (a.Count == 0) return LVal.Err("");
        if (a[0].ValType != LVal.LE.NUM) return LVal.Err("");
        return LVal.Bool(a[0].NumVal == IntZero);
    }

    private static LVal FastFib(LEnv env, LVal val) {
        int n = (int)val[0].NumVal!.ToInt().num;
        BigInteger a = BigInteger.Zero;
        BigInteger b = BigInteger.One;
        for (int i = 31; i >= 0; i--) {
            BigInteger d = a * (b * 2 - a);
            BigInteger e = a * a + b * b;
            a = d;
            b = e;
            if ((((uint)n >> i) & 1) != 0) {
                BigInteger c = a + b;
                a = b;
                b = c;
            }
        }
        return LVal.Number(a);
    }

    public static void AddBuiltins(LEnv e) {
        // Variable Functions
        AddBuiltin(e, "fn",  Lambda); 
        AddBuiltin(e, "def", Def);
        AddBuiltin(e, "set", Put);
        
        // List Functions
        AddBuiltin(e, "list", List);
        AddBuiltin(e, "head", Head);
        AddBuiltin(e, "tail", Tail);
        AddBuiltin(e, "init", Init);
        AddBuiltin(e, "end",  End);
        AddBuiltin(e, "join", Join);
        AddBuiltin(e, "eval", Eval);
        
        // Mathematical Functions
        AddBuiltin(e, "+", Add);
        AddBuiltin(e, "-", Sub);
        AddBuiltin(e, "*", Mul);
        AddBuiltin(e, "/", Div);
        
        // Comparison Functions
        AddBuiltin(e, "if",  If);
        AddBuiltin(e, "eq",  Eq);
        AddBuiltin(e, "neq", Neq);

        AddBuiltin(e, ">",   Gt);
        AddBuiltin(e, "<",   Lt);
        AddBuiltin(e, "cmp", Cmp);
        AddBuiltin(e, "<=>", SpaceShip);
        
        // String Functions
        AddBuiltin(e, "load",  Load); 
        AddBuiltin(e, "error", Error);
        AddBuiltin(e, "print", Print);

        // Other numeric functions
        // TODO: move the bodies of these definitions to static methods with error checking
        AddBuiltin(e, "val",         (e, p) => LVal.Number(Num.Parse(p?[0].StrVal ?? "0")!));
        AddBuiltin(e, "to-fixed",    (e, p) => LVal.Number(Rat.ToRat((p[0].NumVal ?? new Int(0))).ToFix(p.Count > 1 ? (int)(((p[1].NumVal as Int)?.num ?? 0)) : 10)));
        AddBuiltin(e, "to-rational", (e, p) => LVal.Number(Rat.ToRat(p[0].NumVal!)));
        AddBuiltin(e, "truncate",    (e, p) => LVal.Number((p[0].NumVal?.ToInt() ?? new Int(0))));
        AddBuiltin(e, "fib",         FastFib);

        // helper
        AddBuiltin(e, "defined?",    (e, p) => LVal.Bool(p[0].ValType == LVal.LE.SYM && e.ContainsKey(p[0].SymVal)));

        // type checking
        AddBuiltin(e, "t?",      IsT);
        AddBuiltin(e, "nil?",    IsNIL);
        AddBuiltin(e, "num?",    IsNumber);
        AddBuiltin(e, "symbol?", IsSymbol);
        AddBuiltin(e, "string?", IsString);
        AddBuiltin(e, "func?",   IsFunc);
        AddBuiltin(e, "error?",  IsError);
        AddBuiltin(e, "expr?",   IsExpr);
        AddBuiltin(e, "qexpr?",  IsQExpr);
        AddBuiltin(e, "sexpr?",  IsSExpr);
    }
}