public class LEnv : Dictionary<string, LVal> {
    public LEnv(LEnv? p = null) : base(StringComparer.OrdinalIgnoreCase) => Parent = p;
    public LEnv? Parent {get; set;}

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

    public LVal Get(string s) {
        if (ContainsKey(s)) return this[s].Copy();
        return Parent?.Get(s) ?? LVal.Err($"Unbound Symbol '{s}'");
    }
}