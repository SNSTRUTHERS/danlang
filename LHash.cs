using System.Text;

public class TaggedValue<T> where T: class {
    protected TaggedValue<T>? _privateCallProxy = null;
    protected static HashSet<string> _ReservedTags = new HashSet<string> {LHash.TAG_LOCKED, LHash.TAG_RO};
    private T? _value;
    public T? Value {get => _privateCallProxy != null ? _privateCallProxy._value : _value; set => _value = value;}
    public HashSet<string>? Tags = null;

    public TaggedValue(T? val = null) {Value = val;}
    public TaggedValue(TaggedValue<T> val, bool isProxy = false) {
        if (isProxy) _privateCallProxy = val;
        else {
            Value = val._value;
            if (val.Tags != null) {
                foreach (var t in val.Tags) _Add(t);
            }
        }
    }

    public bool _Add(string t) {
        if (_privateCallProxy != null) return _privateCallProxy._Add(t);
        Tags = Tags ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return Tags.Add(t);
    }

    public virtual LVal AddTag(LVal t) {
//        Console.WriteLine($"Adding tag {t.SymVal}");
        if (t.IsAtom) return LVal.Bool(_Add(t.SymVal));
        return LVal.Err($"A tag must be an atom, not {t.ToStr()}");
    }

    public virtual LVal HasTag(LVal t) {
        if (t.IsAtom) return LVal.Bool(Tags?.Contains(t.SymVal) ?? false);
        return LVal.Err("Invalid tag value");
    }

    public bool IsLocked => Tags?.Contains(LHash.TAG_LOCKED) ?? false;
    public void Lock() => _Add(LHash.TAG_LOCKED);
    public bool IsReadonly => Tags?.Contains(LHash.TAG_RO) ?? false;
    public void MakeReadOnly() => _Add(LHash.TAG_RO);

    public static bool IsReserved(string s) => _ReservedTags.Contains(s);
}

public class LHash : TaggedValue<Dictionary<string, LHash.LHashEntry>> {
    public const string TAG_LOCKED = "__locked";
    public const string TAG_RO = "__read-only";
    public const string TAG_PRIV = "__private";
    public const string TAG_NOT_NIL = "__not_nil";
    public LHash() : base(new Dictionary<string, LHash.LHashEntry>()) {}
    public LHash(LHash l, LVal? overrides = null, bool isProxy = false) {
        if (isProxy) {
            _privateCallProxy = l;
            return;
        }

        // make sure entry values are copied properly
        if (l.Value != null) {
            Value = new Dictionary<string, LHashEntry>();
            foreach (var kvp in l.Value) {
                var he = new LHashEntry();
                if (kvp.Value.Tags != null) {
                    foreach (var t in kvp.Value.Tags) he._Add(t);
                }
                if (kvp.Value.Value?.IsHash ?? false) {
                    he.Value = LVal.Hash(kvp.Value.Value.HashValue!.Clone());
                }
                else he.Value = kvp.Value.Value?.Copy();
                Value.Add(kvp.Key, he);
            }
        }

        // the overrides, each its own (the first error among them kept: OverrideError)
        if (overrides != null)
            foreach (var o in overrides.Cells ?? new List<LVal>()) {
                OverrideError = Override(o);
                if (OverrideError != null) break;
            }

        // the hash's own tags (locked, read-only ...): after the overrides, which they'd stop
        if (l.Tags != null) foreach (var t in l.Tags) _Add(t);
    }

    // hash-clone's overrides' first error (null: none)
    public LVal? OverrideError;

    // An override put (one of hash-clone's arguments, the hash's own tags still to come): a tag, an entry {key value
    // tag...} (its value as it is), or a list of them (a list of one is its item); anything else, or a tag or a put
    // that fails, is an error, the first
    private LVal? Override(LVal overrides) {
        var v = overrides;
        while (v.Count == 1) v = v[0];
        if (v.IsAtom) return Failed(AddTag(v));
        if (v.Count > 1 && IsKey(v[0])) return PutEntry(v);
        if (!v.IsQExpr || v.Count == 0) return LVal.Err($"A hash's entry is {{key value tag...}}, not {v.ToStr()}");
        foreach (var e in v.Cells!) {
            LVal? err;
            if (e.IsAtom) err = Failed(AddTag(e));
            else if (e.IsQExpr && e.Count == 1) err = Failed(AddTag(e[0]));
            else if (e.IsQExpr && e.Count > 1) err = PutEntry(e);
            else err = LVal.Err($"A hash's entry is {{key value tag...}}, not {e.ToStr()}");
            if (err != null) return err;
        }
        return null;
    }

