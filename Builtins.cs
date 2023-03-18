using System.Text;
using System.Numerics;
using System.Threading;
public class Builtins
{ 
    private static void AddBuiltin(LEnv e, string name, Func<LEnv, LVal, LVal> func) {
        LVal k = LVal.Sym(name);
        LVal v = LVal.Builtin(func);
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
        LVal v = LVal.Builtin(f);
        e.Put(k.SymVal!, v);
    }

    private static LVal Lambda(LEnv e, LVal a) {
        LVal formals = a.Pop(0, e);
        LVal body = a.Pop(0, e);
        return LVal.Lambda(formals, body);
    }

    public static LVal List(LEnv e, LVal a) {
        if (!a.IsSExpr) return LVal.Err("'list' can only be applied to a SExpr");
        a.ValType = LVal.LE.QEXPR;
        return a;
    }

    private static LVal Head(LEnv e, LVal a) {
        var v = a.Pop(0, e);
        var ret = LVal.Qexpr();
        ret.Add(v.Pop(0));
        return ret;
    }

    private static LVal Tail(LEnv e, LVal a) {
        LVal v = a.Pop(0, e);
        v.Pop(0);
        return v;
    }

    private static LVal Init(LEnv e, LVal a) {
        if (a == null || a.Count == 0) return LVal.NIL();
        var v = a.Pop(0, e);
        v.Pop(v.Count - 1);
        return v;
    }

    private static LVal End(LEnv e, LVal a) {
        if (a == null || a.Count == 0) return LVal.NIL();
        var v = a.Pop(0, e);
        v = v.Pop(v.Count - 1);
        var ret = LVal.Qexpr();
        ret.Add(v);
        return ret;
    }

    public static LVal Eval(LEnv e, LVal a) {
        if (a.Count != 1) return LVal.Err("Incorrect number of parameters passed to 'eval'");
        LVal x =  a.Pop(0, e);
        if (!x.IsQExpr) return LVal.Err("Incorrect parameter passed to 'eval'.  Expected QExpr.");
        x.ValType = LVal.LE.SEXPR;
        return x.Eval(e)!;
    }

    private static LVal Join(LEnv e, LVal a) {
        if (a.Count == 0) return LVal.Err("Invalid number of parameters passed to 'join'");

        LVal? x = null;
        while (a.Count > 0) {
            var y = a.Pop(0, e);
            if (!y.IsQExpr) return LVal.Err("Invalid parameter passed to 'join'.  Expected QExpr.");
            else if (x == null) x = y;
            else x = x.Join(y);
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
                // special negation case
                if (a.Count == 0 && op == "-") {
                    y.NumVal = -y.NumVal!;
                    return y;
                }

                // not negation? Just set x to y and continue;
                x = y;
                continue;
            }

            if (op == "-") x.NumVal = x.NumVal! - y.NumVal!;
            else if (op == "*") x.NumVal = x.NumVal! * y.NumVal!;
            else if (op == "/") {
                if (y.NumVal! == Num.Zero) {
                    return LVal.Err("Division by zero.");
                }
                x.NumVal = x.NumVal! / y.NumVal!;
            }
        }

