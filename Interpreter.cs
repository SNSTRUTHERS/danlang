using System.Text;

public class Env {
    const string GlobalDefines = @"
        :(pi         3.14159_26535_89793_23846_26433_83279_50288_41971_69399_37510_58209_74944_59230_78164_06286_20899_86280_34825_34211_70679_82148_08651_32823_06647_09384)
        :(fun        @('() '(:(^(&1) @($(&1) &2)))))
        :(local-fun  @('() '(=(^(&1) @($(&1) &2)))))
        :(>=         @('() '(< &2 &1)))
        :(<=         @('() '(> &2 &1)))
        :(not        @('() '(if &1 NIL T)))
        :(neq        @('() '(not (eq &1 &2))))
        :(unpack     @('() '(eval (cons &1 &2))))
        :(apply      unpack)
        :(pack       @('() '(&1 $(&_))))
        :(curry      unpack)
        :(uncurry    pack)
   ";

    public static void AddGlobalsDefinesToEnv(Env env) {
        var tokens = Parser.Tokenize(new StringReader(Env.GlobalDefines ?? "exit"));
        var expr = SExpr.Create(tokens.ToList(), true);
        expr.Evaluate(env);
    }
    private Dictionary<string, IVal> _env = new Dictionary<string, IVal>(StringComparer.OrdinalIgnoreCase);
    public Env(Env? parent = null) : base() => Parent = parent;
    public Env? Parent { get; set; }
    private bool ParentContains(string s) => Parent != null && Parent.Contains(s);
    public bool Contains(string s) => ParentContains(s) || _env.ContainsKey(s);
    public IVal? GetValue(string s) => _env.GetValueOrDefault(s) ?? Parent?.GetValue(s);

    public override string ToString() {
        var sb = new StringBuilder();
        if (Parent != null) sb.AppendLine(Parent.ToString());
        sb.AppendLine(string.Join(",", _env.Select(kvp => $"{kvp.Key}={kvp.Value.Value}")));
        return sb.ToString();
    }
    public IVal? SetValue(string s, IVal v) {
        if (ParentContains(s)) {
            return Parent!.SetValue(s, v);
        }

        var oldVal = GetValue(s);
        _env[s] = v;
        return oldVal;
    }
    public bool RemoveValue(string s) {
        bool ret = Contains(s);
        if (ret) {
            if (_env.ContainsKey(s)) _env.Remove(s);
            else Parent!.RemoveValue(s);
        }
        return ret;
    }

    private static IVal Agg(List<IVal>? p, Func<Num, Num, Num> f) {
        if (p == null) return new BoolVal(false);
        if (p.Any(v => v.Type != ValType.NUM))
            return new StrVal(p.Aggregate(new StringBuilder(), (s1, s2) => s1.Append(s2.Value ?? "")).ToString());
        
        var start = (p.FirstOrDefault() as NumVal)?.UnderlyingValue ?? new Int(0);
        
        if (p.Count == 1) start = f(new Int(0), start);
        
        foreach (NumVal n in p.Skip(1)) {
            start = f(start, n.UnderlyingValue);
        }

        return new NumVal(start);
    }

    private static IVal Apply(List<IVal>? p) {
        var sexp = new SExpr();
        if (p != null) {
            sexp.Cells.Add(p[0]);
            if (p.Count > 1) {
                var q = p[1] as QExpr;
                if (q != null) {
                    sexp.Cells.AddRange(q.Cells);
                }
                else return new ErrVal("Invalid second parameter passed to 'apply'");
            }
        }
        return sexp;
    }

    private static IVal Join(List<IVal>? p, Env env, bool firstIsVal) {
        if (p == null || p.Count == 0) return new BoolVal(false);
        if (p.First() is not QExpr && !firstIsVal) return new ErrVal("First argument to 'join' must be a QExpr");
        var r = p.First() as QExpr ?? new QExpr(p.Take(1).ToList());
        r = new QExpr(r.Cells.Select(c => c.Evaluate(env)).ToList());

        foreach (QExpr n in p.Skip(1)) {
            r.Cells.AddRange(n.Cells.Select(c => c.Evaluate(env)));
        }
        return r;
    }