    private static LVal? Failed(LVal v) => v.IsErr ? v : null;

    // An entry, {key value tag...}, put as it is (by a member); the first error
    private LVal? PutEntry(LVal e) {
        var err = Failed(Put(e[0], e[1], true));
        for (int i = 2; err == null && i < e.Count; i++) err = Failed(AddTag(e[0], e[i]));
        return err;
    }

    public LHash PrivateCallProxy => new LHash(this, isProxy: true);

    private Dictionary<string, LHash.LHashEntry>.ValueCollection? _Values => _privateCallProxy != null ? _privateCallProxy.Value?.Values : Value?.Values;
    public LVal Values { get {
        var v = LVal.Qexpr();
        if (_Values != null)
            foreach (var e in _Values) {
                v.Add(e.Value?.Copy() ?? LVal.NIL());
            }
        return v; 
    } }

    private Dictionary<string, LHash.LHashEntry>.KeyCollection? _Keys => _privateCallProxy != null ? _privateCallProxy.Value?.Keys : Value?.Keys;
    public LVal Keys { get {
        var v = LVal.Qexpr();
        if (_Keys != null)
            foreach (var k in _Keys) {
                v.Add(KeyToLVal(k));
            }
        return v; 
    } }

    public int Count => (_privateCallProxy != null ? _privateCallProxy.Value?.Count : Value?.Count) ?? 0;

    private bool _ContainsKey(string s) => (_privateCallProxy != null ? _privateCallProxy.Value?.ContainsKey(s) : Value?.ContainsKey(s)) ?? false;

    public LVal ContainsKey(LVal key) {
        try {
            return LVal.Bool(_ContainsKey(_KeyFromLVal(key)));
        }
        catch (Exception e) {
            return LVal.Err(e.Message);
        }
    }

    public class LHashEntry : TaggedValue<LVal> {
        static LHashEntry() {
            _ReservedTags.Add(TAG_PRIV);
            _ReservedTags.Add(TAG_NOT_NIL);
        }
        public bool IsPrivate => Tags?.Contains(TAG_PRIV) ?? false;
        public bool IsNillable => !(Tags?.Contains(TAG_NOT_NIL) ?? false);
        public bool MakePrivate => _Add(TAG_PRIV);
        public bool MakeNotNil => _Add(TAG_NOT_NIL);

        public void Serialize(string key, string pre, StringBuilder sb) {
            sb.Append(pre).Append('{').Append(KeyToLVal(key).ToStr()).Append(' ');
            sb.Append(Value?.Serialize() ?? "NIL");

            if (Tags != null) {
                foreach (var t in Tags) {
                    sb.Append(" :").Append(t);
                }
            }
            sb.Append('}');
        }
    }

    // A key, as the dictionary keeps it: an atom's name; a string after a '"', a number after a '#' (so each comes back
    // as what it was: KeyToLVal)
    private static string _KeyFromLVal(LVal key) => key.ValType switch {
            LVal.LE.ATOM => key.SymVal!,
            LVal.LE.STR  => "\"" + key.StrVal!,
            LVal.LE.NUM  => Builtins.Whole(key, "hash", "a key", out var n).IsErr
                ? throw new Exception($"A hash key must be an atom, a string or an integer, not {key.ToStr()}")
                : "#" + n.ToString(),
            _            => throw new Exception($"Unsupported key type {LVal.LEName(key.ValType)}")
        };

    public static LVal KeyToLVal(string k) =>
        k.StartsWith('"') ? LVal.Str(k.Substring(1)) :
        k.StartsWith('#') ? LVal.Number(System.Numerics.BigInteger.Parse(k.Substring(1))) :
        LVal.Atom(k);

