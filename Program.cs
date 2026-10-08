using System.Text;

public class Program {
    public static readonly int MAJOR_VERSION = 0;
    public static readonly int MINOR_VERSION = 2;

    private const string Prompt = "danlang>";

    // The interpreter runs on a thread of its own, with a stack big enough for LVal.MaxDepth calls nested
    private const int StackSize = 512 * 1024 * 1024;

    public static int Main(string[] args) {
        // Ctrl-C stops what's running (LVal.Interrupted), not danlang
        Console.CancelKeyPress += (s, a) => {
            a.Cancel = true;
            LVal.Interrupted = true;
        };
        ConsoleBytes();
        var code = 0;
        var t = new Thread(() => code = Run(args), StackSize);
        t.Start();
        t.Join();
        return code;
    }

    // A character is a byte (0-255), as on the Hydra: the console is read and written a byte a character (Latin-1,
    // which maps each byte to the character with its code), and the terminal is told UTF-8, so UTF-8 text shows as
    // itself while danlang sees its bytes
    private static void ConsoleBytes() {
        try {
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;
        }
        catch (IOException) { }     // (no console: input or output redirected)
        Console.SetIn(new StreamReader(Console.OpenStandardInput(), Encoding.Latin1));
        ConsoleMode.Init();
        Console.SetOut(new StreamWriter(Console.OpenStandardOutput(), Encoding.Latin1) { AutoFlush = true });
        Console.SetError(new StreamWriter(Console.OpenStandardError(), Encoding.Latin1) { AutoFlush = true });
    }

    // An environment with the built-ins and the standard library (lib/globals.dl)
    public static LEnv NewEnv() {
        LEnv e = new LEnv { IsGlobal = true };
        Builtins.AddBuiltins(e);
        var g = Builtins.Load(e, LVal.Sexpr().Add(LVal.Str("globals")));
        if (g.IsErr) Console.Error.WriteLine(g.ToStr());
        return e;
    }

    // danlang: the REPL; danlang file [args...]: the file run, args its arguments (args: the file's name, then
    // them), its status 0, 1 after an error (shown on stderr), or (exit n)'s
    private static int Run(string[] args) {
        var (files, config) = Config.ParseCommandLine(args);
        Builtins.Warnings = config.Warnings;
        if (config.Profile) Profiler.Start();
        try {
            return RunAll(files);
        }
        finally {
            if (config.Profile) Profiler.Report(Console.Error);
        }
    }

    private static int RunAll(string[] files) {

        LEnv e;
        try {
            e = NewEnv();
        }
        catch (ExitException x) {
            return x.Code;
        }

        // a file to run
        if (files.Length >= 1) {
            var argv = LVal.Qexpr();
            foreach (var f in files) argv.Add(LVal.Str(Builtins.FromHost(f)));
            e.Put("args", argv);
            try {
                var x = Builtins.Load(e, LVal.Sexpr().Add(LVal.Str(Builtins.FromHost(files[0]))));
                if (x.IsErr) {
                    Console.Error.WriteLine(x.ToStr());
                    var trace = x.Trace();
                    if (trace.Length > 0) Console.Error.WriteLine(trace);
                    return 1;
                }
                return 0;
            }
            catch (ExitException x) {
                return x.Code;
            }
        }

        // the REPL: a line, and more lines while a bracket or a here string is open, the text so far read again with
        // each (so a here string goes on over lines, and an error ends the expression at once)
        e.Put("args", LVal.Qexpr());
        Console.WriteLine($"DanLang Version {MAJOR_VERSION}.{MINOR_VERSION}");
        Console.WriteLine("Type 'exit' to Exit\n");
        var prompt = Prompt;
        while (true) {
            var text = new StringBuilder();
            var tokens = new List<Parser.Token>();
            var parens = "";
            var eof = false;
            var cancelled = false;
            do {
                Console.Write(parens.Length > 0 ? $"\t{parens} <" : prompt);
                ConsoleMode.Cooked();
                var line = Console.ReadLine();
                if (line == null && LVal.Interrupted) {
                    // Ctrl-C at the prompt: the line given up, a new prompt
                    LVal.Interrupted = false;
                    Console.WriteLine();
                    cancelled = true;
                    break;
                }
                if (line == null) {
                    // the input's end: exit, or, in an open expression, what it's missing
                    eof = true;
                    if (parens.Length > 0) {
                        tokens = new List<Parser.Token> { new Parser.Token { type = Parser.Token.Type.Error, str = $"missing {parens}" } };
                        break;
                    }
                    line = "exit";
                }
                text.Append(line).Append('\n');
                tokens = Parser.Tokenize(new StringReader(text.ToString())).ToList();
                var last = tokens.Last();
                var failed = tokens.Any(tk => tk.type == Parser.Token.Type.Error);
                parens = last.type == Parser.Token.Type.More && !failed ? last.parens : "";
            } while (parens.Length > 0);
            if (cancelled) continue;

            try {
                var bad = tokens.FirstOrDefault(tk => tk.type == Parser.Token.Type.Error);
                if (bad != null) {
                    Console.WriteLine($"=> Error: {bad.str}");
                    if (eof) return 0;
                    continue;
                }
                var expr = LVal.ReadExprFromTokens(tokens)?.Freeze();
                LVal.Interrupted = false;
                var ticks = Environment.TickCount;
                var val = expr?.Eval(e);
                ticks = Environment.TickCount - ticks;
                Console.Write($"{(ticks > 1000 ? $"({ticks}ms)" : "")}=> "); val?.Println();
                if (val != null && val.IsErr && val.Trace().Length > 0) Console.Error.WriteLine(val.Trace());
                if (val?.IsExit ?? false) return val.ExitCode;
            }
            catch (ExitException x) {
                return x.Code;
            }
            catch (Exception x) {
                Console.WriteLine($"=> Error: {x.Message}");
            }
            if (eof) return 0;
        }
    }
}
