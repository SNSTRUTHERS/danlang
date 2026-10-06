using System.Diagnostics;
using System.Numerics;
using System.Text;

// The system library: the part of hylang's Hydra library that a PC has too.  Files and directories, programs and the
// shell, the environment, the clock, bits and bytes.  On the Hydra each is a system call or a device's file; here,
// .NET's.  A failure is the Hydra's error: its text (ERRSTR's, after the name it's about) and its code, an atom
// (error-code: :noent, :exist ...), so a program, and the regression suite, sees the same on both.
public partial class Builtins
{
    // The Hydra's error codes (spec/errors.def), by name, and their texts
    private static readonly Dictionary<string, string> SysErrors = new() {
        {"perm", "not allowed"}, {"inval", "invalid argument"}, {"nosys", "no such call"}, {"intr", "interrupted"},
        {"nomem", "out of memory"}, {"busy", "busy"}, {"range", "out of range"}, {"nametoolong", "name too long"},
        {"noent", "not found"}, {"exist", "already exists"}, {"notdir", "not a directory"},
        {"isdir", "is a directory"}, {"notempty", "directory not empty"}, {"badf", "bad file descriptor"},
        {"nospc", "disk full"}, {"rofs", "read-only"}, {"io", "i/o error"}, {"noexec", "not a program"},
        {"eof", "end of file"}, {"srch", "no such task"}, {"child", "no such child"},
    };

    // Text between danlang and the host: danlang's characters are bytes (0-255), the host's text UTF-16, so a host's
    // string (a file's name, the environment's, an error's message) comes in as its UTF-8 bytes, a character each, and
    // danlang's goes out as the text those bytes are in UTF-8
    public static string FromHost(string s) => Encoding.Latin1.GetString(Encoding.UTF8.GetBytes(s));
    public static string ToHost(string s) => Encoding.UTF8.GetString(Encoding.Latin1.GetBytes(s));

    // The Hydra's error: "name: text", and its code
    public static LVal SysErr(string code, string? name = null) =>
        LVal.Err((name != null ? name + ": " : "") + SysErrors[code], code);

    // An argument a system built-in can't take: its own message, the code :inval (not .NET's "x: invalid argument")
    public class LArgException : ArgumentException {
        public LArgException(string message) : base(message) {}
    }

    // A .NET exception as the Hydra's error
    public static LVal SysErr(Exception ex, string? name = null) => ex switch {
        FileNotFoundException or DirectoryNotFoundException => SysErr("noent", name),
        UnauthorizedAccessException => SysErr("perm", name),
        PathTooLongException => SysErr("nametoolong", name),
        System.ComponentModel.Win32Exception => SysErr("noent", name),
        IOException io when io.HResult == unchecked((int)0x80070050) || io.HResult == unchecked((int)0x800700B7) => SysErr("exist", name),
        IOException io when io.HResult == unchecked((int)0x80070091) => SysErr("notempty", name),
        IOException => SysErr("io", name),
        LArgException a => LVal.Err(a.Message, "inval"),
        ArgumentException => SysErr("inval", name),
        _ => LVal.Err(FromHost(ex.Message))
    };

    // A system operation: its n arguments evaluated (strings for the paths, or anything op takes), then op; an
    // exception is the Hydra's error, about the first argument
    private static LVal SysOp(LEnv e, LVal a, string fn, Func<LVal[], LVal> op) {
        var args = new LVal[a.Count];
        for (int i = 0; i < args.Length; i++) {
            args[i] = a.Pop(0);
            if (args[i].IsErr) return args[i];
        }
        try {
            return op(args);
        }
        catch (ExitException) { throw; }
        catch (Exception ex) {
            return SysErr(ex, args.Length > 0 ? args[0].ToDisplay() : null);
        }
    }

    // A path argument: a string (or a symbol's or atom's name), as the host has it
    private static string PathOf(LVal v, string fn) {
        if (v.IsStr) return ToHost(v.StrVal);
        if (v.IsAtom || v.IsSym) return ToHost(v.SymVal);
        throw new LArgException($"'{fn}' expects a path");
    }

