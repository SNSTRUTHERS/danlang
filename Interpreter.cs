// THIS IS NOT DEPRECATED

using System.Numerics;
using System.Text;
namespace Dep {
    public class Env {
        public static QExpr NIL => new QExpr();
        public static bool IsNIL(IVal i) => (i is QExpr q) && q.Cells.Count == 0;
        public static TrueVal TRUE => new TrueVal();
        public static bool IsTRUE(IVal i) => !IsNIL(i);
        public static ErrVal ERROR(string s) { Console.WriteLine("ERROR: " + s); return new ErrVal(s); }

        public Env Copy() {
            var cpy = new Env();
            cpy.Parent = Parent;
            foreach (var kvp in _env) {
                cpy.SetValue(kvp.Key, kvp.Value.Copy());
            }
            return cpy;
        }

        public IVal Evaluate(IVal i) => i.Evaluate(this);
        public IVal EvalQExpr(QExpr q) {
            // Console.WriteLine($"Evaluating QExpr: {i.Value}");
            return new SExpr(q).Evaluate(this);
        }

        public static void AddGlobalsDefinesToEnv(Env env) {
            env.Evaluate(Load(@".\lib\globals.lisp"));
        }

        public static IVal Load(string fn) {
            TextReader? freeStream = null;
            try {
                var globalsStream = fn == "-" ? Console.In : new StreamReader(fn);
                if (fn != "-") freeStream = globalsStream;
                var tokens = Parser.Tokenize(globalsStream);
                return SExpr.Create(tokens.ToList());
            }
            finally {
                freeStream?.Dispose();
            }
        }

        private Dictionary<string, IVal> _env = new Dictionary<string, IVal>(StringComparer.OrdinalIgnoreCase);
        public Env(Env? parent = null) : base() => Parent = parent;
        public Env? Parent { get; set; }
        private bool ParentContains(string s) => Parent != null && Parent.Contains(s);
        public bool Contains(string s) => _env.ContainsKey(s) || ParentContains(s);
        public IVal? GetValue(string s) {
            var v = _env.GetValueOrDefault(s) ?? Parent?.GetValue(s);
            // if (s.StartsWith("&")) Console.WriteLine($"*** Getting {s}={v?.ToString() ?? "null"} ***");
            return v?.Copy();
        }

