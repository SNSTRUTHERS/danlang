// A name, interned: one object for each name (names are case-insensitive: lower case), compared by reference
public sealed class Name {
    public readonly string Text;
    public readonly int Id;
    private Name(string text, int id) { Text = text; Id = id; }

    private static readonly Dictionary<string, Name> _table = new(StringComparer.Ordinal);
    public static Name Of(string s) {
        if (_table.TryGetValue(s, out var n)) return n;
        var lower = s.ToLowerInvariant();
        if (!_table.TryGetValue(lower, out n)) _table[lower] = n = new Name(lower, _table.Count);
        if (lower != s) _table[s] = n;
        return n;
    }

    public override int GetHashCode() => Id;
    public override string ToString() => Text;

    public static readonly Name Rest = Of("&_");        // a call's extra arguments, a list
}

// A scope: names bound to values (shared: a value is frozen once bound), and the scope it's in (Parent).  A small one
// keeps them in arrays, a big one (the global scope) in a dictionary
public class LEnv {
    public LEnv(LEnv? p = null) => Parent = p;

    // The enclosing scope: lexical, so a function's call has the scope it was made in (its closure) as its parent,
    // not its caller's; the global environment has none
    public LEnv? Parent;

    // A function call's own scope: &_ is its, NIL when the call had no extra arguments
    public bool IsCall;

    private const int Small = 8;
    private Name[]? _names;
    private LVal[]? _vals;
    private int _n;
    private Dictionary<Name, Slot>? _map;

    // A big scope's binding: one for each name, kept (a compiled name remembers its global one)
    public sealed class Slot {
        public LVal Value = null!;
    }

    public LEnv Root => Parent?.Root ?? this;
    public int Count => _map?.Count ?? _n;

    // The bindings here (not the enclosing scopes')
    public IEnumerable<KeyValuePair<Name, LVal>> Entries {
        get {
            if (_map != null) foreach (var kv in _map) yield return new KeyValuePair<Name, LVal>(kv.Key, kv.Value.Value);
            else for (int i = 0; i < _n; i++) yield return new KeyValuePair<Name, LVal>(_names![i], _vals![i]);
        }
    }

    public bool TryGetLocal(Name k, out LVal v) {
        if (_map != null) {
            if (_map.TryGetValue(k, out var slot)) {
                v = slot.Value;
                return true;
            }
            v = null!;
            return false;
        }
        for (int i = 0; i < _n; i++) {
            if (ReferenceEquals(_names![i], k)) {
                v = _vals![i];
                return true;
            }
        }
        v = null!;
        return false;
    }

    // A binding here, as it is (the value already shared)
    public void SetLocal(Name k, LVal v) {
        if (_map != null) {
            if (_map.TryGetValue(k, out var slot)) slot.Value = v;
            else _map[k] = new Slot { Value = v };
            return;
        }
        for (int i = 0; i < _n; i++) {
            if (ReferenceEquals(_names![i], k)) {
                _vals![i] = v;
                return;
            }
        }
        if (_n == Small) {
            _map = new Dictionary<Name, Slot>(Small * 2);
            for (int i = 0; i < _n; i++) _map[_names![i]] = new Slot { Value = _vals![i] };
            _map[k] = new Slot { Value = v };
            _names = null;
            _vals = null;
            return;
        }
        if (_names == null) {
            _names = new Name[4];
            _vals = new LVal[4];
        }
        else if (_n == _names.Length) {
            Array.Resize(ref _names, Small);
            Array.Resize(ref _vals, Small);
        }
        _names[_n] = k;
        _vals![_n++] = v;
    }

    // A big scope's slot for k, if it has one
    public Slot? SlotOf(Name k) => _map != null && _map.TryGetValue(k, out var slot) ? slot : null;

    public bool ContainsKey(Name k) => TryGetLocal(k, out _);
    public bool ContainsKey(string s) => ContainsKey(Name.Of(s));

    public void Def(Name k, LVal v) {
        if (Parent != null) Parent.Def(k, v);
        else Put(k, v);
    }
    public void Def(string s, LVal v) => Def(Name.Of(s), v);

    // A binding: the value is shared from here on (frozen), not copied
    public void Put(Name k, LVal v) => SetLocal(k, v.Freeze());
    public void Put(string s, LVal v) => Put(Name.Of(s), v);

    public LEnv Copy() {
        var e = new LEnv();
        foreach (var kv in Entries) e.SetLocal(kv.Key, kv.Value);
        return e;
    }

    // The scope holding k: this one, or the nearest enclosing one that does
    public LEnv? Find(Name k) {
        for (var e = this; e != null; e = e.Parent) {
            if (e.TryGetLocal(k, out _)) return e;
        }
        return null;
    }
    public LEnv? Find(string s) => Find(Name.Of(s));

    // A name's value, from the innermost scope out (shared: values aren't changed); &_ is the nearest call's
    public LVal Get(Name k) {
        for (var e = this; e != null; e = e.Parent) {
            if (e.TryGetLocal(k, out var v)) return v;
            if (e.IsCall && ReferenceEquals(k, Name.Rest)) return LVal.NIL();
        }
        return LVal.Err($"Unbound Symbol '{k.Text}'");
    }
    public LVal Get(string s) => Get(Name.Of(s));

    // set!: the nearest binding of k changed, wherever it is
    public LVal Update(Name k, LVal v) {
        var e = Find(k);
        if (e == null) return LVal.Err($"Unbound Symbol '{k.Text}'");
        e.Put(k, v);
        return LVal.NIL();
    }
    public LVal Update(string s, LVal v) => Update(Name.Of(s), v);
}