    // ---- Files and directories

    // The seconds since 2000-01-01 (the Hydra's clock's) of a time
    private static readonly DateTime Epoch = new DateTime(2000, 1, 1);
    private static long SecondsOf(DateTime t) => (long)Math.Floor((t - Epoch).TotalSeconds);

    // A file's stat record, as a hash: :name, :length, :dir (T for a directory), :mtime (seconds since 2000)
    private static LVal StatOf(string path) {
        FileSystemInfo fi = Directory.Exists(path) ? new DirectoryInfo(path) : new FileInfo(path);
        if (!fi.Exists) return SysErr("noent", FromHost(path));
        var h = new LHash();
        h.Put(LVal.Atom("name"), LVal.Str(FromHost(fi.Name == "" ? path : fi.Name)));
        h.Put(LVal.Atom("length"), LVal.Number(fi is FileInfo f ? f.Length : 0));
        h.Put(LVal.Atom("dir"), LVal.Bool(fi is DirectoryInfo));
        h.Put(LVal.Atom("mtime"), LVal.Number(new BigInteger(SecondsOf(fi.LastWriteTime))));
        return LVal.Hash(h);
    }

    // The names in a directory, sorted (or a file's own name)
    private static List<string> Names(string path) {
        if (File.Exists(path)) return new List<string> { Path.GetFileName(path) };
        if (!Directory.Exists(path)) throw new DirectoryNotFoundException();
        var names = Directory.EnumerateFileSystemEntries(path).Select(p => Path.GetFileName(p)!).ToList();
        names.Sort(string.CompareOrdinal);
        return names;
    }

    private static LVal StrList(IEnumerable<string> ss) {
        var l = LVal.Qexpr();
        foreach (var s in ss) l.Add(LVal.Str(s));
        return l;
    }

    // The texts given, as print shows them, joined
    private static string Texts(LVal[] a, int from) => string.Concat(a.Skip(from).Select(v => v.ToDisplay()));

    // A string's lines (LF, or CR LF), a last one with no LF too
    private static LVal Lines(string s) {
        var lines = s.Replace("\r\n", "\n").Split('\n').ToList();
        if (lines.Count > 0 && lines[^1] == "") lines.RemoveAt(lines.Count - 1);
        return StrList(lines);
    }

    // rc's patterns: * (any run), ? (any character), [...] (one of them; a-z a range; ~ first: none of them)
    private static bool Match(string p, string s) {
        int pi = 0, si = 0, star = -1, mark = 0;
        while (si < s.Length) {
            if (pi < p.Length && p[pi] == '*') { star = pi++; mark = si; continue; }
            if (pi < p.Length && MatchOne(p, ref pi, s[si])) { si++; continue; }
            if (star < 0) return false;
            pi = star + 1;
            si = ++mark;
        }
        while (pi < p.Length && p[pi] == '*') pi++;
        return pi == p.Length;
    }

    // One character of the pattern, at pi (moved past it), against c
    private static bool MatchOne(string p, ref int pi, char c) {
        if (p[pi] == '?') { pi++; return true; }
        if (p[pi] != '[') return p[pi++] == c;
        int i = pi + 1;
        var not = i < p.Length && p[i] == '~';
        if (not) i++;
        var found = false;
        while (i < p.Length && p[i] != ']') {
            if (i + 2 < p.Length && p[i + 1] == '-' && p[i + 2] != ']') {
                if (c >= p[i] && c <= p[i + 2]) found = true;
                i += 3;
            }
            else if (p[i++] == c) found = true;
        }
        if (i >= p.Length) return p[pi++] == c;   // no ]: a [ as itself
        pi = i + 1;
        return found != not;
    }

    private static bool IsPattern(string s) => s.IndexOfAny(new[] {'*', '?', '['}) >= 0;

