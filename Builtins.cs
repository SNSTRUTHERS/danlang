public class Builtins
{ 
    private static void add_builtin(LEnv e, string name, Func<LEnv, LVal, LVal> func) {
        LVal k = LVal.Sym(name);
        LVal v = LVal.Builtin(func);
        e.Put(k.SymVal!, v);
    }

    private static LVal Lambda(LEnv e, LVal a) {
        // LASSERT_NUM("\\", a, 2);
        // LASSERT_TYPE("\\", a, 0, LE.LVAL_QEXPR);
        // LASSERT_TYPE("\\", a, 1, LE.LVAL_QEXPR);
        
        for (int i = 0; i < a.Cells![0].Count; i++) {
            // LASSERT(a, (a.Cells[0].Cells[i].ValType == LE.LVAL_SYM),      "Cannot define non-symbol. Got %s, Expected %s.",      ltype_name(a.Cells[0].Cells[i].ValType), ltype_name(LE.LVAL_SYM));
        }
        
        LVal formals =  a.Pop(0);
        LVal body =  a.Pop(0);
        
        return LVal.Lambda(formals, body);
    }

    public static LVal List(LEnv e, LVal a) {
        a.ValType = LVal.LE.LVAL_QEXPR;
        return a;
    }

    private static LVal Head(LEnv e, LVal a) {
        // LASSERT_NUM("head", a, 1);
        // LASSERT_TYPE("head", a, 0, LE.LVAL_QEXPR);
        // LASSERT_NOT_EMPTY("head", a, 0);
        
        LVal v = a.Take(0);  
        while (v.Count > 1) {  v.Pop(1); }
        return v;
    }

    private static LVal Tail(LEnv e, LVal a) {
        // LASSERT_NUM("tail", a, 1);
        // LASSERT_TYPE("tail", a, 0, LE.LVAL_QEXPR);
        // LASSERT_NOT_EMPTY("tail", a, 0);

        LVal v = a.Take(0);  
        v.Pop(0);
        return v;
    }

    private static LVal Init(LEnv e, LVal a) {
        if (a == null || a.Count == 0) return LVal.Sexpr();
        LVal v = a.Take(0);
        v.Pop(v.Count - 1);
        return v;
    }

    private static LVal End(LEnv e, LVal a) {
        // LASSERT_NUM("end", a, 1);
        // LASSERT_TYPE("end", a, 0, LE.LVAL_QEXPR);
        // LASSERT_NOT_EMPTY("end", a, 0);
        
        LVal v = a.Take(0);  
        while (v.Count > 1) {  v.Pop(0); }
        return v;
    }

    public static LVal Eval(LEnv e, LVal a) {
        if (a.Count != 1 || a.Cells![0].ValType != LVal.LE.LVAL_QEXPR) return LVal.Err("Invalid parameter(s) passed to 'eval'");
        LVal x =  a.Take(0);
        x.ValType = LVal.LE.LVAL_SEXPR;
        return x.Eval(e)!;
    }

    private static LVal Join(LEnv e, LVal a) {
        if (a.Cells!.Any(v => v.ValType != LVal.LE.LVAL_QEXPR)) return LVal.Err("Non-QExpr in parameters list for 'join'");
        LVal x =  a.Pop(0);
        
        while (a.Count > 0) {
            LVal y =  a.Pop(0);
            x =  x.Join(y);
        }
        return x;
    }

    private static LVal Op(LEnv e, LVal a, string op) {
        if (a.Cells!.Any(v => v.ValType != LVal.LE.LVAL_NUM)) return LVal.Err($"Non-NumVal in parameters list for '{op}'");
        LVal x =  a.Pop(0);
        
        if ((op == "-") && a.Count == 0) { x.NumVal = -x.NumVal!; }
        
        while (a.Count > 0) {  
            LVal y =  a.Pop(0);
            
            if (op == "+") { x.NumVal = x.NumVal! + y.NumVal!; }
            if (op == "-") { x.NumVal = x.NumVal! - y.NumVal!; }
            if (op == "*") { x.NumVal = x.NumVal! * y.NumVal!; }
            if (op == "/") {
                if ((y.NumVal as Int)!.num == 0) {
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
        //if (a.Count == 0 || a.ValType != LVal.LE.LVAL_QEXPR) return LVal.Err($"Invalid parameter(s) passed to '{func}'");
        LVal syms = a.Cells![0];
        if (syms.Cells!.Any(v => v.ValType != LVal.LE.LVAL_SYM)) return LVal.Err($"'{func}' cannot define non-symbols");
        if (syms.Count != a.Count - 1) return LVal.Err($"'{func}' passed too many arguments or symbols.  Expected {syms.Count}, got {a.Count - 1}");
            
        for (int i = 0; i < syms.Count; i++) {
            var symbol = syms.Cells![i].SymVal!;
            var value = a.Cells[i+1].Eval(e);
            if (func == "def") { e.Def(symbol, value) ; }
            if (func == "set") { e.Put(symbol, value); } 
        }
        
        return LVal.Sexpr();
    }

    private static LVal Def(LEnv e, LVal a) { return Var(e, a, "def"); }
    private static LVal Put(LEnv e, LVal a) { return Var(e, a, "set"); }

    private static LVal Ord(LEnv e, LVal a, string op) {
        // LASSERT_NUM(op, a, 2);
        // LASSERT_TYPE(op, a, 0, LE.LVAL_NUM);
        // LASSERT_TYPE(op, a, 1, LE.LVAL_NUM);
        bool r = false;
        if (op == ">") { r = ((a.Cells![0].NumVal as Int)!.num >  (a.Cells[1].NumVal as Int)!.num); }
        if (op == "<") { r = ((a.Cells![0].NumVal as Int)!.num <  (a.Cells[1].NumVal as Int)!.num); }
        return LVal.Number(new Int(r ? 1 : 0));
    }

    private static LVal Gt(LEnv e, LVal a) { return Ord(e, a, ">");  }
    private static LVal Lt(LEnv e, LVal a) { return Ord(e, a, "<");  }
    private static LVal Ge(LEnv e, LVal a) { return Ord(e, a, ">="); }
    private static LVal Le(LEnv e, LVal a) { return Ord(e, a, "<="); }

    private static LVal Cmp(LEnv e, LVal a, string op) {
        // LASSERT_NUM(op, a, 2);
        bool r = false;
        if (op == "eq")  { r =  a.Cells![0].Equals(a.Cells[1]); }
        if (op == "neq") { r = !a.Cells![0].Equals(a.Cells[1]); }
        return LVal.Number(new Int(r ? 1 : 0));
    }

    private static LVal Eq(LEnv e, LVal a) { return Cmp(e, a, "eq"); }
    private static LVal Ne(LEnv e, LVal a) { return Cmp(e, a, "neq"); }

    private static LVal If(LEnv e, LVal a) {
        // LASSERT_NUM("if", a, 3);
        // LASSERT_TYPE("if", a, 0, LE.LVAL_NUM);
        // LASSERT_TYPE("if", a, 1, LE.LVAL_QEXPR);
        // LASSERT_TYPE("if", a, 2, LE.LVAL_QEXPR);        
        LVal x;
        // Console.Write("In If: "); a.Println();
        a.Cells![1].ValType = LVal.LE.LVAL_SEXPR;
        a.Cells![2].ValType = LVal.LE.LVAL_SEXPR;
        
        if (a.Cells[0].NumVal != null && a.Cells[0].NumVal?.CompareTo(0) != 0) {
            x = a.Pop(1).Eval(e)!;
        } else {
            x = a.Pop(2).Eval(e)!;
        }

        return x;
    }

    public static LVal Load(LEnv e, LVal a) {
        // LASSERT_NUM("load", a, 1);
        // LASSERT_TYPE("load", a, 0, LE.LVAL_STR);
        
        /* Open file and check it exists */
        var filename = a.Cells![0].StrVal!;
        if (!File.Exists(filename)) return LVal.Err($"File {filename} does not exist");
        using var f = File.Open(a.Cells![0].StrVal!, FileMode.Open);
        if (f == null) {
            LVal err = LVal.Err($"Could not load Library {filename}");
            return err;
        }
        
        /* Read File Contents */
        Console.Write($"Loading '{filename}'...");
        var input = new StreamReader(f).ReadToEnd();
        var tokens = Parser.Tokenize(new StringReader(input)).ToList();
        var expr = LVal.read_expr_from_tokens(tokens);

        if (expr != null && expr.ValType != LVal.LE.LVAL_ERR) {
            while (expr.Count > 0) {
                LVal x = expr.Pop(0).Eval(e)!;
                if (x.ValType == LVal.LE.LVAL_ERR) { x.Println(); }
            }
        } else {
            expr?.Println();
        }

        Console.WriteLine("Complete!");
        return LVal.Sexpr();
    }

    private static LVal Print(LEnv e, LVal a) {  
        for (int i = 0; i < a.Count; i++) {
            a.Cells![i].Print(); Console.Write(' ');
        }
        Console.Write('\n');
        return LVal.Sexpr();
    }

    private static LVal Error(LEnv e, LVal a) {
        // LASSERT_NUM("error", a, 1);
        // LASSERT_TYPE("error", a, 0, LE.LVAL_STR);
        return LVal.Err(a.Cells![0].StrVal!);  
    }

    public static void add_builtins(LEnv e) {
        /* Variable Functions */
        add_builtin(e, "fn",  Lambda); 
        add_builtin(e, "def", Def);
        add_builtin(e, "set", Put);
        
        /* List Functions */
        add_builtin(e, "list", List);
        add_builtin(e, "head", Head);
        add_builtin(e, "tail", Tail);
        add_builtin(e, "init", Init);
        add_builtin(e, "end",  End);
        add_builtin(e, "eval", Eval);
        add_builtin(e, "join", Join);
        
        /* Mathematical Functions */
        add_builtin(e, "+", Add);
        add_builtin(e, "-", Sub);
        add_builtin(e, "*", Mul);
        add_builtin(e, "/", Div);
        
        /* Comparison Functions */
        add_builtin(e, "if", If);
        add_builtin(e, "eq", Eq);
        // add_builtin(e, "neq", ne);
        add_builtin(e, ">",  Gt);
        add_builtin(e, "<",  Lt);
        
        /* String Functions */
        add_builtin(e, "load",  Load); 
        add_builtin(e, "error", Error);
        add_builtin(e, "print", Print);

        /* Other functions */
        // TODO: move the bodies of these definitions to static methods with error checking
        add_builtin(e, "val", (e, p) => LVal.Number(Num.Parse(p.Cells?[0].StrVal ?? "0")!));
        add_builtin(e, "is-def", (e, p) => (p.Cells![0].ValType == LVal.LE.LVAL_SYM && e.ContainsKey(p.Cells[0].SymVal) ? LVal.Number(new Int(1)) : LVal.Sexpr()));
        add_builtin(e, "to-fixed", (e, p) => LVal.Number(Rat.ToRat((p.Cells![0].NumVal ?? new Int(0))).ToFix(p.Count > 1 ? (int)(((p.Cells![1].NumVal as Int)?.num ?? 0)) : 10)));
        add_builtin(e, "to-rational", (e, p) => LVal.Number(Rat.ToRat(p.Cells![0].NumVal!)));
        add_builtin(e, "truncate", (e, p) => LVal.Number((p.Cells![0].NumVal?.ToInt() ?? new Int(0))));
    }
}