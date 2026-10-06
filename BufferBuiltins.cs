using System.Numerics;

// Buffers: bytes, changed in place (reference.md, section 4's Buffers): made, read (a buffer is called with an index, as
// a hash is with a key), written, filled, copied, and read from a stream or written to one
public partial class Builtins
{
    // A byte argument: an integer 0-255, or a character; else the error saying so
    private static LVal ByteArg(LVal v, string fn, out byte b) {
        b = 0;
        if (v.IsErr) return v;
        if (v.IsChar && v.StrVal.Length == 1 && v.StrVal[0] < 256) {
            b = (byte)v.StrVal[0];
            return v;
        }
        if (v.IsPlainInt(out var n) && n >= 0 && n <= 255) {
            b = (byte)(int)n;
            return v;
        }
        return LVal.Err($"'{fn}': {v.ToStr()} isn't a byte (0-255)", "inval");
    }

    // A buffer argument; else the error saying so
    private static LVal BufferArg(LVal v, string fn) =>
        v.IsErr || v.IsBuffer ? v : LVal.Err($"'{fn}' expects a buffer, not {v.ToStr()}");

    // An index into something n long (0 to n - 1; with end, 0 to n); else the error saying so
    private static LVal IndexArg(LVal v, string fn, int n, bool end, out int i) {
        i = 0;
        var w = Whole(v, fn, "an index", out var k);
        if (w.IsErr) return w;
        if (k < 0 || k > n || (k == n && !end)) return LVal.Err($"'{fn}': index {k} is outside the buffer");
        i = (int)k;
        return w;
    }

    // A count, from i, of something n long: what there is, at most (a negative count is an error)
    private static LVal CountArg(LVal v, string fn, int n, int i, out int count) {
        count = 0;
        var w = Whole(v, fn, "a count", out var k);
        if (w.IsErr) return w;
        if (k < 0) return LVal.Err($"'{fn}': a count can't be negative");
        count = (int)BigInteger.Min(k, n - i);
        return w;
    }

    // (buffer n [fill]), (buffer s), (buffer l), (buffer b): a buffer of n bytes (each fill, or 0); of a string's
    // bytes, a list's (each 0-255) or another buffer's (a copy)
    private static LVal MakeBuffer(LEnv e, LVal a) {
        var x = a[0];
        if (x.IsStr || x.IsChar) {
            if (a.Count > 1) return LVal.Err("'buffer' takes a fill only with a count");
            var bytes = new byte[x.StrVal.Length];
            for (int i = 0; i < bytes.Length; i++) {
                if (x.StrVal[i] > 255) return LVal.Err($"'buffer': '{x.StrVal[i]}' isn't a byte", "inval");
                bytes[i] = (byte)x.StrVal[i];
            }
            return LVal.Buffer(bytes);
        }
        if (x.IsQExpr) {
            if (a.Count > 1) return LVal.Err("'buffer' takes a fill only with a count");
            var bytes = new byte[x.Count];
            for (int i = 0; i < bytes.Length; i++) {
                var b = ByteArg(x.Cells![i], "buffer", out bytes[i]);
                if (b.IsErr) return b;
            }
            return LVal.Buffer(bytes);
        }
        if (x.IsBuffer) {
            if (a.Count > 1) return LVal.Err("'buffer' takes a fill only with a count");
            return LVal.Buffer((byte[])x.BufferValue!.Clone());
        }
        var w = Whole(x, "buffer", "a size", out var n);
        if (w.IsErr) return w;
        if (n < 0 || n > Array.MaxLength) return LVal.Err($"'buffer': {n} bytes is outside 0 to {Array.MaxLength}");
        byte fill = 0;
        if (a.Count > 1) {
            var f = ByteArg(a[1], "buffer", out fill);
            if (f.IsErr) return f;
        }
        var buf = new byte[(int)n];
        if (fill != 0) Array.Fill(buf, fill);
        return LVal.Buffer(buf);
    }

    // (buffer-get b i), as (b i) is: byte i
    internal static LVal BufferGet(LVal b, LVal i) {
        var v = BufferArg(b, "buffer-get");
        if (v.IsErr) return v;
        var bytes = b.BufferValue!;
        if (i.IsPlainInt(out var k) && k >= 0 && k < bytes.Length) return LVal.Number(bytes[(int)k]);
        var ix = IndexArg(i, "buffer-get", bytes.Length, false, out var at);
        return ix.IsErr ? ix : LVal.Number(bytes[at]);
    }