    public static bool IsKey(LVal key) => key.IsAtom || key.IsStr || key.IsNum;

    private LHashEntry? _GetEntry(LVal key, bool create = false) {
        LHashEntry? e = null;
        var k = _KeyFromLVal(key);
        if (!string.IsNullOrEmpty(k)) {
            if (IsReserved(k)) throw new Exception($"Cannot get/set value for reserved hash key {k}");
            if (_ContainsKey(k)) e = Value![k];
        }

        if (e == null && create && !IsLocked) {
            e = new LHashEntry();
            Value![k] = e;
        }
        return e;
    }

    private bool RemoveEntry(LVal key) {
        var k = _KeyFromLVal(key);
        return Value?.Remove(k) ?? false;
    }

    // hash-remove: the entry taken out; its value, or NIL if there was none
    public LVal Remove(LVal key, bool callerIsMember = false) {
        if (_privateCallProxy != null && !callerIsMember) return ((LHash)_privateCallProxy).Remove(key, true);
        try {
            if (IsReadonly) return LVal.Err("hash-remove error: cannot modify read-only hash");
            var e = _GetEntry(key);
            if (e == null) return LVal.NIL();
            if (e.IsPrivate && !callerIsMember) return LVal.Err("hash-remove error: cannot access private hash entry");
            if (IsLocked || e.IsReadonly) return LVal.Err("hash-remove error: cannot remove an entry from a locked hash");
            var priorValue = e.Value ?? LVal.NIL();
            RemoveEntry(key);
            return priorValue;
        }
        catch (Exception e) {
            return LVal.Err(e.Message);
        }
    }

    // Equal hashes: the same keys, with equal values
    public bool EqualTo(LHash h) {
        var a = _privateCallProxy != null ? _privateCallProxy.Value : Value;
        var b = h._privateCallProxy != null ? h._privateCallProxy.Value : h.Value;
        if (a == null || b == null) return a == b;
        if (a.Count != b.Count) return false;
        foreach (var kvp in a) {
            if (!b.TryGetValue(kvp.Key, out var other)) return false;
            var x = kvp.Value.Value ?? LVal.NIL();
            var y = other.Value ?? LVal.NIL();
            if (!x.Equals(y)) return false;
        }
        return true;
    }

    public LVal Put(LVal key, LVal val, bool callerIsMember = false) {
        if (_privateCallProxy != null && !callerIsMember) return ((LHash)_privateCallProxy).Put(key, val, true);
        try {
            if (IsReadonly) return LVal.Err("hash-put error: cannot modify read-only hash");
            var e = _GetEntry(key, !IsLocked);
            if (e != null) {
                if (e.IsPrivate && !callerIsMember) return LVal.Err("hash-put error: cannot access private hash entry");
                if (e.IsReadonly) return LVal.Err("hash-put error: cannot modify read-only hash entry");

                var priorValue = e.Value ?? LVal.NIL();
                if (val.IsNIL) {
                    if (e.IsNillable) {
                        if (!IsLocked && !e.IsLocked) RemoveEntry(key);
                        else e.Value = null;
                    }
                    else return LVal.Err($"Cannot set non-nillable field {key.ToStr()} to NIL.");
                }
                else e.Value = val.Copy();

                return priorValue;
            }

            return LVal.Err("hash-put error: cannot add new entries to a locked hash");
        }
        catch (Exception e) {
            return LVal.Err(e.Message);
        }
    }