    private static IVal Init(List<IVal>? p) {
        if (p == null || p.Count == 0) return new BoolVal(false);
        if (p.First() is QExpr q) {
            if (q.Cells.Count == 0) return new ErrVal("'init' requires a QExpr of length 1+");
            q.Cells.RemoveAt(q.Cells.Count - 1);
            return q;
        }
        return new ErrVal("First argument to 'init' must be a QExpr");
    }

    private static IVal If(List<IVal>? p, Env env) {
        if (p == null || p.Count == 0) return new BoolVal(false);
        if (p!.Count != 3) return new ErrVal($"Incorrect number of parameters ({p!.Count}) passed to 'if', expected 3");
        if (p[0] is not BoolVal || (p[0] as BoolVal)!.UnderlyingValue) {
            return p[1] switch {
                Expr s => s.Evaluate(env),
                _ => new ErrVal("Second parameter to 'if' is not a QExpr")
            };
        } else {
            return p[2] switch {
                Expr s => s.Evaluate(env),
                _ => new ErrVal("Second parameter to 'if' is not a QExpr")
            };
        }
    }

    private static IVal Len(List<IVal>? p) {
        if (p == null || p.Count == 0) return new BoolVal(false);
        if (p.First() is QExpr q) {
            return new NumVal(new Int(q.Cells.Count));
        }
        return new ErrVal("First argument to 'len' must be a QExpr");
    }

    private static IVal Define(List<IVal>? p, Env env, bool defineAtRoot = false) {
        if ((p?.Count ?? 0) < 2) return new ErrVal($"Too few parameters passsed to '{(defineAtRoot ? "def" : "let")}'");
        while (defineAtRoot && env.Parent != null) env = env.Parent;

        // TODO ensure p[0] is QExpr, all cells are SymVals, and p[0].Count = p.Count -1
        var symbols = p![0] switch {
            QExpr q => q.Cells,
            SymVal s => new List<IVal> {s},
            _ => new List<IVal>()
        };

        var values = p!.Skip(1).ToArray();
        var i = 0;

        foreach (SymVal s in symbols) {
            var oldV = env.GetValue(s.Value);
            if (oldV is FuncVal f && f.IsBuiltin) return new  ErrVal($"Cannot override builtin {f.Value}");
            if (values[i] is FuncVal fv) fv.SetValue(s.Value);
            env.SetValue(s.Value, values[i++]);
        }

        return new BoolVal(false);
    }

    private static IVal Lambda(List<IVal>? p) {
        if ((p?.Count ?? 0) < 2) return new ErrVal("Func definition passed too few parameters");
        var formals = p![0] as QExpr;
        var body = p![1] as QExpr;
        var v = new FuncVal("_", formals!, body!);
        return v;
    }

    private static BoolVal Cmp(List<IVal>? p, Func<IVal, IVal, bool> f) {
        if (p == null) return new BoolVal(false);
        if (p.Count < 2) return p?[0] as BoolVal ?? new BoolVal(true);
        return new BoolVal(f(p[0]!, p[1]!));
    }