    // (glob pattern): the paths matching it (each element of it a pattern), sorted
    private static List<string> Glob(string pattern) {
        var parts = pattern.Replace('\\', '/').Split('/');
        var paths = new List<string> { "" };
        for (int i = 0; i < parts.Length; i++) {
            var part = parts[i];
            var next = new List<string>();
            foreach (var p in paths) {
                var prefix = i == 0 ? "" : p + "/";
                if (i == 0 && part == "") { next.Add(""); continue; }       // an absolute path's /
                if (!IsPattern(part)) {
                    next.Add(prefix + part);
                    continue;
                }
                var dir = i == 0 ? "." : (p == "" ? "/" : p);
                if (!Directory.Exists(dir)) continue;
                foreach (var name in Names(dir))
                    if (Match(part, name)) next.Add(prefix + name);
            }
            paths = next;
        }
        var found = paths.Where(p => File.Exists(p) || Directory.Exists(p)).ToList();
        found.Sort(string.CompareOrdinal);
        return found;
    }

    // ---- Programs and the shell

    private static readonly Dictionary<int, Process> _spawned = new();

    // A program started: its name, its arguments; its input given (or the console's), its output taken (or the console's)
    private static Process StartProgram(string prog, IEnumerable<string> args, string? input, bool takeOutput) {
        var psi = new ProcessStartInfo(prog) {
            UseShellExecute = false,
            RedirectStandardInput = input != null,
            RedirectStandardOutput = takeOutput,
            StandardInputEncoding = input != null ? Encoding.Latin1 : null,
            StandardOutputEncoding = takeOutput ? Encoding.Latin1 : null,
        };
        foreach (var arg in args) psi.ArgumentList.Add(arg);
        var p = Process.Start(psi) ?? throw new FileNotFoundException();
        if (input != null) {
            p.StandardInput.Write(input);
            p.StandardInput.Close();
        }
        return p;
    }

    // The shell a line goes to: rc on the Hydra; cmd here, or sh
    private static (string, string[]) Shell(string line) =>
        OperatingSystem.IsWindows() ? ("cmd.exe", new[] {"/c", line}) : ("/bin/sh", new[] {"-c", line});

    private static string ArgText(LVal v) => ToHost(v.ToDisplay());

    // ---- The clock

    private static readonly Stopwatch _clock = Stopwatch.StartNew();
    private const int TickHz = 200;

    private static LVal DateParts(DateTime t) {
        var h = new LHash();
        h.Put(LVal.Atom("year"), LVal.Number(t.Year));
        h.Put(LVal.Atom("month"), LVal.Number(t.Month));
        h.Put(LVal.Atom("day"), LVal.Number(t.Day));
        h.Put(LVal.Atom("hour"), LVal.Number(t.Hour));
        h.Put(LVal.Atom("minute"), LVal.Number(t.Minute));
        h.Put(LVal.Atom("second"), LVal.Number(t.Second));
        h.Put(LVal.Atom("weekday"), LVal.Number((int)t.DayOfWeek));
        return LVal.Hash(h);
    }

    private static DateTime TimeArg(LVal[] a, int i) => i < a.Length ? Epoch.AddSeconds((double)IntOf(a[i], "a time")) : DateTime.Now;

    // ---- Bits and bytes

    // An integer argument (a number equal to one counts: 2.0)
    private static BigInteger IntOf(LVal v, string what) {
        if (v.IsPlainInt(out var n)) return n;
        if (v.IsNum && !(v.NumVal is Comp)) {
            var r = Rat.ToRat(v.NumVal!);
            if (r.den == 1) return r.num;
        }
        throw new LArgException($"{what} must be an integer");
    }

    private static LVal Bits(LEnv e, LVal a, string fn, BigInteger start, Func<BigInteger, BigInteger, BigInteger> op) {
        BigInteger r = default;
        for (int i = 0; i < a.Count; i++) {
            var v = a[i];
            if (v.IsErr) return v;
            if (!v.IsPlainInt(out var n)) {
                try { n = IntOf(v, $"'{fn}''s argument"); }
                catch (Exception ex) { return SysErr(ex, a[0].ToDisplay()); }
            }
            r = i == 0 ? n : op(r, n);
        }
        return LVal.Number(r);
    }

