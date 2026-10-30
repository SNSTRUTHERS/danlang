using System.Collections;
using System.Runtime.CompilerServices;

// LList.cs - a list's items: a run of an array that other lists may share.  A run of a list's (tail's, drop's, take's
// ...: Slice) is a run of the same array, not a copy, and a list with one more item in front (cons's: Prepend) takes
// the place just before the items when no list has it yet, so going down a list, or building one up, is a step, not a
// copy of it.  Whatever would change items another list may see copies them first: no list sees another's change.  A
// list knows which of its items may not be frozen yet (the first so many), so freezing it needn't look at the rest.  It
// does what the interpreter does with a List<LVal>, as List does it (its exceptions too)
public sealed class LList : IList<LVal>, IReadOnlyList<LVal> {
    // An array's places Lo to Hi, the lists' that share it (the places before Lo, and from Hi on, no list's yet)
    private sealed class Claims {
        public int Lo, Hi;
    }

    private LVal[] _a;
    private Claims? _c;         // (none: the array is this list's alone)
    private int _start, _count;
    private int _unfrozen;      // (the first so many items may not be frozen; the rest are)

    public LList() => _a = Array.Empty<LVal>();
    public LList(int capacity) => _a = capacity > 0 ? new LVal[capacity] : Array.Empty<LVal>();
    public LList(IEnumerable<LVal> items) : this() => AddRange(items);
    private LList(LVal[] a, Claims? c, int start, int count, int unfrozen) =>
        (_a, _c, _start, _count, _unfrozen) = (a, c, start, count, unfrozen);

    public int Count {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _count;
    }
    public bool IsReadOnly => false;

    public LVal this[int i] {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get {
            if ((uint)i >= (uint)_count) return OutOfRange(i);
            return _a[_start + i];
        }
        set {
            if ((uint)i >= (uint)_count) {
                OutOfRange(i);
                return;
            }
            Own();
            _a[_start + i] = value;
            if (!value.Frozen && i >= _unfrozen) _unfrozen = i + 1;
        }
    }

    // (List's exceptions, as List throws them: a List's own)
    private static readonly List<LVal> _none = new();
    private static LVal OutOfRange(int i) => _none[i];
    private void BadRange(int from, int n) => new List<LVal>(new LVal[_count]).GetRange(from, n);

    // The array shared with another list from now on: the places this list has, the lists'
    private Claims Share() => _c ??= new Claims { Lo = _start, Hi = _start + _count };

    // The items, this list's own to change: copied if another list may see them
    private void Own() {
        if (_c == null) return;
        var a = new LVal[Math.Max(_count, 4)];
        Array.Copy(_a, _start, a, 0, _count);
        (_a, _c, _start) = (a, null, 0);
    }

    // Room for n more items at the end: the places after them, if no list has them, else a copy (with room to grow)
    private void Room(int n) {
        int end = _start + _count;
        if ((_c == null || end == _c.Hi) && end + n <= _a.Length) return;
        var a = new LVal[Math.Max(4, Math.Max(_count + n, 2 * _count))];
        Array.Copy(_a, _start, a, 0, _count);
        (_a, _c, _start) = (a, null, 0);
    }

    public void Add(LVal x) {
        Room(1);
        int end = _start + _count;
        _a[end] = x;
        if (_c != null) _c.Hi = end + 1;
        _count++;
        if (!x.Frozen) _unfrozen = _count;
    }

    public void AddRange(IEnumerable<LVal> items) {
        if (items is not LList l) {
            foreach (var x in items) Add(x);
            return;
        }
        int n = l._count;
        if (n == 0) return;
        var (from, at, unfrozen) = (l._a, l._start, l._unfrozen);      // (l may be this list)
        Room(n);
        int end = _start + _count;
        Array.Copy(from, at, _a, end, n);
        if (_c != null) _c.Hi = end + n;
        if (unfrozen > 0) _unfrozen = _count + unfrozen;
        _count += n;
    }

    // x, then this list's items: a new list, in the place just before them if that's free (else a copy, with room
    // before it for a quarter as many again)
    public LList Prepend(LVal x) {
        int unfrozen = x.Frozen && _unfrozen == 0 ? 0 : _unfrozen + 1;
        if (_start > 0 && (_c == null || _start == _c.Lo)) {
            var c = Share();
            c.Lo = _start - 1;
            _a[_start - 1] = x;
            return new LList(_a, c, _start - 1, _count + 1, unfrozen);
        }
        int room = Math.Max(4, _count / 4);
        var a = new LVal[room + _count + 1];
        a[room] = x;
        Array.Copy(_a, _start, a, room + 1, _count);
        return new LList(a, null, room, _count + 1, unfrozen);
    }