    public static Env RootEnv = new Env {
        _env = {
            { "t", new BoolVal(true) },
            { "nil", new BoolVal(false) },
            { "exit", new FuncVal("exit", (p, env) => new ExitVal()) }, 
            { "list", new FuncVal("list", (p, env) => (p != null ? new QExpr(p) : new BoolVal(false))) },
            { "head", new FuncVal("head", (p, env) => (p != null ? new QExpr((p.FirstOrDefault() as QExpr)?.Cells.Take(1).ToList()) : new BoolVal(false))) },
            { "tail", new FuncVal("tail", (p, env) => (p != null ? new QExpr((p.FirstOrDefault() as QExpr)?.Cells.Skip(1).ToList()) : new BoolVal(false))) },
            { "init", new FuncVal("init", (p, env) => Init(p)) },
            { "join", new FuncVal("join", (p, env) => Join(p, env, false)) },
            { "cons", new FuncVal("cons", (p, env) => Join(p, env, true)) },
            { "len",  new FuncVal("join", (p, env) => Len(p)) },
            { "eval", new FuncVal("eval", (p, env) => new SExpr(p?.FirstOrDefault() as QExpr ?? new QExpr(p)).Evaluate(env) as IVal ?? new BoolVal(false)) },
            { "def", new FuncVal("def", (p, env) => Define(p, env, true)) },
            { "set", new FuncVal("set", (p, env) => Define(p, env)) },
            { "fn", new FuncVal("fn", (p, env) => Lambda(p)) },
            { "if", new FuncVal("if", (p, env) => If(p, env)) },
            { "to-fixed", new FuncVal("to-fixed", (p, env) => new NumVal(Rat.ToRat((p?[0] as NumVal)?.UnderlyingValue ?? new Int(0)).ToFix(p?.Count > 1 ? (int)(((p?[1] as NumVal)?.UnderlyingValue as Int)?.num ?? 0) : 10))) },
            { "to-rational", new FuncVal("to-rational", (p, env) => new NumVal(Rat.ToRat((p![0] as NumVal)!.UnderlyingValue))) },
            { "truncate", new FuncVal("truncate", (p, env) => new NumVal((p?[0] as NumVal)?.UnderlyingValue?.ToInt() ?? new Int(0))) },
            // { "apply", new FuncVal("apply", (p, env) => Apply(p).Evaluate(env)) }, // more efficient 'apply' than global, above
            { "<", new FuncVal("<", (p, env) => Cmp(p, (a, b) => a.CompareTo(b) < 0)) },
            { ">", new FuncVal(">", (p, env) => Cmp(p, (a, b) => a.CompareTo(b) > 0)) },
            // { "not", new FuncVal("not", (p, env) => new BoolVal(!(Cmp(p, (a, b) => false)).UnderlyingValue)) }, // more efficient 'not' than global, above
            { "eq", new FuncVal("eq", (p, env) => Cmp(p, (a, b) => a.CompareTo(b) == 0)) },
            { "val", new FuncVal("val", (p, env) => new NumVal(Num.Parse(p?[0]?.Value?.Replace("\"", "") ?? "0")!))},
            { "+", new FuncVal("+", (p, env) => Agg(p, (a, b) => a + b)) },
            { "*", new FuncVal("*", (p, env) => Agg(p, (a, b) => a * b)) },
            { "-", new FuncVal("-", (p, env) => Agg(p, (a, b) => a - b)) },
            { "/", new FuncVal("/", (p, env) => Agg(p, (a, b) => a / b)) },
            { "%", new FuncVal("%", (p, env) => Agg(p, (a, b) => new Int(a.ToInt().num % b.ToInt().num))) }  // TODO: add operator% to Num class
        }
    };
}

public enum ValType {
    SEXPR,
    QEXPR,
    EXPR,
    SYM,
    STR,
    NUM,
    BOOL,
    FUNC,
    COMMENT,
    ERR,
    EXIT
}

public interface IVal : IComparable<IVal> {
    ValType Type { get; }
    string? Value { get; }
    IVal Evaluate(Env env);

    public static IVal Get(List<Parser.Token> tokens) => SExpr.Create(tokens);
    IVal Copy();
}

public class ExitVal : IVal {
    public ValType Type => ValType.EXIT;

    public string? Value => null;

    public IVal Evaluate(Env env) => this;
    public int CompareTo(IVal? v) {
        return v is ExitVal ? 0 : Type.CompareTo(v?.Type);
    }
    public IVal Copy() => this;
}

