// danlang -p: a profile, on stderr when the program ends.  A thread of its own looks, every millisecond or so, at
// where evaluation is (LVal.Site, the call being made, and the innermost function's call) and counts it: the calls
// where the most looks found it, and the functions' calls it was most often in (itself or in what they call, as
// far as the innermost)
public static class Profiler {
    private static readonly Dictionary<LVal.CodeInfo, int> _at = new(), _in = new();
    private static int _looks;
    private static volatile bool _on;
    private static Thread? _thread;

    // (Windows sleeps a tick, 15.6 ms, unless asked for 1)
    [System.Runtime.InteropServices.DllImport("winmm.dll")] private static extern uint timeBeginPeriod(uint ms);

    public static void Start() {
        _on = true;
        if (OperatingSystem.IsWindows()) try { timeBeginPeriod(1); } catch (Exception) { }
        _thread = new Thread(() => {
            while (_on) {
                Thread.Sleep(1);
                var (site, call) = LVal.Now();
                _looks++;
                if (site != null) _at[site] = _at.GetValueOrDefault(site) + 1;
                if (call != null) _in[call] = _in.GetValueOrDefault(call) + 1;
            }
        }) { IsBackground = true };
        _thread.Start();
    }

    public static void Report(TextWriter w) {
        _on = false;
        _thread?.Join();
        if (_looks == 0) return;
        void Show(string title, Dictionary<LVal.CodeInfo, int> m) {
            w.WriteLine($"--- {title} (of {_looks} looks)");
            foreach (var (info, n) in m.OrderByDescending(kv => kv.Value).Take(30))
                w.WriteLine($"{100.0 * n / _looks,6:F1}%  {info.Place()}");
        }
        Show("the calls being made", _at);
        Show("the functions' calls they were in (the innermost)", _in);
    }
}