    // An integer's digits in base b (upper case), at least width of them; a - before a negative one's
    private static string Digits(BigInteger n, int b, int width) {
        var neg = n < 0;
        if (neg) n = -n;
        var sb = new StringBuilder();
        do {
            sb.Insert(0, "0123456789ABCDEF"[(int)(n % b)]);
            n /= b;
        } while (n > 0);
        while (sb.Length < width) sb.Insert(0, '0');
        return (neg ? "-" : "") + sb;
    }

    // A string's bytes (its characters' codes, each 0-255)
    private static LVal BytesOf(string s) {
        var l = LVal.Qexpr();
        foreach (var c in s) {
            if (c > 255) throw new LArgException($"'{c}' isn't a byte");
            l.Add(LVal.Number(c));
        }
        return l;
    }

    private static string StringOfBytes(LVal l, string fn) {
        if (!l.IsQExpr) throw new LArgException($"'{fn}' expects a list of bytes");
        var sb = new StringBuilder();
        foreach (var b in l.Cells!) {
            var n = IntOf(b, "a byte");
            if (n < 0 || n > 255) throw new LArgException($"{n} isn't a byte");
            sb.Append((char)(int)n);
        }
        return sb.ToString();
    }

    public static void AddSystemBuiltins(LEnv e) {
        // ---- files and directories
        AddBuiltin(e, "read-file", (e, a) => SysOp(e, a, "read-file", x => {
            var p = PathOf(x[0], "read-file");
            if (Directory.Exists(p)) return SysErr("isdir", FromHost(p));
            return LVal.Str(File.ReadAllText(p, Encoding.Latin1));
        }));
        AddBuiltin(e, "read-lines", (e, a) => SysOp(e, a, "read-lines", x => {
            var p = PathOf(x[0], "read-lines");
            if (Directory.Exists(p)) return SysErr("isdir", FromHost(p));
            return Lines(File.ReadAllText(p, Encoding.Latin1));
        }));
        AddBuiltin(e, "write-file", (e, a) => SysOp(e, a, "write-file", x => {
            File.WriteAllText(PathOf(x[0], "write-file"), Texts(x, 1), Encoding.Latin1);
            return LVal.NIL();
        }));
        AddBuiltin(e, "append-file", (e, a) => SysOp(e, a, "append-file", x => {
            File.AppendAllText(PathOf(x[0], "append-file"), Texts(x, 1), Encoding.Latin1);
            return LVal.NIL();
        }));
        AddBuiltin(e, "ls", (e, a) => SysOp(e, a, "ls", x => StrList(Names(x.Length > 0 ? PathOf(x[0], "ls") : ".").Select(FromHost))));
        AddBuiltin(e, "dir", (e, a) => SysOp(e, a, "dir", x => {
            var p = x.Length > 0 ? PathOf(x[0], "dir") : ".";
            var l = LVal.Qexpr();
            foreach (var n in Names(p)) l.Add(StatOf(File.Exists(p) ? p : Path.Join(p, n)));
            return l;
        }));
        AddBuiltin(e, "stat", (e, a) => SysOp(e, a, "stat", x => StatOf(PathOf(x[0], "stat"))));
        AddBuiltin(e, "exists?", (e, a) => SysOp(e, a, "exists?", x => {
            var p = PathOf(x[0], "exists?");
            return LVal.Bool(File.Exists(p) || Directory.Exists(p));
        }));
        AddBuiltin(e, "dir?", (e, a) => SysOp(e, a, "dir?", x => LVal.Bool(Directory.Exists(PathOf(x[0], "dir?")))));
        AddBuiltin(e, "file?", (e, a) => SysOp(e, a, "file?", x => LVal.Bool(File.Exists(PathOf(x[0], "file?")))));
        AddBuiltin(e, "mkdir", (e, a) => SysOp(e, a, "mkdir", x => {
            var p = PathOf(x[0], "mkdir");
            if (File.Exists(p) || Directory.Exists(p)) return SysErr("exist", FromHost(p));
            var parent = Path.GetDirectoryName(Path.GetFullPath(p));
            if (parent != null && !Directory.Exists(parent)) return SysErr("noent", FromHost(p));
            Directory.CreateDirectory(p);
            return LVal.NIL();
        }));
        AddBuiltin(e, "remove", (e, a) => SysOp(e, a, "remove", x => {
            var p = PathOf(x[0], "remove");
            if (Directory.Exists(p)) {
                if (Directory.EnumerateFileSystemEntries(p).Any()) return SysErr("notempty", FromHost(p));
                Directory.Delete(p);
            }
            else if (File.Exists(p)) File.Delete(p);
            else return SysErr("noent", FromHost(p));
            return LVal.NIL();
        }));
        AddBuiltin(e, "rename", (e, a) => SysOp(e, a, "rename", x => {
            var from = PathOf(x[0], "rename");
            var to = PathOf(x[1], "rename");
            if (!File.Exists(from) && !Directory.Exists(from)) return SysErr("noent", FromHost(from));
            if (File.Exists(to) || Directory.Exists(to)) return SysErr("exist", FromHost(to));
            if (Directory.Exists(from)) Directory.Move(from, to);
            else File.Move(from, to);
            return LVal.NIL();
        }));
        AddBuiltin(e, "copy-file", (e, a) => SysOp(e, a, "copy-file", x => {
            var from = PathOf(x[0], "copy-file");
            if (Directory.Exists(from)) return SysErr("isdir", FromHost(from));
            File.Copy(from, PathOf(x[1], "copy-file"), true);
            return LVal.NIL();
        }));
        AddBuiltin(e, "cd", (e, a) => SysOp(e, a, "cd", x => {
            var p = x.Length > 0 ? PathOf(x[0], "cd") : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!Directory.Exists(p)) return SysErr(File.Exists(p) ? "notdir" : "noent", FromHost(p));
            Environment.CurrentDirectory = p;
            return LVal.NIL();
        }));
        AddBuiltin(e, "cwd", (e, a) => SysOp(e, a, "cwd", x => LVal.Str(FromHost(Environment.CurrentDirectory.Replace('\\', '/')))));
        AddBuiltin(e, "glob", (e, a) => SysOp(e, a, "glob", x => StrList(Glob(PathOf(x[0], "glob")).Select(FromHost))));