public abstract class Expr : IVal
{
    public List<IVal> Cells { get; protected set; } = new List<IVal>();
    public abstract ValType Type { get; }
    public Expr(List<IVal>? cells) { if (cells != null) Cells.AddRange(cells.Select(c => c.Copy())); }
    public virtual string? Value => string.Join(" ", Cells.Select(c => c.Value));
    public virtual IVal Evaluate(Env env) => this;
    public int CompareTo(IVal? v) {
        if (Value == null) return v?.Value == null ? 0 : -1;
        if (v?.Value == null) return 1;
        if (v.Type != Type) return v.Type.CompareTo(Type);
        return Value!.CompareTo(v!.Value!);
    }
    public abstract IVal Copy();
}

public abstract class BaseVal<T> : Expr {
    protected T _t;
    private ValType _vt;
    public T UnderlyingValue => _t;
    public BaseVal(T t, ValType vt) : base(null) { _t = t; _vt = vt; }
    public override ValType Type { get => _vt; }
    public override string? Value { get => _t?.ToString(); }
    public override IVal Evaluate(Env env) => this;
    public void SetValue(T v) => _t = v;
}

public class BoolVal : BaseVal<bool> {
    public BoolVal(bool b) : base(b, ValType.BOOL) {}
    public override string? Value => _t ? "T" : "NIL";
    public override IVal Copy() => new BoolVal(_t);
}

public class NumVal : BaseVal<Num> {
    public NumVal(Num n) : base(n, ValType.NUM) {}
    public override IVal Copy() => new NumVal(_t);
}

public class StrVal : BaseVal<string> {
    public StrVal(string s, ValType vt = ValType.STR) : base(s, vt) {}
    public override string Value => "\"" + _t + "\"";
    public override IVal Copy() => new StrVal(_t);
}

public class SymVal : StrVal {
    public SymVal(string s, ValType vt = ValType.SYM) : base(s, vt) {}
    public override string Value => _t;
    public override IVal Evaluate(Env env) {
        if (env.Contains(Value)) return env.GetValue(Value)!;
        return this; // new ErrVal($"Unbound symbol {Value}");
    }
    public override IVal Copy() => new SymVal(_t);
}

public class QExpr : Expr {
    public override ValType Type => ValType.QEXPR;
    public QExpr(List<IVal>? cells) : base(cells) {
        if (Cells.Count == 1 && Cells[0] is SExpr s) Cells = s.Cells;
    }
    public override string? Value => $"'({base.Value})";
    public override IVal Copy() => new QExpr(Cells);
}

public class SExpr : Expr {
    public static Expr Create(List<Parser.Token> tokens, bool root = false) {
        Expr exp = new SExpr();
        var t = tokens.FirstOrDefault();
        while (t != null) {
            tokens.RemoveAt(0);
            if (t.type == Parser.Token.Type.Symbol && t.str == "list") {
                exp = new QExpr(null);
            }
            else  {
                IVal? val = t.type switch {
                    Parser.Token.Type.EOF => root ? null : new ErrVal("Missing RParen for SExpr"),
                    Parser.Token.Type.Comment => new CommentVal(),
                    Parser.Token.Type.Error => new ErrVal(t.str ?? "Unknown error"),
                    Parser.Token.Type.LParen => SExpr.Create(tokens),
                    Parser.Token.Type.RParen => root ? new ErrVal("RParen without LParen") : null,
                    Parser.Token.Type.Number => new NumVal(t.num!),
                    Parser.Token.Type.String => new StrVal(t.str!),
                    Parser.Token.Type.Symbol => new SymVal(t.str!),
                    _ => new ErrVal($"Unknown token type {t.type}")
                };

                if (val == null) break;

                if (val is Expr) {
                    exp.Cells.Add(val);
                }
                else if (val is ErrVal e) throw new Exception(e.Value);
            }
            t = tokens.FirstOrDefault();
        }

        return exp;
    }
    public override string? Value => $"({base.Value})";
    public SExpr(List<IVal>? cells = null) : base(cells) {}
    public SExpr(QExpr q) : base (q.Cells) {}
    public override ValType Type => ValType.SEXPR;
    public override IVal Evaluate(Env env) {
        if (Cells.Count == 0) return new BoolVal(false);
        Cells = Cells.Select(c => c.Evaluate(env)).ToList();
        var fun = Cells[0];
        if (fun == null) return new ErrVal($"Unbound Symbol");
        if (fun.Type != ValType.FUNC) return new ErrVal($"First element of SExpr is not a function: {fun.Value}");
        var result = (fun as FuncVal)!.Apply(Cells.Skip(1).ToList(), env);
        // Console.WriteLine($"Evaluate: {Value} => {result.Value}");
        return result;
    }
    public override IVal Copy() => new SExpr(Cells);
}

