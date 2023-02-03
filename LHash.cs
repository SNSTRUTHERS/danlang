public class TaggedValue<T> where T: class {
    protected static HashSet<string> _ReservedTags = new HashSet<string> {LHash.TAG_LOCKED, LHash.TAG_RO};
    public T? Value;
    public HashSet<string>? Tags = null;

    public TaggedValue(T? val = null) {Value = val;}
    public TaggedValue(TaggedValue<T> val) : this(val.Value) {
        if (val.Tags != null) {
            foreach (var t in val.Tags) _Add(t);
        }
    }

    protected bool _Add(string t) {
        Tags = Tags ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return Tags.Add(t);
    }

    public virtual LVal AddTag(LVal t) {
        Console.WriteLine($"Adding tag {t.SymVal}");
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
    public LHash(LHash l, LVal? overrides = null) : base(l) {
        // make sure entry values are copied properly
        foreach (var e in _Values!) {
            e.Value = e.Value?.Copy();
        }

        // apply overrides
        // TODO: validate the overall shape and type of the overrides object
        if (overrides != null && overrides.Count > 0) {
            var entries = overrides.Count == 1 ? overrides[0].Cells : overrides.Cells;
            if (entries != null) foreach (var e in entries) Put(e, true);
        }
    }

    private Dictionary<string, LHash.LHashEntry>.ValueCollection? _Values => Value?.Values;
    public LVal Values { get {
        var v = LVal.Qexpr();
        if (_Values != null)
            foreach (var e in _Values) {
                v.Add(e.Value?.Copy() ?? LVal.NIL());
            }
        return v; 
    } }

    private Dictionary<string, LHash.LHashEntry>.KeyCollection? _Keys => Value?.Keys;
    public LVal Keys { get {
        var v = LVal.Qexpr();
        if (_Keys != null)
            foreach (var k in _Keys) {
                v.Add(LVal.Atom(k));
            }
        return v; 
    } }

    private bool _ContainsKey(string s) => Value?.ContainsKey(s) ?? false;

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
        try {
            if (IsReadonly) return LVal.Err("#put error: cannot modify read-only hash");
            var e = _GetEntry(key, !IsLocked);
            if (e != null) {
                if (e.IsPrivate && !callerIsMember) return LVal.Err("#put error: cannot access private hash entry");
                if (e.IsReadonly) return LVal.Err("#put error: cannot modify read-only hash entry");

                var priorValue = e.Value ?? LVal.NIL();
                if (val.IsNIL) {
                    if (!IsLocked && !e.IsLocked) RemoveEntry(key);
                    else e.Value = null;
                }
                else e.Value = val.Copy();

                return priorValue;
            }

            return LVal.Err("#put error: cannot add new entries to a locked hash");
        }
        catch (Exception e) {
            return LVal.Err(e.Message);
        }
    }

    public LVal Put(LVal entry, bool callerIsMember = false) {
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

    public LVal Get(LVal key, bool errOnNotFound = false) {
        try {
            var e = _GetEntry(key);
            if (e != null) {
                if (e.IsPrivate) return LVal.Err("#get error: cannot access private hash entry");
                if (e.Value != null) return e.Value.Copy();
            }
            else if (errOnNotFound) {
                return LVal.Err("#get failed: entry not found");
            }
            return LVal.NIL();
        }
        catch (Exception e) {
            return LVal.Err(e.Message);
        }
    }

    public override LVal AddTag(LVal v) {
        if (v.Count == 1 || v.IsAtom) return base.AddTag(v);
        return AddTag(v[0], v[1]);
    }

    public LVal AddTag(LVal key, LVal tag) {
        try {
            Console.WriteLine($"Adding tag {tag.ToStr()} to key {key.ToStr()}");
            var e = _GetEntry(key);
            if (e != null) {
                return e.AddTag(tag);
            }
            return LVal.Err("#add-tag failed: entry not found");
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
            if (e == null) return LVal.Err($"Key {key} not found when looking up key tag");
            return e.HasTag(t.Pop(0));
        }
        catch (Exception e) {
            return LVal.Err(e.Message);
        }
    }

    public LHash Clone(LVal? overrides = null) {
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