        // ---- programs and the shell
        AddBuiltin(e, "run", (e, a) => SysOp(e, a, "run", x => {
            var p = StartProgram(PathOf(x[0], "run"), x.Skip(1).Select(ArgText), null, false);
            p.WaitForExit();
            return LVal.Number(p.ExitCode);
        }));
        AddBuiltin(e, "sh", (e, a) => SysOp(e, a, "sh", x => {
            var (prog, args) = Shell(ToHost(x[0].ToDisplay()));
            var p = StartProgram(prog, args, x.Length > 1 ? x[1].ToDisplay() : null, false);
            p.WaitForExit();
            return LVal.Number(p.ExitCode);
        }));
        AddBuiltin(e, "sh-out", (e, a) => SysOp(e, a, "sh-out", x => {
            var (prog, args) = Shell(ToHost(x[0].ToDisplay()));
            var p = StartProgram(prog, args, x.Length > 1 ? x[1].ToDisplay() : null, true);
            var output = p.StandardOutput.ReadToEnd();
            p.WaitForExit();
            return LVal.Str(output.Replace("\r\n", "\n"));
        }));
        AddBuiltin(e, "spawn", (e, a) => SysOp(e, a, "spawn", x => {
            var p = StartProgram(PathOf(x[0], "spawn"), x.Skip(1).Select(ArgText), null, false);
            _spawned[p.Id] = p;
            return LVal.Number(p.Id);
        }));
        AddBuiltin(e, "wait", (e, a) => SysOp(e, a, "wait", x => {
            var id = (int)IntOf(x[0], "A task");
            if (!_spawned.TryGetValue(id, out var p)) return SysErr("child", x[0].ToDisplay());
            p.WaitForExit();
            _spawned.Remove(id);
            return LVal.Number(p.ExitCode);
        }));
        AddBuiltin(e, "kill", (e, a) => SysOp(e, a, "kill", x => {
            var id = (int)IntOf(x[0], "A task");
            if (!_spawned.TryGetValue(id, out var p)) return SysErr("srch", x[0].ToDisplay());
            p.Kill();
            return LVal.NIL();
        }));
        AddBuiltin(e, "pid", (e, a) => SysOp(e, a, "pid", x => LVal.Number(Environment.ProcessId)));

