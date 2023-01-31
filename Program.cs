using System.ComponentModel;
public class Program {
    public static readonly int MAJOR_VERSION = 0;
    public static readonly int MINOR_VERSION = 1;

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

    public static int Main(string[] args) {
        var (files, config) = Config.ParseCommandLine(args);

        switch (config.mode) {
        case Config.Mode.RunTests:
            Tests.TestNumbers();
            Tests.TestTokens();
            break;

        // case Config.Mode.Compile:
        default:
            LEnv e = new LEnv();
            Builtins.AddBuiltins(e);

            /* Interactive Prompt */
            if (args.Length == 0) {
                Console.WriteLine($"DanLang Version {MAJOR_VERSION}.{MINOR_VERSION}");
                Console.WriteLine("Type 'exit' to Exit\n");
                Builtins.Load(e, LVal.Sexpr().Add(LVal.Str("globals")));
                Console.WriteLine();
                var prompt = Prompt;
                var parens = "";
                while (true) {
                    var allTokens = new List<Parser.Token>();
                    Parser.Token? last = null;
                    do {
                        Console.Write(parens.Length > 0 ? $"\t{parens} <" : prompt);
                        var tokens = Parser.Tokenize(new StringReader(Console.ReadLine() ?? "exit"), parens).ToList();
                        last = tokens.LastOrDefault();
                        if (last == null) continue;

                        if (last.type == Parser.Token.Type.More) {
                            parens = last.parens;
                            tokens.RemoveAt(tokens.Count - 1);
                        } else parens = "";

                        allTokens.AddRange(tokens);
                    } while (parens.Length > 0);

                    var expr = LVal.ReadExprFromTokens(allTokens.ToList());
                    var ticks = Environment.TickCount;
                    var val = expr?.Eval(e);
                    ticks = Environment.TickCount - ticks;
                    if (val?.IsExit ?? false) break;
                    Console.Write($"{(ticks > 1000 ? $"({ticks}ms)" : "")}=> "); val?.Println();
                }
            }
            /* Supplied with list of files */
            else if (files?.Length >= 1) {
                for (int i = 1; i < files.Length; i++) {
                    LVal a = LVal.Sexpr().Add(LVal.Str(files[i]));
                    LVal x = Builtins.Load(e, a);
                    if (x.IsErr) { x.Println(); }
                }
            }
            break;
        }

        return 0;
    }

    static string? readline(string prompt) {
        Console.Write(prompt);
        return Console.ReadLine();
    }

    static void add_history(string? unused) {}
    /* Lisp Value */
}
