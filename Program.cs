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
                        tok.type != Parser.Token.Type.RParen ? " " : "",
                    Parser.Token.Type.Comment => "\n",
                    _ => "" });
            }

            if (tok.type == Parser.Token.Type.LParen && indentLevel > 0) {
                if (priorToken?.type != Parser.Token.Type.RParen) Console.WriteLine();
                Console.Write(new String(' ', 2 * indentLevel));
            }
            
            if (tok.type == Parser.Token.Type.RParen) {
                --indentLevel;
                if (priorToken?.type == Parser.Token.Type.RParen && indentLevel > 0)
                    Console.Write(new String(' ', 2 * indentLevel));
            }

            Console.Write(tok.raw);
            if (tok.type == Parser.Token.Type.LParen) ++indentLevel;
            if (tok.type == Parser.Token.Type.RParen) {
                Console.WriteLine();
            }
            priorToken = tok;
        }
    }

    private static void DoTokenize(TextReader reader) {
        var tokens = Parser.Tokenize(reader);
        var env = Env.RootEnv;
        Env.AddGlobalsDefinesToEnv(env);
        Console.WriteLine(SExpr.Create(tokens.ToList(), true).Evaluate(env).Value);
    }

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
                    var reader = filename == "-" ? Console.In : new StreamReader(filename);
                    DoTokenize(reader);
                    if (reader != Console.In) reader.Dispose();
                }
            } else {
                var env = Env.RootEnv;
                Env.AddGlobalsDefinesToEnv(env);
                while (true) {
                    var tokens = Parser.Tokenize(new StringReader(Console.ReadLine() ?? "exit"));
                    var expr = SExpr.Create(tokens.ToList(), true);
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
