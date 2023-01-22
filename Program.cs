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

    private static void DoTokenize(string filename) {
        var env = Env.RootEnv;
        Env.AddGlobalsDefinesToEnv(env);
        Console.WriteLine(Env.Load(filename).Evaluate(env).Value);
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
            if (files.Length > 0) {
                foreach (var filename in files) {
                    DoTokenize(filename);
                }
            } else {
                var env = Env.RootEnv;
                Env.AddGlobalsDefinesToEnv(env);
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
                    var expr = SExpr.Create(allTokens.ToList());
                    var val = expr.Evaluate(env);
                    if (val.Type == ValType.EXIT) break;
                    Console.WriteLine($"=> {val.Value}");
                }
            }
            break;
        }

        return 0;
    }
}