public class FuncVal : SymVal {
    private Func<List<IVal>?, Env, IVal>? _f;
    private QExpr? _formals;
    private QExpr? _body;
    private Env? _env;

    public bool IsBuiltin => _f != null;
    public FuncVal(string s, Func<List<IVal>?, Env, IVal>? f = null) : base(s, ValType.FUNC) { 
        _f = f;
    }
    public FuncVal(string s, QExpr formals, QExpr body, Env? env = null) : this(s) {
        // TODO: ensure that all formal cells are SymVals
        _formals = formals.Copy() as QExpr;
        _body = body.Copy() as QExpr;
        _env = env;
    }
    public IVal Apply(List<IVal>? p, Env env) {
        if (_f != null) return _f(p, env);

        var localEnv = _env ?? new Env();

        // add this as &0 to the local env
        var fCount = _formals?.Cells.Count ?? 0;
        var pCount = p?.Count ?? 0;
        localEnv.SetValue("&0", this);
        if (pCount > 0 || fCount > 0) {
            var i = 0;
            // add formals to 
            for (; i < fCount && i < pCount; ++i) {
                localEnv.SetValue(_formals!.Cells[i].Value!, p![i]);
            }

            // if not all formals are bound, return a partially-bound func
            if (fCount > i) {
                var formals = new QExpr(_formals!.Cells.Skip(i).ToList());
                return new FuncVal($"partial_{this.Value}", formals, _body!, localEnv);
            }

            // add on a "Rest" local variable (&_) that is a list of remaining parameters after the formals
            if (i < pCount) localEnv.SetValue("&_", new QExpr(p!.Skip(i).ToList()));

            // and finally add an &x local variable for each additional parameter as well, starting with &n where n = length of formals list
            for (;i < pCount; ++i) {
                localEnv.SetValue($"&{i+1}", p![i]);
            }
        }

        localEnv.Parent = env;
        return new SExpr(_body!).Evaluate(localEnv);
    }
    public override IVal Copy() => _f == null ? new FuncVal(_t, _formals!, _body!, _env) : new FuncVal(_t, _f);
}

public class CommentVal : IVal
{
    public ValType Type => ValType.COMMENT;
    public string? Value => string.Empty;
    public IVal Evaluate(Env env) => this;
    public int CompareTo(IVal? v) {
        if (Value == null) return v?.Value == null ? 0 : -1;
        if (v?.Value == null) return 1;
        if (v.Type != Type) return v.Type.CompareTo(Type);
        return Value!.CompareTo(v!.Value!);
    }
    public IVal Copy() => this;
}

public class ErrVal : IVal {
    private string _err;
    public ErrVal(string s) {_err = s;}

    public ValType Type => ValType.ERR;

    public string? Value => _err;

    public int CompareTo(IVal? v) {
        if (Value == null) return v?.Value == null ? 0 : -1;
        if (v?.Value == null) return 1;
        if (v.Type != Type) return v.Type.CompareTo(Type);
        return Value.CompareTo(v.Value);
    }

    public IVal Evaluate(Env env) => this;
    public IVal Copy() => this;

}