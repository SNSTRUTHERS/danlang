public class Program {
    public static readonly int MAJOR_VERSION = 0;
    public static readonly int MINOR_VERSION = 2;

    private static void PrintTokens(IEnumerable<Parser.Token> tokens) {
        Parser.Token? priorToken = null;
        var indentLevel = 0;
        var tokArray = tokens.ToList();
        foreach (var tok in tokArray) {
            if (priorToken != null) {
                Console.Write(priorToken.type switch {
                    Parser.Token.Type.Number or Parser.Token.Type.String or Parser.Token.Type.Symbol =>
                        tok.type != Parser.Token.Type.SExClose ? " " : "",
                    Parser.Token.Type.Comment => "\n",
                    _ => "" });
            }

            if (tok.type == Parser.Token.Type.SExOpen && indentLevel > 0) {
                if (priorToken?.type != Parser.Token.Type.SExClose) Console.WriteLine();
                Console.Write(new String(' ', 2 * indentLevel));
            }

            if (tok.type == Parser.Token.Type.SExClose) {
                --indentLevel;
                if (priorToken?.type == Parser.Token.Type.SExClose && indentLevel > 0)
                    Console.Write(new String(' ', 2 * indentLevel));
            }

            Console.Write(tok.raw);
            if (tok.type == Parser.Token.Type.SExOpen) ++indentLevel;
            if (tok.type == Parser.Token.Type.SExClose) {
                Console.WriteLine();
            }
            priorToken = tok;
        }
    }

    private const string Prompt = "danlang>";

    // The interpreter runs on a thread of its own, with a stack big enough for LVal.MaxDepth calls nested
    private const int StackSize = 512 * 1024 * 1024;

    public static int Main(string[] args) {
        var code = 0;
        var t = new Thread(() => code = Run(args), StackSize);
        t.Start();
        t.Join();
        return code;
    }

    // An environment with the built-ins and the standard library (lib/globals.dl)
    public static LEnv NewEnv() {
        LEnv e = new LEnv();
        Builtins.AddBuiltins(e);
        var g = Builtins.Load(e, LVal.Sexpr().Add(LVal.Str("globals")));
        if (g.IsErr) Console.Error.WriteLine(g.ToStr());
        return e;
    }

    // danlang: the REPL; danlang file [args...]: the file run, args its arguments (args: the file's name, then
    // them), its status 0, 1 after an error (shown on stderr), or (exit n)'s
    private static int Run(string[] args) {
        var (files, config) = Config.ParseCommandLine(args);

        switch (config.mode) {
        case Config.Mode.RunTests:
            Tests.TestNumbers();
            Tests.TestTokens();
            return 0;

        // case Config.Mode.Compile:
        default:
            LEnv e;
            try {
                e = NewEnv();
            }
            catch (ExitException x) {
                return x.Code;
            }

            /* Supplied a file to run */
            if (files.Length >= 1) {
                var argv = LVal.Qexpr();
                foreach (var f in files) argv.Add(LVal.Str(f));
                e.Put("args", argv);
                try {
                    var x = Builtins.Load(e, LVal.Sexpr().Add(LVal.Str(files[0])));
                    if (x.IsErr) {
                        Console.Error.WriteLine(x.ToStr());
                        return 1;
                    }
                    return 0;
                }
                catch (ExitException x) {
                    return x.Code;
                }
            }

            /* Interactive Prompt */
            e.Put("args", LVal.Qexpr());
            Console.WriteLine($"DanLang Version {MAJOR_VERSION}.{MINOR_VERSION}");
            Console.WriteLine("Type 'exit' to Exit\n");
            var prompt = Prompt;
            var parens = "";
            while (true) {
                var allTokens = new List<Parser.Token>();
                Parser.Token? last = null;
                var eof = false;
                do {
                    Console.Write(parens.Length > 0 ? $"\t{parens} <" : prompt);
                    var line = Console.ReadLine();
                    if (line == null) {
                        eof = true;
                        line = "exit";
                    }
                    var tokens = Parser.Tokenize(new StringReader(line), parens).ToList();
                    last = tokens.LastOrDefault();
                    if (last == null) continue;

                    if (last.type == Parser.Token.Type.More && !eof) {
                        parens = last.parens;
                        tokens.RemoveAt(tokens.Count - 1);
                    } else parens = "";

                    allTokens.AddRange(tokens);
                } while (parens.Length > 0);

                try {
                    var bad = allTokens.FirstOrDefault(tk => tk.type == Parser.Token.Type.Error);
                    if (bad != null) {
                        Console.WriteLine($"=> Error: {bad.str}");
                        continue;
                    }
                    var expr = LVal.ReadExprFromTokens(allTokens.ToList());
                    var ticks = Environment.TickCount;
                    var val = expr?.Eval(e);
                    ticks = Environment.TickCount - ticks;
                    Console.Write($"{(ticks > 1000 ? $"({ticks}ms)" : "")}=> "); val?.Println();
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

    static string? readline(string prompt) {
        Console.Write(prompt);
        return Console.ReadLine();
    }

    static void add_history(string? unused) {}
    /* Lisp Value */
}