    public LVal Put(LVal entry, bool callerIsMember = false) {
        if (_privateCallProxy != null && !callerIsMember) return ((LHash)_privateCallProxy).Put(entry, true);
        // {tag} alone: a tag on the hash (a key's name: nothing); anything else short of {key value}, an error
        if (entry.Count < 2) {
            if (!entry.IsQExpr || entry.Count == 0 || !entry[0].IsAtom)
                return LVal.Err($"A hash's entry is {{key value tag...}}, not {entry.ToStr()}");
            if (!_ContainsKey(_KeyFromLVal(entry[0]))) AddTag(entry[0]);
            return LVal.NIL();
        }

        for (int i = 2; i < entry.Count; ++i)
            if (!entry[i].IsAtom) return LVal.Err($"A tag must be an atom, not {entry[i].ToStr()}");

        var priorValue = Put(entry[0], entry[1], callerIsMember);
        if (!priorValue.IsErr) {
            if (entry.Count > 2) {
                try {
                    var e = _GetEntry(entry[0]);
                    if (e != null && !e.IsReadonly && (!e.IsPrivate || callerIsMember)) {
                        for (int i = 2; i < entry.Count; ++i) {
                            e.AddTag(entry[i]);
                        }
                    }
                }
                catch {}
            }
        }
        return priorValue;
    }

    public LVal Get(LVal key, bool errOnNotFound = false, bool callerIsMember = false) {
        if (_privateCallProxy != null && !callerIsMember) return ((LHash)_privateCallProxy).Get(key, errOnNotFound, true);
        try {
            var e = _GetEntry(key);
            if (e != null) {
                if (e.IsPrivate && !callerIsMember) return LVal.Err("hash-get error: cannot access private hash entry");
                if (e.Value != null) return e.Value.Copy();
            }
            else if (errOnNotFound) {
                return LVal.Err("hash-get failed: entry not found");
            }
            return LVal.NIL();
        }
        catch (Exception e) {
            return LVal.Err(e.Message);
        }
    }

    public override LVal AddTag(LVal v) {
        if (v.Count == 1 || v.IsAtom) return base.AddTag(v);
        else if (v.Count == 2) return AddTag(v[0], v[1]);
        return LVal.Err($"Failed to add tag: invalid parameters {v.ToStr()}");
    }

    public LVal AddTag(LVal key, LVal tag) {
        try {
            var e = _GetEntry(key);
            if (e != null) {
                return e.AddTag(tag);
            }
            return LVal.Err("hash-add-tag failed: entry not found");
        }
        catch (Exception e) {
            return LVal.Err(e.Message);
        }
    }

    // A tag of the hash's (an atom, or {tag}), or of an entry's ({key tag})
    public override LVal HasTag(LVal t) {
        if (t.IsAtom) return base.HasTag(t);
        if (!t.IsQExpr || t.Count == 0) return LVal.Err("Invalid tag value");
        if (t.Count == 1) return base.HasTag(t[0]);

        var key = t.Pop(0);
        try {
            var e = _GetEntry(key);
            if (e == null) return LVal.Err($"Key {key.ToStr()} not found when looking up tag for hash entry");
            return e.HasTag(t.Pop(0));
        }
        catch (Exception e) {
            return LVal.Err(e.Message);
        }
    }

    public LHash Clone(LVal? overrides = null) {
        if (_privateCallProxy != null) return new LHash((LHash)_privateCallProxy, overrides);
        return new LHash(this, overrides);
    }

    public string Serialize() {
        var sb = new StringBuilder();
        sb.Append("#({");
        var pre = "";
        foreach (var k in _Keys!) {
            Value![k].Serialize(k, pre, sb);
            pre = " ";
        }

        if (Tags != null) {
            foreach (var t in Tags) {
                sb.Append(pre).Append(':').Append(t);
                pre = " ";
            }
        }

        return sb.Append("})").ToString();
    }

    public LVal ToQexpr() {
        LVal v = LVal.Qexpr();
        foreach (var k in _Keys!) {
            var e = LVal.Qexpr();
            e.Add(KeyToLVal(k));
            var entry = Value![k];
            e.Add(entry.Value?.ValType switch {
                null => LVal.NIL(),
                LVal.LE.HASH => entry.Value.HashValue!.ToQexpr(),
                _ => entry.Value.Copy()
            });

            if (entry.Tags != null) {
                foreach (var t in entry.Tags) {
                    e.Add(LVal.Atom(t));
                }
            }
            v.Add(e);
        }

        if (Tags != null) foreach (var t in Tags) v.Add(LVal.Atom(t));

        return v;
    }

    public override string? ToString() => ToQexpr().ToString();
}