        public override string ToString() {
            var sb = new StringBuilder();
            if (Parent != null) sb.AppendLine(Parent.ToString());
            sb.AppendLine(string.Join(",", _env.Select(kvp => $"{kvp.Key}={kvp.Value.Value}")));
            return sb.ToString();
        }
        public void SetValue(string s, IVal v) {
            _env[s] = v.Copy();
            // if (s.StartsWith("&")) Console.WriteLine($"Env: setting {s} to {v.Value}");
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
            if (p == null) return NIL;
            if (p.Any(v => v.Type != ValType.NUM))
                return new StrVal(p.Aggregate(new StringBuilder(), (s1, s2) => s1.Append(s2.Value ?? "")).ToString());
            
            var start = (p.FirstOrDefault() as NumVal)?.UnderlyingValue ?? new Int();
            
            if (p.Count == 1) start = f(new Int(), start);
            
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
                    if (p[1] is QExpr q) {
                        sexp.Cells.AddRange(q.Cells);
                    }
                    else return ERROR("Invalid second parameter passed to 'apply'");
                }
            }
            return sexp;
        }

        private static IVal Join(List<IVal>? p, Env env, bool firstIsVal) {
            if (p == null || p.Count == 0) return NIL;
            if (p.First() is not QExpr && !firstIsVal) return ERROR("First argument to 'join' must be a QExpr");
            var r = p.First() as QExpr ?? new QExpr(p.Take(1).ToList(), false);
            r = new QExpr(r.Cells.Select(c => c.Evaluate(env)).ToList());

            foreach (QExpr n in p.Skip(1)) {
                r.Cells.AddRange(n.Cells.Select(c => c.Evaluate(env)));
            }
            return r;
        }

        private static IVal Init(List<IVal>? p) {
            if (p == null || p.Count == 0) return NIL;
            if (p.First() is QExpr q) {
                if (q.Cells.Count == 0) return ERROR("'init' requires a QExpr of length 1+");
                q.Cells.RemoveAt(q.Cells.Count - 1);
                return q;
            }
            return ERROR("First argument to 'init' must be a QExpr");
        }

        private static IVal If(List<IVal>? p, Env env) {
            if (p == null || p.Count == 0) return NIL;
            if (p!.Count != 3) return ERROR($"Incorrect number of parameters ({p!.Count}) passed to 'if', expected 3");
            return (IsNIL(p[0]) ? p[2] : p[1]) switch {
                QExpr q2 => env.EvalQExpr(q2),
                _ => ERROR("Second parameter to 'if' is not a QExpr")
            };
        }

        private static IVal Len(List<IVal>? p) {
            if (p == null || p.Count == 0) return NIL;
            if (p.First() is QExpr q) {
                return new NumVal(new Int(q.Cells.Count));
            }
            return ERROR("First argument to 'len' must be a QExpr");
        }

        private static IVal Define(List<IVal>? p, Env env, bool defineAtRoot = false) {
            if ((p?.Count ?? 0) < 2) return ERROR($"Too few parameters passsed to '{(defineAtRoot ? "def" : "set")}'");
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
                if (oldV is FuncVal f && f.IsBuiltin) return ERROR($"Cannot override builtin {f.Value}");
                if (values[i] is FuncVal fv && fv.Value == "_") fv.SetValue(s.Value);
                env.SetValue(s.Value, values[i++]);
            }

            return NIL;
        }

        private static IVal Fn(List<IVal>? p) {
            if ((p?.Count ?? 0) < 2) return ERROR("Func definition passed too few parameters");
            var formals = p![0] as QExpr;
            var body = p![1] as QExpr;
            // Console.WriteLine($"Fn: {p[0].Value} {p[1].Value}");
            var v = new FuncVal("_", formals!, body!);
            return v;
        }

        private static IVal Cmp(List<IVal>? p, Func<IVal, IVal, bool> f) {
            if (p == null) return NIL;
            if (p.Count < 2) return p?[0] ?? NIL;
            return f(p[0]!, p[1]!) ? TRUE : NIL;
        }

        public static Env RootEnv = new Env {
            _env = {
                { "t", TRUE },
                { "nil", NIL },
                { "exit", new FuncVal("exit", (p, env) => new ExitVal()) }, 
                { "list", new FuncVal("list", (p, env) => (p != null ? new QExpr(p) : NIL)) },
                { "head", new FuncVal("head", (p, env) => (p != null ? new QExpr((p.FirstOrDefault() as QExpr)?.Cells.Take(1).ToList()) : NIL)) },
                { "tail", new FuncVal("tail", (p, env) => (p != null ? new QExpr((p.FirstOrDefault() as QExpr)?.Cells.Skip(1).ToList(), false) : NIL)) },
                { "init", new FuncVal("init", (p, env) => Init(p)) },
                { "join", new FuncVal("join", (p, env) => Join(p, env, false)) },
                // { "cons", new FuncVal("cons", (p, env) => Join(p, env, true)) },
                // { "len", new FuncVal("len", (p, env) => Len(p)) },
                { "eval", new FuncVal("eval", (p, env) => env.EvalQExpr(p?.FirstOrDefault() as QExpr ?? new QExpr(p)) as IVal ?? NIL) },
                { "def", new FuncVal("def", (p, env) => Define(p, env, true)) },
                { "set", new FuncVal("set", (p, env) => Define(p, env)) },
                { "fn", new FuncVal("fn", (p, env) => Fn(p)) },
                { "if", new FuncVal("if", (p, env) => If(p, env)) },
                { "print", new FuncVal("print", (p, env) => { Console.WriteLine(string.Join(@"\n", p?.Select(s => s.ToString()) ?? new List<string>())); return NIL; } )},
                // { "apply", new FuncVal("apply", (p, env) => Apply(p).Evaluate(env)) }, // more efficient 'apply' than global, above
                { "<", new FuncVal("<", (p, env) => Cmp(p, (a, b) => a.CompareTo(b) < 0)) },
                { ">", new FuncVal(">", (p, env) => Cmp(p, (a, b) => a.CompareTo(b) > 0)) },
                // { "not", new FuncVal("not", (p, env) => new TrueVal(!(Cmp(p, (a, b) => false)).UnderlyingValue)) }, // more efficient 'not' than global, above
                { "eq", new FuncVal("eq", (p, env) => Cmp(p, (a, b) => a.CompareTo(b) == 0)) },
                { "val", new FuncVal("val", (p, env) => new NumVal(Num.Parse(p?[0]?.Value?.Replace("\"", "") ?? "0")!))},
                { "is-def", new FuncVal("is-def", (p, env) => (p?[0] is SymVal s && env.Contains(s.Value)) ? TRUE : NIL) },
                { "to-fixed", new FuncVal("to-fixed", (p, env) => new NumVal(Rat.ToRat((p?[0] as NumVal)?.UnderlyingValue ?? new Int()).ToFix(p?.Count > 1 ? (int)(((p?[1] as NumVal)?.UnderlyingValue as Int)?.num ?? BigInteger.Zero) : 10))) },
                { "to-rational", new FuncVal("to-rational", (p, env) => new NumVal(Rat.ToRat((p![0] as NumVal)!.UnderlyingValue))) },
                { "truncate", new FuncVal("truncate", (p, env) => new NumVal((p?[0] as NumVal)?.UnderlyingValue?.ToInt() ?? new Int())) },
                { "error", new FuncVal("error", (p, env) => ERROR(p?[0].Value ?? "Unknown Error")) },

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
        public virtual int CompareTo(IVal? v) {
            if (Value == null) return v?.Value == null ? 0 : -1;
            if (v?.Value == null) return 1;
            if (v.Type != Type) return v.Type.CompareTo(Type);
            return Value!.CompareTo(v!.Value!);
        }
        public abstract IVal Copy();

        public override string ToString() {
            return Value ?? "<not set>";
        }
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

    public class TrueVal : BaseVal<bool> {
        public TrueVal() : base(true, ValType.BOOL) {}
        public override string? Value => "T";
        public override IVal Copy() => new TrueVal();
    }

    public class NumVal : BaseVal<Num> {
        public NumVal(Num n) : base(n, ValType.NUM) {}
        public override IVal Copy() => new NumVal(_t);
        public override int CompareTo(IVal? v) {
            if (Value == null) return v?.Value == null ? 0 : -1;
            if (v?.Value == null) return 1;
            if (v.Type != Type) return v.Type.CompareTo(Type);
            return UnderlyingValue.CompareTo((v as NumVal)?.UnderlyingValue);
        }}

    public class StrVal : BaseVal<string> {
        public StrVal(string s, ValType vt = ValType.STR) : base(s, vt) {}
        public override string Value => _t;
        public override IVal Copy() => new StrVal(_t);
        public override string ToString() {
            return $"\"{Value}\"";
        }
    }

    public class SymVal : StrVal {
        public SymVal(string s, ValType vt = ValType.SYM) : base(s, vt) {}
        public override IVal Evaluate(Env env) {
            if (env.Contains(Value)) return env.GetValue(Value)!;
            return this; // ERROR($"Unbound symbol {Value}");
        }
        public override IVal Copy() => new SymVal(_t);
    }

    public class QExpr : Expr {
        public override ValType Type => ValType.QEXPR;
        public QExpr(List<IVal>? cells = null, bool compactSingleSExpr = true) : base(cells) {
            // Console.WriteLine($"QExpr: Compact={compactSingleSExpr}, Cell[0]={(cells?.Count > 0 ? cells[0].ToString() :"null")}");
            if (compactSingleSExpr && Cells.Count == 1 && Cells[0] is SExpr s) Cells = s.Cells;
        }
        public override string? Value => Cells.Count == 0 ? "NIL" : "{" + base.Value + "}";
        public override IVal Copy() => new QExpr(Cells, false);
    }

    public class SExpr : Expr {
        public static Expr Create(List<Parser.Token> tokens, char? end = null) {
            Expr exp = end == '}' ? Env.NIL : new SExpr();
            var t = tokens.FirstOrDefault();
            while (t != null) {
                tokens.RemoveAt(0);
                if (t.type == Parser.Token.Type.Symbol && t.str == "list") {
                    exp = new QExpr(exp.Cells);
                }
                else  {
                    IVal? val = t.type switch {
                        Parser.Token.Type.EOF => end == null ? null : Env.ERROR("Missing SExClose for SExpr"),
                        Parser.Token.Type.Comment => new CommentVal(),
                        Parser.Token.Type.Error => Env.ERROR(t.str ?? "Unknown error"),
                        Parser.Token.Type.SExOpen => SExpr.Create(tokens, ')'),
                        Parser.Token.Type.QExOpen => SExpr.Create(tokens, '}'),
                        Parser.Token.Type.SExClose => end != ')' ? Env.ERROR("SExClose without SExOpen") : null,
                        Parser.Token.Type.QExClose => end != '}' ? Env.ERROR("QExClose without QExOpen") : null,
                        Parser.Token.Type.Number => new NumVal(t.num!),
                        Parser.Token.Type.String => new StrVal(t.str!),
                        Parser.Token.Type.Symbol => new SymVal(t.str!),
                        _ => Env.ERROR($"Unknown token type {t.type}")
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
        public SExpr(QExpr q) : base(q.Cells) {}
        public override ValType Type => ValType.SEXPR;
        public override IVal Evaluate(Env env) {
            if (Cells.Count == 0) return this;
            Cells = Cells.Select(c => c.Evaluate(env)).ToList();

            // if there are any Errors, return the first one
            var e = Cells.FirstOrDefault(c => c.Type == ValType.ERR);
            if (e != null) return e;

            var fun = Cells[0];
            if (fun.Type != ValType.FUNC) return fun;
            var result = (fun as FuncVal)!.Apply(Cells.Skip(1).ToList(), env);
            //Console.WriteLine($"Evaluate: {Value} => {result.Value}");
            return result;
        }
        public override IVal Copy() => new SExpr(Cells);
    }

    public class FuncVal : SymVal {
        private Func<List<IVal>?, Env, IVal>? _f;
        private QExpr? _formals;
        private QExpr? _body;
        private Env _env = new Env();

        public bool IsBuiltin => _f != null;
        public FuncVal(string s, Func<List<IVal>?, Env, IVal>? f = null) : base(s, ValType.FUNC) { 
            _f = f;
        }
        public FuncVal(string s, QExpr formals, QExpr body, Env? env = null) : this(s) {
            // TODO: ensure that all formal cells are SymVals
            _formals = formals.Copy() as QExpr;
            _body = body.Copy() as QExpr;
            _env = env?.Copy() ?? _env;
        }
        public IVal Apply(List<IVal>? p, Env env) {
            if (_f != null) return _f(p, env);

            var fCount = _formals?.Cells.Count ?? 0;
            var pCount = p?.Count ?? 0;

            if (pCount > 0 || fCount > 0) {
                var i = 0;
                // add formals to 
                for (; i < fCount && i < pCount; ++i) {
                    _env.SetValue(_formals!.Cells[0].Value!, p![i]);
                    _formals.Cells.RemoveAt(0);
                }

                // if not all formals are bound, return a partially-bound func
                if (_formals?.Cells.Count > 0) {
                    return Copy();
                }

                // add on a "Rest" local variable (&_) that is a list of remaining parameters after the formals
                if (i < pCount) _env.SetValue("&_", new QExpr(p!.Skip(i).ToList(), false));

                // and finally add an &x local variable for each additional parameter as well, starting with &n where n = length of formals list
                for (;i < pCount; ++i) {
                    _env.SetValue($"&{i+1}", p![i]);
                }
            }
            else _env.SetValue("&_", Env.NIL);
            _env.Parent = env;

            return new SExpr(_body!).Evaluate(_env.Copy());
        }
        public override IVal Copy() => _f == null ? new FuncVal(_t, _formals!, _body!, _env) : new FuncVal(_t, _f);

        public override string ToString() {
            return $"<{(IsBuiltin ? "builtin" : "function")}: {Value}{((_formals?.Cells.Count ?? 0) > 0 ? " " : "")}{string.Join(" ", _formals?.Cells.Select(s => s.Value) ?? Array.Empty<string>())}>";
        }
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
}