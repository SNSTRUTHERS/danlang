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
        return LVal.Bool(false);
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

        // apply overrides
        // TODO: validate the overall shape and type of the overrides object
        if (overrides != null && overrides.Count > 0) {
            var v = overrides;
            while (v.Count == 1) v = v[0];

            // check for the case where they only apply a single tag
            if (v.Count == 0 && v.IsAtom) {
                AddTag(v);
            }
            // now for if they apply updates to a single element
            else if (v.Count > 1 && v[0].IsAtom) {
                var key = v.Pop(0);
                var val = v.Pop(0);
                Put(key, val, true);

                // now add any tags
                while (v.Count > 0) AddTag(key, v.Pop(0));
            }
            else {
                // case where there are multiple elements updated
                while (v.Count > 0) {
                    var e = v.Pop(0);
                    if (e.Count == 0 && e.IsAtom) AddTag(e);
                    else if (e.Count == 1) AddTag(e[0]);
                    else {
                        var key = e.Pop(0);
                        var val = e.Pop(0);
                        var putResult = Put(key, val, true);
                        
                        // now add any tags
                        while (e.Count > 0) AddTag(key, e.Pop(0));
                    }
                }
            }
        }
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
                v.Add(LVal.Atom(k));
            }
        return v; 
    } }

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
        static LHashEntry() => _ReservedTags.Add(TAG_PRIV);
        public bool IsPrivate => Tags?.Contains(TAG_PRIV) ?? false;
        public bool MakePrivate => _Add(TAG_PRIV);
    }

    private string _KeyFromLVal(LVal key) => key.ValType switch {
            LVal.LE.ATOM => key.SymVal!,
            // LVal.LE.STR  => key.StrVal!, // may need a ToAtom() for strings
            LVal.LE.NUM  => key.NumVal!.ToInt().ToString()!,
            _            => throw new Exception($"Unsupported key type {LVal.LEName(key.ValType)}")
        };

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
                    if (!IsLocked && !e.IsLocked) RemoveEntry(key);
                    else e.Value = null;
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
        if (entry.Count < 2) {
            if (entry.Count == 1 && entry[0].IsAtom && !_ContainsKey(entry[0].SymVal)) {
                AddTag(entry[0]);
            }
            return LVal.NIL();
        }

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

    public override LVal HasTag(LVal t) {
        if (t.Count == 1) return base.HasTag(t[0]);

        var key = t.Pop(0);
        try {
            var e = _GetEntry(key);
            if (e == null) return LVal.Err($"Key {key} not found when looking up tag for hash entry");
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

    public LVal ToQexpr() {
        LVal v = LVal.Qexpr();
        foreach (var k in _Keys!) {
            var e = LVal.Qexpr();
            e.Add(LVal.Atom(k));
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
