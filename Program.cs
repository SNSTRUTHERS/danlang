using System.Security.Principal;
public class Program {
    public static readonly int MAJOR_VERSION = 0;
    public static readonly int MINOR_VERSION = 1;

    private static void DoTokenize(TextReader reader) {
        Parser.Token? priorToken = null;
        var indentLevel = 0;
        foreach (var tok in Parser.Tokenize(reader)) {
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

            Console.Write(tok.str);
            if (tok.type == Parser.Token.Type.LParen) ++indentLevel;
            if (tok.type == Parser.Token.Type.RParen) {
                Console.WriteLine();
            }
            priorToken = tok;
        }
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
                DoTokenize(Console.In);
            }
            break;
        }

        return 0;
    }
}