    // n items from from on: the same items, a run of the same array (or, if that would keep an array more than twice as
    // big for them, a copy)
    public LList Slice(int from, int n) {
        if (from < 0 || n < 0 || _count - from < n) BadRange(from, n);
        int unfrozen = Math.Clamp(_unfrozen - from, 0, n);
        if (n == 0) return new LList();
        if (2 * n < _a.Length) {
            var a = new LVal[n];
            Array.Copy(_a, _start + from, a, 0, n);
            return new LList(a, null, 0, n, unfrozen);
        }
        return new LList(_a, Share(), _start + from, n, unfrozen);
    }

    public LList GetRange(int from, int n) => Slice(from, n);

    public void RemoveAt(int i) {
        if ((uint)i >= (uint)_count) {
            OutOfRange(i);
            return;
        }
        if (i < _unfrozen) _unfrozen--;
        if (i == 0) {
            // (the first: the items as they are, this list one shorter at the front)
            _start++;
            _count--;
            return;
        }
        Own();
        int end = _start + _count;
        Array.Copy(_a, _start + i + 1, _a, _start + i, _count - i - 1);
        _a[end - 1] = null!;
        _count--;
    }

    public void Insert(int i, LVal x) {
        if ((uint)i > (uint)_count) new List<LVal>(new LVal[_count]).Insert(i, x);
        Own();
        Room(1);
        Array.Copy(_a, _start + i, _a, _start + i + 1, _count - i);
        _a[_start + i] = x;
        _count++;
        _unfrozen = !x.Frozen || i < _unfrozen ? Math.Max(_unfrozen + 1, i + 1) : _unfrozen;
    }

    public void Reverse() {
        Own();
        Array.Reverse(_a, _start, _count);
        if (_unfrozen > 0) _unfrozen = _count;
    }

    public void Clear() {
        if (_c == null) Array.Clear(_a, _start, _count);
        _count = 0;
        _unfrozen = 0;
    }

    public bool Remove(LVal x) {
        int i = IndexOf(x);
        if (i < 0) return false;
        RemoveAt(i);
        return true;
    }

    // The items that may not be frozen, for a value's Freeze to freeze (known frozen from now on): the array, and where
    // in it they start and end
    internal LVal[] Unfrozen(out int from, out int end) {
        (from, end) = (_start, _start + _unfrozen);
        _unfrozen = 0;
        return _a;
    }

    public int IndexOf(LVal x) {
        for (int i = 0; i < _count; i++) if (EqualityComparer<LVal>.Default.Equals(_a[_start + i], x)) return i;
        return -1;
    }
    public bool Contains(LVal x) => IndexOf(x) >= 0;
    public int FindIndex(Predicate<LVal> p) {
        for (int i = 0; i < _count; i++) if (p(_a[_start + i])) return i;
        return -1;
    }
    public int FindLastIndex(Predicate<LVal> p) {
        for (int i = _count - 1; i >= 0; i--) if (p(_a[_start + i])) return i;
        return -1;
    }
    public bool TrueForAll(Predicate<LVal> p) => FindIndex(x => !p(x)) < 0;
    public bool Exists(Predicate<LVal> p) => FindIndex(p) >= 0;
    public void CopyTo(LVal[] a, int at) => Array.Copy(_a, _start, a, at, _count);
    public LVal[] ToArray() {
        var a = new LVal[_count];
        CopyTo(a, 0);
        return a;
    }

    public Enumerator GetEnumerator() => new Enumerator(this);
    IEnumerator<LVal> IEnumerable<LVal>.GetEnumerator() => GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public struct Enumerator : IEnumerator<LVal> {
        private readonly LVal[] _a;
        private readonly int _end;
        private int _i;
        internal Enumerator(LList l) => (_a, _i, _end) = (l._a, l._start - 1, l._start + l._count);
        public LVal Current => _a[_i];
        object IEnumerator.Current => Current;
        public bool MoveNext() => ++_i < _end;
        public void Reset() => throw new NotSupportedException();
        public void Dispose() { }
    }
}
