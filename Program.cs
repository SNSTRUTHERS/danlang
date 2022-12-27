using System.Numerics;
using System.Text;

class Parser {
    public record Token {
        public enum Type {
            Error,
            EOF,
            LParen,
            RParen,
            Symbol,
            Number,
            String
        }

        public Type type;

        public string? str;

        public string? raw;

        public BigInteger? num;
    }

    private const string NumChars = "0123456789abcdefghijklmnopqrstuvwxyz";
    private static readonly Dictionary<char, int> NumBases =
        new Dictionary<char, int> {{'b', 2}, {'d', 10}, {'o', 8}, {'q', 4}, {'t', 3}, {'x', 16}, {'z', 36}};
    private static readonly Dictionary<char,string> SpecialPrefixes = // @"'#,>:./~";
        new Dictionary<char, string> {
            {'\'', "quote"}, {'#', "begin"}, {',', "list"}, {'>', ""},
            {':', ""}, {'.', ""}, {'/', "rational"}, {'~', ""}
        };

    public static IEnumerable<Token> Tokenize(TextReader stream) {
        Token? reserve = null;

        char peek() => (char)stream.Peek();
        char next() => (char)stream.Read();

        bool isTerminator(char c) => Char.IsWhiteSpace(c) || c == ')' || c == -1;

        Token error(String reason) => new Token { type = Token.Type.Error, str = reason };
        Token symbol(String sym, String? raw = null) => new Token { type = Token.Type.Symbol, str = sym, raw = raw ?? sym };
        Token paren(char p) => 
            new Token { 
                raw = p.ToString(),
                str = p.ToString(),
                type = (p == '(' ? Token.Type.LParen : Token.Type.RParen)
            };

        Token lexString() {
            var str = new StringBuilder();
            var raw = new StringBuilder("\"");

            for (char c; (c = next()) != -1;) {
                raw.Append(c);

                if (Char.IsWhiteSpace(c)) {
                    str.Append(c);
                } else if (Char.IsControl(c)) {
                    return error("Control sequences not allowed");
                } else switch (c) {
                case '"':
                    return new Token { type = Token.Type.String, str = str.ToString(), raw = raw.ToString() };
                case '\\':
                    // TODO: escape sequences
                    break;
                default:
                    str.Append(c);
                    break;
                }
            }

            return error("Incomplete string literal");
        }

        Token lexSymbol(char prefix) {
            if (peek() == '(' && SpecialPrefixes.Keys.Contains(prefix)) {
                next();
                reserve = symbol(SpecialPrefixes[prefix], Char.ToString(prefix));
                return new Token { type = Token.Type.LParen };
            }

            var str = new StringBuilder();
            str.Append(prefix);

            while (!isTerminator(peek())) {
                str.Append(next());
            }

            return symbol(str.ToString());
        }

        Token lexNumber(char prefix) {
            int numBase = 10;
            var nextChar = prefix;
            var raw = new StringBuilder();

            int digitVal() => NumChars.IndexOf(char.ToLower(nextChar));
            
            bool isNumDigit() {
                if (nextChar == '_') return true;
                var val = digitVal();
                return val >= 0 && val < Math.Abs(numBase);
            }

            void advance() { raw.Append(nextChar); nextChar = next();}

            var mult = prefix == '-' ? -1 : 1;

            if (mult < 0 || nextChar == '+') {
                if (!Char.IsDigit(peek()))
                    return lexSymbol(nextChar);

                advance();
            }

            // check for base
            if (nextChar == '0') {
                advance();
                var lc = Char.ToLower(nextChar);
                if (NumBases.ContainsKey(lc)) {
                    // set numBase, which could be negative if mult == -1
                    numBase = mult * NumBases[lc];

                    // consume base
                    advance();

                    // reset mult since negative bases don't use minus signs
                    mult = 1;
                }
            }

            BigInteger scratch = 0;
            while (isNumDigit()) {
                if (nextChar != '_') {
                    scratch = scratch * numBase + digitVal();
                }
                advance();
            }

            if (!isTerminator(nextChar))
                return error($"Invalid number terminator ({nextChar}) for base ({numBase})");

            scratch *= mult;

            return new Token { type = Token.Type.Number, num = scratch, raw = raw.ToString() };
        }

        char c;
        while ((c = next()) != -1) {
            if (Char.IsWhiteSpace(c)) {
                continue;
            } else if (Char.IsControl(c)) {
                yield return error("Control sequences not allowed");
            } else if (!Char.IsAscii(c)) {
                yield return error("Non-ASCII character outside of string literal");
            } else yield return c switch {
                '(' or ')' => paren(c),
                var x when (x >= '0' && x <= '9') || (x == '+' || x == '-') => lexNumber(x),
                '"' => lexString(),
                var x => lexSymbol(x)
            };

            if (reserve != null) {
                yield return reserve;
                reserve = null;
            }
        }

        yield return new Token { type = Token.Type.EOF };
    }
}

public class Program {

    public static int Main(string[] args) {
        if (args.Contains("-t")) {
            Tests.TestNumbers();
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