        // ---- the environment
        AddBuiltin(e, "env", (e, a) => SysOp(e, a, "env", x => {
            if (x.Length == 0) {
                var h = new LHash();
                foreach (System.Collections.DictionaryEntry kv in Environment.GetEnvironmentVariables())
                    h.Put(LVal.Str(FromHost((string)kv.Key)), LVal.Str(FromHost((string?)kv.Value ?? "")));
                return LVal.Hash(h);
            }
            var v = Environment.GetEnvironmentVariable(PathOf(x[0], "env"));
            return v == null ? LVal.NIL() : LVal.Str(FromHost(v));
        }));
        AddBuiltin(e, "setenv", (e, a) => SysOp(e, a, "setenv", x => {
            var v = x[1].IsQExpr ? string.Join(" ", x[1].Cells!.Select(c => c.ToDisplay())) : x[1].ToDisplay();
            Environment.SetEnvironmentVariable(PathOf(x[0], "setenv"), ToHost(v));
            return LVal.NIL();
        }));
        AddBuiltin(e, "unsetenv", (e, a) => SysOp(e, a, "unsetenv", x => {
            Environment.SetEnvironmentVariable(PathOf(x[0], "unsetenv"), null);
            return LVal.NIL();
        }));

        // ---- the clock
        AddBuiltin(e, "time", (e, a) => SysOp(e, a, "time", x => LVal.Number(new BigInteger(SecondsOf(DateTime.Now)))));
        AddBuiltin(e, "date", (e, a) => SysOp(e, a, "date", x => LVal.Str(TimeArg(x, 0).ToString("yyyy-MM-dd HH:mm:ss"))));
        AddBuiltin(e, "date-parts", (e, a) => SysOp(e, a, "date-parts", x => DateParts(TimeArg(x, 0))));
        AddBuiltin(e, "seconds-of", (e, a) => SysOp(e, a, "seconds-of", x => {
            var n = x.Select(v => (int)IntOf(v, "A date's part")).ToArray();
            var t = new DateTime(n[0], n[1], n[2], n.Length > 3 ? n[3] : 0, n.Length > 4 ? n[4] : 0, n.Length > 5 ? n[5] : 0);
            return LVal.Number(new BigInteger(SecondsOf(t)));
        }));
        AddBuiltin(e, "ticks", (e, a) => SysOp(e, a, "ticks", x => LVal.Number((int)(_clock.ElapsedMilliseconds * TickHz / 1000 % 32768))));
        AddBuiltin(e, "tick-rate", (e, a) => SysOp(e, a, "tick-rate", x => LVal.Number(TickHz)));
        AddBuiltin(e, "sleep", (e, a) => SysOp(e, a, "sleep", x => {
            if (!x[0].IsNum || x[0].NumVal!.CompareTo(Num.Zero) < 0) return LVal.Err("'sleep' expects a number of seconds");
            var r = Rat.ToRat(x[0].NumVal!);
            Thread.Sleep((int)(r.num * 1000 / r.den));
            return LVal.NIL();
        }));

