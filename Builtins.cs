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
        if (!a.IsSExpr) return LVal.Err("'list' can only be applied to a SExpr");
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
        if (a.Count != 1 || !a[0].IsQExpr) return LVal.Err("Invalid parameter(s) passed to 'eval'");
        LVal x =  a.Pop(0);
        x.ValType = LVal.LE.SEXPR;
        return x.Eval(e)!;
    }

    private static LVal Join(LEnv e, LVal a) {
        if (a.Cells!.Any(v => !v.IsQExpr)) return LVal.Err("Non-QExpr in parameters list for 'join'");
        LVal x =  a.Pop(0);
        
        while (a.Count > 0) {
            LVal y = a.Pop(0);
            x = x.Join(y);
        }
        return x;
    }

    private static LVal Op(LEnv e, LVal a, string op) {
        if (a.Cells!.Any(v => !v.IsNum)) return LVal.Err($"Non-NumVal in parameters list for '{op}'");
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
        //if (a.Count == 0 || !a.IsQExpr) return LVal.Err($"Invalid parameter(s) passed to '{func}'");
        LVal syms = a[0];
        if (syms.Cells!.Any(v => !v.IsSym)) return LVal.Err($"'{func}' cannot define non-symbols");
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
        if (!a[0].IsNum || !a[1].IsNum) return LVal.Err($"'{op}' passed non-number parameter(s)");
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

        // Actual logical check; all non-NIL expressions evaluate to T in the 'if' context
        if (!a[0].IsNIL) {
            if (!a[1].IsQExpr) return LVal.Err("'if' body elements must be QExpr");
            a[1].ValType = LVal.LE.SEXPR;
            return a.Pop(1).Eval(e)!;
        }

        // if no else, so return NIL
         if (a.Count == 2) return LVal.NIL();

        if (!a[2].IsQExpr) return LVal.Err("'if' body elements must be QExpr");
        a[2].ValType = LVal.LE.SEXPR;
        return a.Pop(2).Eval(e);
    }

    private static LVal And(LEnv e, LVal a) {
        if (a.Count < 2) return LVal.Err("'and' supplied too few parameters");
        if (a.Cells!.Any(c => !c.IsQExpr)) return LVal.Err("'and' can only operate on QExprs");

        while (a.Count > 0) {
            var b = a.Pop(0);
            b.ValType = LVal.LE.SEXPR;
            var la = b.Eval(e);
            if (la.IsNIL) return la;
        }
        return LVal.Bool(true);
    }

    private static LVal Or(LEnv e, LVal a) {
        if (a.Count < 2) return LVal.Err("'or' supplied too few parameters");
        if (a.Cells!.Any(c => !c.IsQExpr)) return LVal.Err("'or' can only operate on QExprs");
        while (a.Count > 0) {
            var b = a.Pop(0);
            b.ValType = LVal.LE.SEXPR;
            var la = b.Eval(e);
            if (!la.IsNIL) return LVal.Bool(true);
        }
        return LVal.Bool(false);
    }

    private static Num IntZero = new Int(BigInteger.Zero);

    private static LVal SpaceShip(LEnv e, LVal a) {
        bool IsMatch(Num n, LVal l) {
            if (l.Count == 1) return true;
            switch (l[0].SymVal) {
                case "=":
                    return n.CompareTo(IntZero) == 0;
                case "<>": // not equal
                    return n.CompareTo(IntZero) != 0;
                case ">":
                    return n.CompareTo(IntZero) > 0;
                case "<":
                    return n.CompareTo(IntZero) < 0;
                case ">=": // greater than or equal
                case "=>":
                    return n.CompareTo(IntZero) >= 0;
                case "<=": // less than or equal
                case "=<":
                    return n.CompareTo(IntZero) <= 0;
                default:
                    throw new InvalidOperationException($"Unknown comparison test {l[0].SymVal}");
            }
        }

        if (a.Count < 2) return LVal.Err("Too few parameters passed to '<=>' operator");
        if (a.Count > 4) return LVal.Err("Too many parameters passed to '<=>' operator");

        for (var i = 1; i < a.Count; ++i) if (!a[i].IsQExpr) return LVal.Err("Operator '<=>' received one or more invalid case blocks (not QExpr)");

        var cmpVal = a.Pop(0);
        if (!cmpVal.IsNum) return LVal.Err("First parameter to '<=>' must evaluate to a Number");

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
        if (a.Cells!.Any(c => !c.IsStr)) return LVal.Err("'load' passed non-string parameter(s)"); 
        
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

            if (expr != null && !expr.IsErr) {
                while (expr.Count > 0) {
                    LVal x = expr.Pop(0).Eval(e)!;
                    if (x.IsErr) { x.Println(); }
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

    private static LVal IsInt(LEnv _, LVal a) {
        if (a.Count != 1) return LVal.Err("'int?' requires 1 parameter");
        if (!a[0].IsNum) return LVal.Err("Parameter passed to 'int?' is not a Number");
        if (a[0].NumVal is Int i && !(i is Fix) && !(i is Rat)) return LVal.Bool(true);
        return LVal.Bool(false);
    }
    private static LVal IsFix(LEnv _, LVal a) {
        if (a.Count != 1) return LVal.Err("'fixed?' requires 1 parameter");
        if (!a[0].IsNum) return LVal.Err("Parameter passed to 'fixed?' is not a Number");
        return LVal.Bool(a[0].NumVal is  Fix);
    }
    private static LVal IsRat(LEnv _, LVal a) {
        if (a.Count != 1) return LVal.Err("'rational?' requires 1 parameter");
        if (!a[0].IsNum) return LVal.Err("Parameter passed to 'rational?' is not a Number");
        return LVal.Bool(a[0].NumVal is  Rat);
    }

    private static LVal IsComplex(LEnv _, LVal a) {
        if (a.Count != 1) return LVal.Err("'complex?' requires 1 parameter");
        if (!a[0].IsNum) return LVal.Err("Parameter passed to 'complex?' is not a Number");
        if (a[0].NumVal is Comp) return LVal.Bool(true);
        return LVal.Bool(false);
    }
    private static LVal IsZero(LEnv e, LVal a) {
        if (a.Count != 1) return LVal.Err("'zero?' requires 1 parameter");
        if (!a[0].IsNum) return LVal.Err("Parameter passed to 'zero?' is not a Number");
        return LVal.Bool(a[0].NumVal == IntZero);
    }

    private static LVal Complex(LEnv e, LVal a) {
        if (a.Count != 2) return LVal.Err("Too few parameters passed to 'complex'");
        if (a.Cells!.Any(c => !c.IsNum)) return LVal.Err("One or more parameters passed to 'complex' is not a Number");

        if (a.Pop(0).NumVal is Int r) {
            if (a.Pop(0).NumVal is Int i) return LVal.Number(new Comp(r, i));
            return LVal.Err("Imaginary part of complex must be an int, a rational, or a fixed");
        }
        return LVal.Err("Real part of complex must be an int, a rational, or a fixed");
    }

    private static LVal ToStr(LEnv e, LVal a) {
        if (a.Count < 1) return LVal.Err("Too few parameters passed to 'to-base'");
        if (a.Count == 2) {
            if (!a[0].IsNum) return LVal.Err("First 'to-str' parameter must be a Number");
            if (!a[1].IsStr) return LVal.Err("Second 'to-str' parameter must be a String");
            return LVal.Str(NumberParser.ToBase(a[0].NumVal!, a[1].StrVal!));
        }

        return LVal.Str(a[0].ToStr());
    }

    private static LVal FastFib(LEnv env, LVal val) {
        if (val.Count == 0 || !val[0].IsNum) return LVal.Err("'fib' expects one parameter of type Number");
        var v = val[0].NumVal!.ToInt().num;
        if (v < 0) return LVal.Err("'fib' parameter 'n' cannot be negative");
        uint n = (uint)v;
        BigInteger a = 0;
        BigInteger b = 1;
        for (int i = 31; i >= 0; --i) {
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
        AddBuiltin(e, "len",  (e, a) => LVal.Number(a[0].Count));
        
        // Mathematical Functions
        AddBuiltin(e, "+", Add);
        AddBuiltin(e, "-", Sub);
        AddBuiltin(e, "*", Mul);
        AddBuiltin(e, "/", Div);

        // Logical Funcions
        AddBuiltin(e, "and", And);
        AddBuiltin(e, "or", Or);

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
        AddBuiltin(e, "val",         (e, p) => LVal.Number(NumberParser.ParseString(p?[0].StrVal ?? "0")!));
        AddBuiltin(e, "to-fixed",    (e, p) => LVal.Number(Rat.ToRat((p[0].NumVal ?? new Int())).ToFix(p.Count > 1 ? (int)(((p[1].NumVal as Int)?.num ?? BigInteger.Zero)) : 10)));
        AddBuiltin(e, "to-rational", (e, p) => LVal.Number(Rat.ToRat(p[0].NumVal!)));
        AddBuiltin(e, "truncate",    (e, p) => LVal.Number((p[0].NumVal?.ToInt() ?? new Int())));
        AddBuiltin(e, "complex",     Complex);
        AddBuiltin(e, "to-str",      ToStr);

        // fun functions
        AddBuiltin(e, "fib",         FastFib);

        // helper
        AddBuiltin(e, "defined?",    (e, p) => LVal.Bool(p.Count > 0 && p[0].IsQExpr && p[0].Cells!.All(pp => pp.IsSym && e.ContainsKey(pp.SymVal))));

        // type checking
        AddBuiltin(e, "t?",        IsT);
        AddBuiltin(e, "nil?",      IsNIL);
        AddBuiltin(e, "num?",      IsNumber);
        AddBuiltin(e, "fixed?",    IsFix);
        AddBuiltin(e, "rational?", IsRat);
        AddBuiltin(e, "int?",      IsInt);
        AddBuiltin(e, "complex?",  IsComplex);
        AddBuiltin(e, "symbol?",   IsSymbol);
        AddBuiltin(e, "string?",   IsString);
        AddBuiltin(e, "func?",     IsFunc);
        AddBuiltin(e, "error?",    IsError);
        AddBuiltin(e, "expr?",     IsExpr);
        AddBuiltin(e, "qexpr?",    IsQExpr);
        AddBuiltin(e, "sexpr?",    IsSExpr);
    }
}