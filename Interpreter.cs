using System.Numerics;
using System.Text;

public class Env {
    private Dictionary<string, IVal> _env = new Dictionary<string, IVal>(StringComparer.OrdinalIgnoreCase);
    public Env(Env? parent = null) : base() => Parent = parent;
    public Env? Parent { get; }
    private bool ParentContains(string s) => Parent != null && Parent.Contains(s);
    public bool Contains(string s) {
        return ParentContains(s) || _env.ContainsKey(s);
    }
    public IVal? GetValue(string s) {
        return Parent?.GetValue(s) ?? _env.GetValueOrDefault(s);
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

    private static IVal Join(List<IVal>? p) {
        if (p == null || p.Count == 0) return new BoolVal(false);
        var r = p.First() as QExpr ?? new QExpr(p.Take(1).ToList());
        foreach (QExpr n in p.Skip(1)) {
            r.Cells.AddRange(n.Cells);
        }
        return r;
    }

    public static Env RootEnv = new Env {
        _env = {
            { "pi", new NumVal(Num.Parse("3.14159_26535_89793_23846_26433_83279_50288_41971_69399_37510_58209_74944_59230_78164_06286_20899_86280_34825_34211_70679_82148_08651_32823_06647_09384")!) },
            { "list",  new FuncVal("list",  (p, env) => (p != null ? new QExpr(p) : new BoolVal(false) )) },
            { "head",  new FuncVal("head",  (p, env) => (p?.FirstOrDefault() as QExpr)?.Cells.FirstOrDefault() ?? new BoolVal(false)) },
            { "tail",  new FuncVal("tail",  (p, env) => (p != null ? new QExpr((p.First() as QExpr)!.Cells.Skip(1).ToList()) : new BoolVal(false) )) },
            { "join",  new FuncVal("join",  (p, env) => Join(p)) },
            { "eval",  new FuncVal("eval", (p, env) => new SExpr(p?.LastOrDefault() as QExpr ?? new QExpr(p)).Evaluate(env) as IVal ?? new BoolVal(false)) },
            { "def", new FuncVal("", (p, env) => new BoolVal(false)) },
            { "fn", new FuncVal("", (p, env) => new BoolVal(false)) },
            { "to-fixed", new FuncVal("to-fixed", (p, env) => new NumVal(Rat.ToRat((p?[0] as NumVal)?.UnderlyingValue ?? new Int(0)).ToFix(p?.Count > 1 ? (int)(((p?[1] as NumVal)?.UnderlyingValue as Int)?.num ?? 0) : 10))) },
            { "to-rational", new FuncVal("to-rational", (p, env) => new NumVal(Rat.ToRat((p![0] as NumVal)!.UnderlyingValue))) },
            { "truncate", new FuncVal("truncate", (p, env) => new NumVal((p?[0] as NumVal)?.UnderlyingValue?.ToInt() ?? new Int(0))) },
            { "apply", new FuncVal("apply", (p, env) => Apply(p).Evaluate(env)) },
            { "+", new FuncVal("+", (p, env) => Agg(p, (a, b) => a + b)) },
            { "*", new FuncVal("*", (p, env) => Agg(p, (a, b) => a * b)) },
            { "-", new FuncVal("-", (p, env) => Agg(p, (a, b) => a - b)) },
            { "/", new FuncVal("/", (p, env) => Agg(p, (a, b) => a / b)) },
            { "%", new FuncVal("%", (p, env) => Agg(p, (a, b) => new Int(a.ToInt().num % b.ToInt().num))) },
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
    ERR
}

public interface IVal {
    ValType Type { get; }
    string? Value { get; }
    IVal Evaluate(Env env);

    public static IVal Get(List<Parser.Token> tokens) => SExpr.Create(tokens);
}

public abstract class BaseVal<T> : IVal {
    protected T _t;
    private ValType _vt;
    public T UnderlyingValue => _t;
    public BaseVal(T t, ValType vt) { _t = t; _vt = vt; }
    public ValType Type { get => _vt; }
    public virtual string? Value { get => _t?.ToString(); }
    public virtual IVal Evaluate(Env env) => this;
}

public class BoolVal : BaseVal<bool> {
    public BoolVal(bool b) : base(b, ValType.BOOL) {}
    public override string? Value => _t ? "T" : "NIL";
}

public class NumVal : BaseVal<Num> {
    public NumVal(Num n) : base(n, ValType.NUM) {}
}

public class StrVal : BaseVal<string> {
    public StrVal(string s, ValType vt = ValType.STR) : base(s, vt) {}
    public override string Value => "\"" + _t + "\"";
}

public class SymVal : StrVal {
    public SymVal(string s, ValType vt = ValType.SYM) : base(s, vt) {}
    public override string Value => _t;
    public override IVal Evaluate(Env env) {
        if (env.Contains(Value)) return env.GetValue(Value)!;
        return new ErrVal($"ERROR: Evaluation of unbound symbol {Value}");
    }
}

public abstract class Expr : IVal
{
    public List<IVal> Cells { get; protected set; } = new List<IVal>();
    public abstract ValType Type { get; }
    public Expr(List<IVal>? cells) => Cells = cells ?? Cells;
    public virtual string? Value => string.Join(" ", Cells.Select(c => c.Value));
    public virtual IVal Evaluate(Env env) => this;
}

public class QExpr : Expr {
    public override ValType Type => ValType.QEXPR;
    public QExpr(List<IVal>? cells) : base(cells) {
        if (Cells.Count == 1 && Cells[0] is SExpr s) Cells = s.Cells;
    }
    public override string? Value => $"'({base.Value})";
}

public class SExpr : Expr {
    public static SExpr Create(List<Parser.Token> tokens, bool root = false) {
        var exp = new SExpr();
        var t = tokens.FirstOrDefault();
        while (t != null) {
            tokens.RemoveAt(0);
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

            if (val != null && val.Type != ValType.ERR && val.Type != ValType.COMMENT) {
                exp.Cells.Add(val);
            }
            else if (val?.Type == ValType.ERR) throw new Exception(val.Value);

            if (val == null) break;

            t = tokens.FirstOrDefault();
        }

        return exp;
    }
    public override string? Value => $"({base.Value})";
    public SExpr(List<IVal>? cells = null) : base(cells) {}
    public SExpr(QExpr q) : base (q.Cells) {}
    public override ValType Type => ValType.SEXPR;
    public override IVal Evaluate(Env env) {
        Console.WriteLine($"Evaluating SExpr...{Value}");
        if (Cells.Count == 0) return new BoolVal(false);
        Cells = Cells.Select(c => c.Evaluate(env)).ToList();
        var fun = Cells[0];
        if (fun == null) return new ErrVal($"Unbound Symbol {Cells[0].Value}");
        if (fun.Type != ValType.FUNC) return new ErrVal($"First element of SExpr is not a function: {fun.Value}");
        var result = (fun as FuncVal)!.Apply(Cells.Skip(1).ToList(), env);
        return result;
    }
}

public class FuncVal : SymVal {
    private Func<List<IVal>?, Env, IVal> _f;
    public FuncVal(string s, Func<List<IVal>?, Env, IVal>  f) : base(s, ValType.FUNC) => _f = f;
    public IVal Apply(List<IVal>? parameters, Env env) {
        // Console.WriteLine($"Calling func {Value} with params {parameters}");
        var result = _f(parameters, env);
        // Console.WriteLine($"Result is {result}");
        return result;
    }
}

public class CommentVal : IVal
{
    public ValType Type => ValType.COMMENT;
    public string? Value => string.Empty;
    public IVal Evaluate(Env env) => this;
}

public class ErrVal : StrVal {
    public ErrVal(string s) : base(s, ValType.ERR) {}
}