using System.Text;
using System.Numerics;

// A stream: a file opened with 'open', or the console's stdin, stdout and stderr.  Text is read and written as UTF-8;
// a line ends at LF (a CR before it is dropped).  The console's streams go through Console.In and Console.Out, so a
// program's reads and the REPL's share one buffer
public class LStream
{
    public LStream(Stream s) => _stm = s;
    private LStream(int console) => _console = console;

    public static readonly LStream StdIn = new LStream(1);
    public static readonly LStream StdOut = new LStream(2);
    public static readonly LStream StdErr = new LStream(3);

    private Stream? _stm;
    private int _console;   // 1 stdin, 2 stdout, 3 stderr (Console's, as they are when used): 0 a file
    private TextReader? _in => _console == 1 ? Console.In : null;
    private TextWriter? _out => _console == 2 ? Console.Out : _console == 3 ? Console.Error : null;
    private bool _closed;

    public bool IsInput => !_closed && (_in != null || (_stm?.CanRead ?? false));
    public bool IsOutput => !_closed && (_out != null || (_stm?.CanWrite ?? false));
    public bool IsBidirectional => IsInput && IsOutput;
    public bool IsSeekable => !_closed && (_stm?.CanSeek ?? false);
    public LVal Length => IsSeekable ? LVal.Number(new BigInteger(_stm!.Length)) : LVal.Err("Cannot get the length of a non-seekable stream");
    public LVal Position => IsSeekable ? LVal.Number(new BigInteger(_stm!.Position)) : LVal.Err("Cannot get the position of a non-seekable stream");

    public LVal Close() {
        if (_stm == null) return LVal.Err("Cannot close the console's streams");
        if (!_closed) {
            _stm.Close();
            _closed = true;
        }
        return LVal.NIL();
    }

    public LVal SetPosition(LVal byteOffset) {
        if (IsSeekable)
        {
            if (!byteOffset.IsNum) return LVal.Err("Invalid parameter 'byteOffset': is not a number");
            if (byteOffset.NumVal!.CompareTo(Num.Zero) < 0) return LVal.Err("Cannot seek before beginning of stream");
            if (byteOffset.NumVal!.CompareTo(Length.NumVal) > 0) return LVal.Err("Cannot seek past end of stream");
            var offset = (long)byteOffset.NumVal!.ToInt().num;
            _stm!.Position = offset;
            return LVal.Number(new BigInteger(offset));
        }
        return LVal.Err("Cannot seek on a non-seekable stream");
    }

    // A byte (0-255), or NIL at the end
    public LVal ReadByte() {
        if (!IsInput) return LVal.Err("Cannot read from this stream");
        var b = _in != null ? _in.Read() : _stm!.ReadByte();
        return b < 0 ? LVal.NIL() : LVal.Number(b);
    }

    // A line, without its end, or NIL at the end
    public LVal ReadLine() {
        if (!IsInput) return LVal.Err("Cannot read from this stream");
        if (_in != null) {
            var s = _in.ReadLine();
            return s == null ? LVal.NIL() : LVal.Str(s);
        }

        var bytes = new List<byte>();
        int b;
        while ((b = _stm!.ReadByte()) >= 0 && b != '\n') bytes.Add((byte)b);
        if (b < 0 && bytes.Count == 0) return LVal.NIL();
        if (bytes.Count > 0 && bytes[^1] == '\r') bytes.RemoveAt(bytes.Count - 1);
        return LVal.Str(Encoding.UTF8.GetString(bytes.ToArray()));
    }

    // The rest of the stream, as a string ("" at the end)
    public LVal ReadAll() {
        if (!IsInput) return LVal.Err("Cannot read from this stream");
        if (_in != null) return LVal.Str(_in.ReadToEnd());
        var ms = new MemoryStream();
        _stm!.CopyTo(ms);
        return LVal.Str(Encoding.UTF8.GetString(ms.ToArray()));
    }

    public LVal Write(string s) {
        if (!IsOutput) return LVal.Err("Cannot write this stream");
        if (_out != null) {
            _out.Write(s);
            _out.Flush();
        }
        else {
            var bytes = Encoding.UTF8.GetBytes(s);
            _stm!.Write(bytes, 0, bytes.Length);
            _stm.Flush();
        }
        return LVal.NIL();
    }
}
