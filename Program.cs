public class Program {
    public static readonly int MAJOR_VERSION = 0;
    public static readonly int MINOR_VERSION = 1;

    private static void DoTokenize(TextReader reader) {
        foreach (var tok in Parser.Tokenize(reader)) {
            Console.WriteLine(tok);
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
