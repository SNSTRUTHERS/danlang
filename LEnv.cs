public class LEnv : Dictionary<string, LVal> {
    public LEnv(LEnv? p = null) : base(StringComparer.OrdinalIgnoreCase) => Parent = p;

    // The enclosing scope: lexical, so a function's call has the scope it was made in (its closure) as its parent,
    // not its caller's; the global environment has none
    public LEnv? Parent {get; set;}

    public LEnv Root => Parent?.Root ?? this;

    public void Def(string s, LVal v) {
        if (Parent != null) Parent.Def(s, v);
        else Put(s, v);
    }

    public void Put(string s, LVal v) {
        this[s] = v.Copy();
    }

    public LEnv Copy() {
        var e = new LEnv();
        foreach (var s in Keys) {
            e.Add(s, this[s].Copy());
        }
        return e;
    }

    // The scope holding s: this one, or the nearest enclosing one that does
    public LEnv? Find(string s) {
        for (var e = this; e != null; e = e.Parent) {
            if (e.ContainsKey(s)) return e;
        }
        return null;
    }

    public LVal Get(string s) {
        var e = Find(s);
        if (e != null) return e[s].Copy();
        return LVal.Err($"Unbound Symbol '{s}'");
    }

    // set!: the nearest binding of s changed, wherever it is
    public LVal Update(string s, LVal v) {
        var e = Find(s);
        if (e == null) return LVal.Err($"Unbound Symbol '{s}'");
        e.Put(s, v);
        return LVal.NIL();
    }
}