    // (buffer-put b i byte): byte i set; its old value
    private static LVal BufferPut(LVal b, LVal i, LVal x) {
        var v = BufferArg(b, "buffer-put");
        if (v.IsErr) return v;
        var bytes = b.BufferValue!;
        var ix = IndexArg(i, "buffer-put", bytes.Length, false, out var at);
        if (ix.IsErr) return ix;
        var y = ByteArg(x, "buffer-put", out var byt);
        if (y.IsErr) return y;
        var old = bytes[at];
        bytes[at] = byt;
        return LVal.Number(old);
    }

    // (buffer-fill b byte [i [n]]): NIL, the bytes from i (0) on, n of them (to the end), set to byte
    private static LVal BufferFill(LEnv e, LVal a) {
        var v = BufferArg(a[0], "buffer-fill");
        if (v.IsErr) return v;
        var bytes = a[0].BufferValue!;
        var y = ByteArg(a[1], "buffer-fill", out var byt);
        if (y.IsErr) return y;
        int at = 0, count = bytes.Length;
        if (a.Count > 2) {
            var ix = IndexArg(a[2], "buffer-fill", bytes.Length, true, out at);
            if (ix.IsErr) return ix;
            count = bytes.Length - at;
        }
        if (a.Count > 3) {
            var c = CountArg(a[3], "buffer-fill", bytes.Length, at, out count);
            if (c.IsErr) return c;
        }
        Array.Fill(bytes, byt, at, count);
        return LVal.NIL();
    }

    // (buffer-copy to at from [i [n]]): NIL, from's bytes from i (0) on, n of them (to its end), put in to at at (as
    // many as fit); the two may be one buffer, the parts overlapping
    private static LVal BufferCopy(LEnv e, LVal a) {
        var t = BufferArg(a[0], "buffer-copy");
        if (t.IsErr) return t;
        var to = a[0].BufferValue!;
        var ix = IndexArg(a[1], "buffer-copy", to.Length, true, out var at);
        if (ix.IsErr) return ix;
        var f = BufferArg(a[2], "buffer-copy");
        if (f.IsErr) return f;
        var from = a[2].BufferValue!;
        int i = 0, count = from.Length;
        if (a.Count > 3) {
            var fx = IndexArg(a[3], "buffer-copy", from.Length, true, out i);
            if (fx.IsErr) return fx;
            count = from.Length - i;
        }
        if (a.Count > 4) {
            var c = CountArg(a[4], "buffer-copy", from.Length, i, out count);
            if (c.IsErr) return c;
        }
        Array.Copy(from, i, to, at, Math.Min(count, to.Length - at));
        return LVal.NIL();
    }

    // (read-buffer s b [at [n]]): up to n bytes (to b's end) from the stream into b at at (0): how many, or NIL at
    // the stream's end
    private static LVal ReadBuffer(LEnv e, LVal a) {
        var s = a[0];
        if (s.IsErr) return s;
        if (!s.IsStream) return LVal.Err("'read-buffer' expects a stream");
        var v = BufferArg(a[1], "read-buffer");
        if (v.IsErr) return v;
        var bytes = a[1].BufferValue!;
        int at = 0, count = bytes.Length;
        if (a.Count > 2) {
            var ix = IndexArg(a[2], "read-buffer", bytes.Length, true, out at);
            if (ix.IsErr) return ix;
            count = bytes.Length - at;
        }
        if (a.Count > 3) {
            var c = CountArg(a[3], "read-buffer", bytes.Length, at, out count);
            if (c.IsErr) return c;
        }
        return s.StreamValue!.ReadInto(bytes, at, count);
    }

    private static void AddBufferBuiltins(LEnv e) {
        AddBuiltin(e, "buffer", MakeBuffer);
        AddBuiltin(e, "buffer?", (e, a) => LVal.Bool(a[0].IsBuffer));
        AddBuiltin(e, "buffer-get", (e, a) => BufferGet(a[0], a[1]));
        AddBuiltin(e, "buffer-put", (e, a) => BufferPut(a[0], a[1], a[2]));
        AddBuiltin(e, "buffer-fill", BufferFill);
        AddBuiltin(e, "buffer-copy", BufferCopy);
        AddBuiltin(e, "read-buffer", ReadBuffer);
        Fast(e, "buffer-get", null, (b, i) => b.IsBuffer && i.IsSmallInt(out var k) && k >= 0 && k < b.BufferValue!.Length
            ? LVal.Number(b.BufferValue![(int)k]) : null);
        e.Get("buffer-put").SetFast3((b, i, x) => {
            if (!b.IsBuffer || !i.IsSmallInt(out var k) || !x.IsSmallInt(out var n)) return null;
            var bytes = b.BufferValue!;
            if (k < 0 || k >= bytes.Length || n < 0 || n > 255) return null;
            var old = bytes[(int)k];
            bytes[(int)k] = (byte)(int)n;
            return LVal.Number(old);
        });
    }
}
