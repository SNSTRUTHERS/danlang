public class Program {

    public static int Main(string[] args) {
        if (args.Contains("-t")) {
            Tests.TestNumbers();
            Tests.TestTokens();
            return 0;
        }

        var reader = args.Length > 1 ? new StreamReader(args[1]) : Console.In;

        foreach (var tok in Parser.Tokenize(reader)) {
            Console.WriteLine(tok);
        }

        if (reader != Console.In) reader.Dispose();
        return 0;
    }
}
