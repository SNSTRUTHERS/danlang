using System.Runtime.InteropServices;

// The console's modes, on Windows (elsewhere .NET's ReadKey makes the terminal raw itself as it reads, and the
// terminal takes escape sequences anyway): the output takes the terminal's escape sequences (VT), as a Unix
// terminal's does; the input is raw for key and key? (no line editing, no echo, Ctrl-A and the like a program's, not
// the console window's, and the terminal's keys as the sequences it sends, which key reads back as one key each), and
// cooked again for a line read (raw till the next line is read, as hylang's console has it) and when danlang ends
internal static class ConsoleMode {
    private const int StdInput = -10, StdOutput = -11;
    private const uint LineInput = 0x2, EchoInput = 0x4, VtInput = 0x200, VtOutput = 0x4;

    [DllImport("kernel32.dll")] private static extern IntPtr GetStdHandle(int which);
    [DllImport("kernel32.dll")] private static extern bool GetConsoleMode(IntPtr handle, out uint mode);
    [DllImport("kernel32.dll")] private static extern bool SetConsoleMode(IntPtr handle, uint mode);

    private static IntPtr _in;
    private static uint? _cooked;       // the input's mode as it was (none: not a Windows console)
    private static bool _raw;

    public static void Init() {
        if (!OperatingSystem.IsWindows()) return;
        try {
            var output = GetStdHandle(StdOutput);
            if (GetConsoleMode(output, out var m)) SetConsoleMode(output, m | VtOutput);
            _in = GetStdHandle(StdInput);
            if (GetConsoleMode(_in, out var i)) {
                _cooked = i;
                AppDomain.CurrentDomain.ProcessExit += (s, a) => Cooked();
            }
        }
        catch (Exception) { }           // (no kernel32: not Windows after all)
    }

    // Raw, for a key read (and whether one's waiting)
    public static void Raw() {
        if (_raw || _cooked == null) return;
        _raw = true;
        SetConsoleMode(_in, (_cooked.Value & ~(LineInput | EchoInput)) | VtInput);
    }

    // Cooked again, for a line read
    public static void Cooked() {
        if (!_raw) return;
        _raw = false;
        SetConsoleMode(_in, _cooked!.Value);
    }
}
