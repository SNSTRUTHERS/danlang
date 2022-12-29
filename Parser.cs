using System.Numerics;
using System.Text;

public class Parser {
    public record Token {
        public enum Type {
            Error,
            EOF,
            LParen,
            RParen,
            Symbol,
            Number,
            String,
            Comment
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
            {'\'', "quote"}, {'#', "begin"}, {',', "list"}, {'>', "send"},
            {':', "fn"}, {'.', "apply"}, {'/', "ratio"}, {'~', "complex"}
        };

    public static IEnumerable<Token> Tokenize(TextReader stream) {
        Token? reserve = null;

        int peekInt() => stream.Peek();
        char peek() => (char)peekInt();
        int nextInt() => stream.Read();
        char next() => (char)nextInt();

        bool isTerminator(int c) => c == -1 || Char.IsWhiteSpace((char)c) || c == ')';

        Token error(String reason, String? raw = null) => new Token { type = Token.Type.Error, str = reason, raw = raw };
        Token symbol(String sym, String? raw = null) => new Token { type = Token.Type.Symbol, str = sym, raw = raw ?? sym };
        Token paren(char p, string? str = null) => 
            new Token { 
                raw = str ?? p.ToString(),
                str = p.ToString(),
                type = (p == '(' ? Token.Type.LParen : Token.Type.RParen)
            };

        Token lexParen() => paren(next());

        Token lexString() {
            var str = new StringBuilder();
            var raw = new StringBuilder();
            var quoteCount = 0;
            var trailingQuotes = 0;
            while (peek() == '"') {
                ++quoteCount;
                raw.Append(next());
            }

            if (quoteCount % 2 == 1) { // not matched pairs
                for (int i; (i = nextInt()) != -1;) {
                    var c = (char)i;
                    raw.Append(c);

                    if (Char.IsWhiteSpace(c)) {
                        if ((c == '\n' || c == '\r') && quoteCount == 1)
                            return error("Newlines are not allowed in regular strings", raw.ToString());
                        str.Append(c);
                    } else if (Char.IsControl(c)) {
                        return error("Control sequences not allowed", raw.ToString());
                    } else switch (c) {
                    case '"':
                        while (++trailingQuotes < quoteCount && peek() == '"')
                            raw.Append(next());

                        if (trailingQuotes == quoteCount)
                            return new Token { type = Token.Type.String, str = str.ToString(), raw = raw.ToString() };
                        
                        // not enough trailing quotes found, so keep looking and append the quotes to the end of str
                        str.Append('"', trailingQuotes);
                        trailingQuotes = 0;
                        break;
                    case '\\':
                        // TODO: escape sequences
                        break;
                    default:
                        str.Append(c);
                        break;
                    }
                }
            } else {
                return new Token { type = Token.Type.String, str = str.ToString(), raw = raw.ToString() };
            }

            return error("Incomplete string literal", raw.ToString());
        }

        Token lexComment() {
            var raw = new StringBuilder();
            var str = new StringBuilder();
            raw.Append(next());
            int i;
            while ((i = peekInt()) != '\n' && i != '\r' && i != -1) {
                var c = next();
                str.Append(c);
                raw.Append(c);
            }
            return new Token { type = Token.Type.Comment, str = str.ToString(), raw = raw.ToString()};
        }

        Token lexSymbol(char? pref = null) {
            char prefix = pref ?? next();
            if (peek() == '(' && SpecialPrefixes.Keys.Contains(prefix)) {
                next();
                var raw = $"{prefix}(";
                reserve = symbol(SpecialPrefixes[prefix], raw);
                return paren('(', raw);
            }

            var str = new StringBuilder();
            str.Append(prefix);

            while (!isTerminator(peek())) {
                str.Append(next());
            }

            return symbol(str.ToString());
        }

        Token lexNumber() {
            int numBase = 10;
            var nextChar = peekInt();
            var raw = new StringBuilder();

            int digitVal() => NumChars.IndexOf(Char.ToLower((char)nextChar));
            
            bool isNumDigit() {
                if (nextChar == '_') return true;
                var val = digitVal();
                return val >= 0 && val < Math.Abs(numBase);
            }

            void advance() { raw.Append(next()); nextChar = peekInt(); }

            var mult = nextChar == '-' ? -1 : 1;

            if (mult < 0 || nextChar == '+') {
                var prefix = (char)nextChar;
                advance();
                if (!Char.IsDigit((char)nextChar))
                    return lexSymbol(prefix);
            }

            // check for base
            if (nextChar == '0') {
                // consume leading zero always
                advance();
                var lc = Char.ToLower((char)nextChar);
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
                return error($"Invalid number terminator ({(char)nextChar}) for base ({numBase})", raw.Append((char)nextChar).ToString());

            scratch *= mult;

            return new Token { type = Token.Type.Number, num = scratch, raw = raw.ToString() };
        }

        var i = 0;
        while ((i = peekInt()) != -1) {
            var c = (char)i;
            if (Char.IsWhiteSpace(c)) {
                next();
            } else if (Char.IsControl(c)) {
                yield return error("Control sequences not allowed");
                next();
            } else if (!Char.IsAscii(c)) {
                yield return error("Non-ASCII character outside of string literal");
                next();
            } else yield return c switch {
                ';' => lexComment(),
                '(' or ')' => lexParen(),
                (>= '0' and <= '9') or '+' or '-' => lexNumber(),
                '"' => lexString(),
                _ => lexSymbol()
            };

            if (reserve != null) {
                yield return reserve;
                reserve = null;
            }
        }

        yield return new Token { type = Token.Type.EOF };
    }
}