        // If no arguments were passed, return 0 for "-", and 1 otherwise
        return x ?? LVal.Number(op == "-" ? BigInteger.Zero : BigInteger.One);
    }

    private static LVal Add(LEnv e, LVal a) {
        LVal Plus(LVal a, LVal b) {
            if (a.IsNum && b.IsNum) {
                a.NumVal = a.NumVal! + b.NumVal!;
                return a;
            }
            return LVal.Str((a.ToStr() + b.ToStr()).Replace("\"\"", ""));
        }
        LVal? x = null;
        while (a.Count > 0) {
            LVal y = a.Pop(0, e);
            if (y.IsErr) return y;
            if (x == null) x = y;
            else  x = Plus(x, y);
        }
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

        if (syms.Cells!.Any(v => !v.IsSym)) return LVal.Err($"'{func}' cannot define non-symbols");

        if (syms.Count != a.Count) return LVal.Err($"'{func}' passed too many arguments or symbols.  Expected {syms.Count}, got {a.Count}");
            
        while (syms.Count > 0 && a.Count > 0) {
            var symbol = syms.Pop(0).SymVal!;
            var value = a.Pop(0, e);
            if (func == "def") { e.Def(symbol, value); }
            if (func == "set") { e.Put(symbol, value); }
        }
        
        return LVal.NIL();
    }

    private static LVal Def(LEnv e, LVal a) { return Var(e, a, "def"); }
    private static LVal Put(LEnv e, LVal a) { return Var(e, a, "set"); }

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

    private static LVal If(LEnv e, LVal a) {
        if (a.Count < 2) return LVal.Err("'if' supplied too few parameters");

        // Actual logical check; all non-NIL expressions evaluate to T in the 'if' context
        var t = a.Pop(0, e);
        if (!t.IsNIL) {
            return a.Pop(0, e);
        }

        // if no else, so return NIL
        if (a.Count == 1) return LVal.NIL();
        return a.Pop(1, e);
    }

    private static LVal And(LEnv e, LVal a) {
        if (a.Count < 2) return LVal.Err("'and' supplied too few parameters");

        while (a.Count > 0) {
            var la = a.Pop(0, e);
            if (la.IsNIL) return la;
        }
        return LVal.Bool(true);
    }

    private static LVal Or(LEnv e, LVal a) {
        if (a.Count < 2) return LVal.Err("'or' supplied too few parameters");
        while (a.Count > 0) {
            var la = a.Pop(0, e);
            if (!la.IsNIL) return LVal.Bool(true);
        }
        return LVal.Bool(false);
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
        if (a.Count > 4) return LVal.Err("Too many parameters passed to '<=>' operator");

        for (var i = 1; i < a.Count; ++i) if (!a[i].IsQExpr) return LVal.Err("Operator '<=>' received one or more invalid case blocks (not QExpr)");

        var cmpVal = a.Pop(0, e);
        if (!cmpVal.IsNum) return LVal.Err("First parameter to '<=>' must evaluate to a Number");

        // TODO: check the structure of the "cases" to ensure that there is no duplication, that the first one always has a compare value, and only the last one has no comparator

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

    public static LVal Load(LEnv e, LVal a) {
        if (a.Count == 0) return LVal.Err("'load' supplied too few parameters");
        if (a.Cells!.Any(c => !c.IsStr)) return LVal.Err("'load' passed non-string parameter(s)"); 
        
        /* Open file and check it exists */
        LVal? lastExpr = null;
        while (a.Count > 0) {
            var v = a.Pop(0, e);
            var filename = v.StrVal!;
            if (!File.Exists(filename)) {
                if (!filename.EndsWith(".dl")) filename += ".dl";
                if (!File.Exists(filename)) {
                    if (!filename.Contains(Path.PathSeparator)) filename = Path.Join("lib", filename);
                    if (!File.Exists(filename)) return LVal.Err($"File {filename} does not exist");
                }
            }

            using var f = File.Open(filename, FileMode.Open);
            if (f == null) return LVal.Err($"Could not load file {filename}");
        
            /* Read File Contents */
            Console.Write($"Loading '{filename}'...");
            var input = new StreamReader(f).ReadToEnd();
            var tokens = Parser.Tokenize(new StringReader(input)).ToList();
            lastExpr = LVal.ReadExprFromTokens(tokens);

            if (lastExpr != null && !lastExpr.IsErr) {
                LVal? expr = null;
                while (lastExpr.Count > 0) {
                    expr = lastExpr.Pop(0, e);
                    if (expr.IsErr) { expr.Println(); }
                }
                lastExpr = expr;
            }
            else {
                lastExpr?.Println();
            }

            Console.WriteLine("Complete!");
        }
        return lastExpr ?? LVal.NIL();
    }

    public static LVal Save(LEnv e, LVal a) {
        if (a.Count == 0) return LVal.Err("'save' supplied too few parameters");
        var ob = a.Pop(0, e);

        if (a.Count > 0) {
            var filename = a.Pop(0, e);
            if (!filename.IsStr) return LVal.Err("Second parameter to 'save' must be a string");
            var s = new HashSet<string>();
            while (a.Count > 0) {
                var t = a.Pop(0, e);
                if (!t.IsAtom) return LVal.Err("Parameters 3+ for function 'save' must be atoms");
                s.Add(t.SymVal);
            }

            var fi = new FileInfo(filename.StrVal);
            if (fi.Directory == null) return LVal.Err("Folder for 'save' does not exist");
            if (!fi.Directory.Exists) fi.Directory.Create();
            FileStream? fs = null;
            if (fi.Exists) {
                if (s.Contains("overwrite")) fs = fi.Open(FileMode.Truncate);
                else if (s.Contains("append")) fs = fi.Open(FileMode.Append);
                else return LVal.Err("File exitst but neither ':append' or ':overwrite' were specified");
            } else fs = fi.OpenWrite();

            fs.Write(UTF8Encoding.UTF8.GetBytes(ob.Serialize()));
            fs.Write(UTF8Encoding.UTF8.GetBytes(Environment.NewLine));
            fs.Flush();
            fs.Close();
        }
        else {
            Console.Out.WriteLine(ob.Serialize());
        }

        return LVal.NIL();
    }

    private static LVal Print(LEnv e, LVal a) {
        var pre = "";
        while (a.Count > 0) {
            Console.Write(pre);
            pre = " ";
            a.Pop(0, e).Print();
        }
        Console.Write('\n');
        return LVal.NIL();
    }

    private static LVal Error(LEnv e, LVal a) {
        return LVal.Err(a.Pop(0).StrVal!);
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

    private static LVal IsInt(LEnv e, LVal a) {
        if (a.Count != 1) return LVal.Err("'int?' requires 1 parameter");
        var n = a.Pop(0, e);
        if (!n.IsNum) return LVal.Err("Parameter passed to 'int?' is not a Number");
        if (n.NumVal is Int i && !(i is Fix) && !(i is Rat)) return LVal.Bool(true);
        return LVal.Bool(false);
    }

    private static LVal IsFix(LEnv e, LVal a) {
        if (a.Count != 1) return LVal.Err("'fixed?' requires 1 parameter");
        var v = a.Pop(0, e);
        if (!v.IsNum) return LVal.Err("Parameter passed to 'fixed?' is not a Number");
        return LVal.Bool(v.NumVal is Fix);
    }

    private static LVal IsRat(LEnv e, LVal a) {
        if (a.Count != 1) return LVal.Err("'rational?' requires 1 parameter");
        var v = a.Pop(0, e);
        if (!v.IsNum) return LVal.Err("Parameter passed to 'rational?' is not a Number");
        return LVal.Bool(v.NumVal is  Rat);
    }

    private static LVal IsComplex(LEnv e, LVal a) {
        if (a.Count != 1) return LVal.Err("'complex?' requires 1 parameter");
        var v = a.Pop(0, e);
        if (!v.IsNum) return LVal.Err("Parameter passed to 'complex?' is not a Number");
        return LVal.Bool(v.NumVal is Comp);
    }

    private static LVal IsZero(LEnv e, LVal a) {
        if (a.Count != 1) return LVal.Err("'zero?' requires 1 parameter");
        var v = a.Pop(0, e);
        if (!v.IsNum) return LVal.Err("Parameter passed to 'zero?' is not a Number");
        return LVal.Bool(v.NumVal == Num.Zero);
    }

    private static LVal Complex(LEnv e, LVal a) {
        if (a.Count != 2) return LVal.Err("Too few parameters passed to 'complex'");
        if (a.Cells!.Any(c => !c.IsNum)) return LVal.Err("One or more parameters passed to 'complex' is not a Number");

        if (a.Pop(0, e).NumVal is Int r) {
            if (a.Pop(0, e).NumVal is Int i) return LVal.Number(new Comp(r, i));
            return LVal.Err("Imaginary part of complex must be an int, a rational, or a fixed");
        }
        return LVal.Err("Real part of complex must be an int, a rational, or a fixed");
    }

    private static LVal ToStr(LEnv e, LVal a) {
        if (a.Count < 1) return LVal.Err("Too few parameters passed to 'to-str'");

        var n = a.Pop(0, e);
        if (!n.IsNum) return LVal.Err("First 'to-str' parameter must be a Number");
        if (a.Count == 1) {
            var str = a.Pop(0, e);
            if (!str.IsStr) return LVal.Err("Second 'to-str' parameter must be a String");
            return LVal.Str(NumberParser.ToBase(n.NumVal!, str.StrVal!));
        }

        return LVal.Str(n.ToStr());
    }

    private static LVal Substring(LEnv e, LVal a) {
        if (a.Count < 2) return LVal.Err("Too few parameters passed to 'substring'");
        var str = a.Pop(0, e);
        if (!str.IsStr) return LVal.Err("First 'substring' parameter must be a String");
        var n = a.Pop(0, e);
        if (!n.IsNum) return LVal.Err("Second 'substring' parameter must be a Number");
        if (a.Count == 0) {
            var index = (int)n.NumVal!.ToInt()!.num;
            return LVal.Str(str.StrVal.Substring(index));
        }

        if (a.Count == 1) {
            var l = a.Pop(0, e);
            if (!l.IsNum) return LVal.Err("Third 'substring' parameter must be a Number");
            var index = (int)n.NumVal!.ToInt()!.num;
            var len = (int)l.NumVal!.ToInt()!.num;
            // if too many characters are requested, just return whatever is left of the string after index
            var s = str.StrVal;
            if (len > s.Length - index) return LVal.Str(s.Substring(index));
            return LVal.Str(s.Substring(index, len));
        }
        return LVal.Err("Too many parameters passed to 'substring'");
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
        // TODO: convert this to return a character instead of a string once the character type is created
        a.Add(LVal.Number(1));
        return Substring(e, a);
    }

    private static LVal Subset(LEnv e, LVal a) {
        if (a.Count < 2) return LVal.Err("Too few parameters passed to 'subset'");

        var q = a.Pop(0, e);
        if (!q.IsQExpr) return LVal.Err("First 'subset' parameter must be a QExpr");

        var i = a.Pop(0, e);
        if (!i.IsNum) return LVal.Err("Second 'subset' parameter must be a Number");
        var index = (int)i.NumVal!.ToInt()!.num;
        if (q.Count <= index) return LVal.Bool(false);
        if (a.Count == 0) {
            var ret = LVal.Qexpr();
            foreach (var c in q.Cells!.Skip(index)) ret.Add(c.Copy());
            return ret;
        }

        if (a.Count == 1) {
            var l = a.Pop(0, e);
            if (!l.IsNum) return LVal.Err("Third 'subset' parameter must be a Number");
            var count = (int)l.NumVal!.ToInt()!.num;
            var ret = LVal.Qexpr();
            foreach (var c in q.Cells!.Skip(index).Take(count)) ret.Add(c.Copy());
            return ret;
        }
        return LVal.Err("Too many parameters passed to 'subset'");
    }

    private static LVal ItemAt(LEnv e, LVal a) {
        if (a.Count < 2) return LVal.Err("Too few parameters passed to 'item-at'");

        var q = a.Pop(0, e);
        if (!q.IsQExpr) return LVal.Err("First 'item-at' parameter must be a QExpr");

        var i = a.Pop(0, e);
        if (i.IsNum) return LVal.Err("Second 'item-at' parameter must be a Number");
        var index = (int)i.NumVal!.ToInt()!.num;
        if (q.Count <= index) return LVal.Bool(false);
        return q[index].Copy();
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
        if (!hash.IsHash) return LVal.Err("First parameter to 'hash-put' must be a hash");

        if (val.Count == 1) {
            var p = val.Pop(0, e);
            if (p.Count == 2) return hash.HashValue!.Put(p[0], p[1].Eval(e));
            return hash.HashValue!.Put(p);
        }

        var ret = LVal.Qexpr();
        while (val.Count > 0) {
            var v = val.Pop(0, e);
            if (v.Count == 2) {
                ret.Add(hash.HashValue!.Put(v[0], v[1].Eval(e)));
            }
            else ret.Add(hash.HashValue!.Put(v));
        }

        return ret;
    }

    private static LVal ToHash(LEnv e, LVal val) {
        if (val.Count < 1) return LVal.Err("'to#' requires one parameters");
        var hash = val.Pop(0, e);
        if (!hash.IsQExpr) return LVal.Err("First parameter to 'to#' must be a QExpr");
        return LVal.Hash(val, e);
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

        e.Put("&0", LVal.Hash(hash.HashValue.PrivateCallProxy));
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

    private static LVal HashIsLocked(LEnv e, LVal val) {
        val.Add(LVal.Atom(LHash.TAG_LOCKED));
        return HashHasTag(e, val);
    }

    private static LVal HashIsPrivate(LEnv e, LVal val) {
        val.Add(LVal.Atom(LHash.TAG_PRIV));
        return HashHasTag(e, val);
    }

    private static LVal HashIsConst(LEnv e, LVal val) {
        val.Add(LVal.Atom(LHash.TAG_RO));
        return HashHasTag(e, val);
    }

    static private Random _rand = new Random();

    // Add Builtins to an Environment
    public static void AddBuiltins(LEnv e) {
        // variable definition functions
        AddBuiltin(e, "fn",  Lambda); 
        AddBuiltin(e, "def", Def);
        AddBuiltin(e, "set", Put);
        
        // list functions
        AddBuiltinEvaluated(e, "list", List);
        AddBuiltin(e, "head", Head);
        AddBuiltin(e, "tail", Tail);
        AddBuiltin(e, "init", Init);
        AddBuiltin(e, "end",  End);
        AddBuiltin(e, "join", Join);
        AddBuiltin(e, "eval", Eval);
        AddBuiltinEvaluated(e, "len",  (e, a) => a[0].ValType switch { 
            LVal.LE.QEXPR => LVal.Number(a[0].Count),
            LVal.LE.STR   => LVal.Number(a[0].StrVal.Length),
            _             => LVal.Err("'len' requires parameter of type string or list")
        });
        AddBuiltin(e, "item-at", ItemAt);
        AddBuiltin(e, "subset", Subset);

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

        AddBuiltin(e, "random",     (e, a) => LVal.Number(_rand.NextInt64((long)(a.Pop(0, e).NumVal!.ToInt().num))));
 
        // logical funcions
        AddBuiltin(e, "and", And);
        AddBuiltin(e, "or",  Or);

        // comparison functions
        AddBuiltin(e, "if",  If);
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
        AddBuiltin(e, "index-of",      (e, a) => LVal.Number(a.Pop(0, e).StrVal!.IndexOf(a.Pop(0, e).StrVal!)));
        AddBuiltin(e, "last-index-of", (e, a) => LVal.Number(a.Pop(0, e).StrVal!.LastIndexOf(a.Pop(0, e).StrVal!)));
        AddBuiltin(e, "substring", Substring);
        AddBuiltin(e, "char-at",   CharAt);
        AddBuiltin(e, "str-split", Split);

        // conversion functions
        // TODO: move the bodies of these definitions to static methods with error checking
        AddBuiltin(e, "val",         (e, a) => LVal.Number(NumberParser.ParseString(a.Pop(0, e).StrVal)!));
        AddBuiltinEvaluated(e, "to-fixed",    (e, a) => LVal.Number(Rat.ToRat((a[0].NumVal ?? new Int())).ToFix(a.Count > 1 ? (int)(((a[1].NumVal as Int)?.num ?? BigInteger.Zero)) : 10)));
        AddBuiltin(e, "to-rational", (e, a) => LVal.Number(Rat.ToRat(a.Pop(0, e).NumVal!)));
        AddBuiltin(e, "truncate",    (e, a) => LVal.Number((a.Pop(0, e).NumVal?.ToInt() ?? new Int())));
        AddBuiltin(e, "complex",     Complex);
        AddBuiltinEvaluated(e, "to-str",      ToStr);
        AddBuiltin(e, "to-sym",      (e, a) => a[0].IsAtom ? LVal.Sym(a[0].SymVal) : LVal.Err("Only atoms can be converted into symbols"));
        AddBuiltinEvaluated(e, "to-atom",
            (e, a) => a[0].IsSym ? LVal.Atom(a[0].SymVal) : 
                (a[0].IsNum ? LVal.Atom(a[0].NumVal!.ToInt().num.ToString()) : 
                    LVal.Err("Only symbols and numbers can be converted to atoms")));

        // fun functions
        AddBuiltin(e, "fib",       FastFib);

        // helper
        AddBuiltin(e, "defined?",  (e, a) => LVal.Bool((a[0].IsT || (a[0].IsSym && e.ContainsKey(a[0].SymVal))) || (a.Count > 0 && a[0].IsQExpr && a[0].Cells!.All(pp => pp.IsT || (pp.IsSym && e.ContainsKey(pp.SymVal))))));

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
        AddBuiltin(e, "hash-add-tag", HashAddTag);
        AddBuiltin(e, "hash-lock",         (e, a) => _HashApplyTag(e, a, LHash.TAG_LOCKED));
        AddBuiltin(e, "hash-make-const",   (e, a) => _HashApplyTag(e, a, LHash.TAG_RO));
        AddBuiltin(e, "hash-make-private", (e, a) => _HashApplyTag(e, a, LHash.TAG_PRIV));
        AddBuiltin(e, "hash-make-not-nil", (e, a) => _HashApplyTag(e, a, LHash.TAG_NOT_NIL));
        AddBuiltin(e, "hash-tag?",     HashHasTag);
        AddBuiltin(e, "hash-locked?",  HashIsLocked);
        AddBuiltin(e, "hash-private?", HashIsPrivate);
        AddBuiltin(e, "hash-const?",   HashIsConst);
    }
}