        // ---- bits and bytes
        AddBuiltin(e, "bit-and", (e, a) => Bits(e, a, "bit-and", BigInteger.MinusOne, (p, q) => p & q));
        AddBuiltin(e, "bit-or",  (e, a) => Bits(e, a, "bit-or", BigInteger.Zero, (p, q) => p | q));
        AddBuiltin(e, "bit-xor", (e, a) => Bits(e, a, "bit-xor", BigInteger.Zero, (p, q) => p ^ q));
        AddBuiltin(e, "bit-not", (e, a) => SysOp(e, a, "bit-not", x => LVal.Number(-IntOf(x[0], "'bit-not''s argument") - 1)));
        AddBuiltin(e, "shl", (e, a) => SysOp(e, a, "shl", x => LVal.Number(IntOf(x[0], "'shl''s number") << (int)IntOf(x[1], "'shl''s count"))));
        AddBuiltin(e, "shr", (e, a) => SysOp(e, a, "shr", x => LVal.Number(IntOf(x[0], "'shr''s number") >> (int)IntOf(x[1], "'shr''s count"))));
        AddBuiltin(e, "bit?", (e, a) => SysOp(e, a, "bit?", x => LVal.Bool(!((IntOf(x[0], "'bit?''s number") >> (int)IntOf(x[1], "'bit?''s bit")) & 1).IsZero)));
        AddBuiltin(e, "hex", (e, a) => SysOp(e, a, "hex", x => LVal.Str(Digits(IntOf(x[0], "'hex''s number"), 16, x.Length > 1 ? (int)IntOf(x[1], "A width") : 1))));
        AddBuiltin(e, "bin", (e, a) => SysOp(e, a, "bin", x => LVal.Str(Digits(IntOf(x[0], "'bin''s number"), 2, x.Length > 1 ? (int)IntOf(x[1], "A width") : 1))));
        AddBuiltin(e, "lo", (e, a) => SysOp(e, a, "lo", x => LVal.Number(IntOf(x[0], "'lo''s number") & 255)));
        AddBuiltin(e, "hi", (e, a) => SysOp(e, a, "hi", x => LVal.Number((IntOf(x[0], "'hi''s number") >> 8) & 255)));
        AddBuiltin(e, "word", (e, a) => SysOp(e, a, "word", x => LVal.Number((IntOf(x[0], "A low byte") & 255) + 256 * (IntOf(x[1], "A high byte") & 255))));
        AddBuiltin(e, "bytes", (e, a) => SysOp(e, a, "bytes", x => {
            if (!x[0].IsStr && !x[0].IsChar) return LVal.Err("'bytes' expects a String");
            return BytesOf(x[0].StrVal);
        }));
        AddBuiltin(e, "from-bytes", (e, a) => SysOp(e, a, "from-bytes", x => LVal.Str(StringOfBytes(x[0], "from-bytes"))));
        AddBuiltin(e, "read-bytes", (e, a) => StreamOp(e, a, "read-bytes", 1, (s, x) => {
            var n = IntOf(x[0], "A count");
            var l = LVal.Qexpr();
            for (var i = BigInteger.Zero; i < n; i++) {
                var b = s.ReadByte();
                if (b.IsErr) return b;
                if (b.IsNIL) break;
                l.Add(b);
            }
            return l.Count == 0 && n > 0 ? LVal.NIL() : l;
        }));
        AddBuiltin(e, "write-bytes", (e, a) => StreamOp(e, a, "write-bytes", 1, (s, x) => s.WriteBytes(StringOfBytes(x[0], "write-bytes"))));

        // ---- where it's running
        AddBuiltin(e, "platform", (e, a) => LVal.Atom(OperatingSystem.IsWindows() ? "windows" : OperatingSystem.IsLinux() ? "linux" : OperatingSystem.IsMacOS() ? "macos" : "host"));
        AddBuiltin(e, "hydra?", (e, a) => LVal.NIL());
    }
}
