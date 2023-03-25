using System.Text;

public class Parser {
    public record Token {
        public enum Type {
            EOF = '\0',
            Error = '!',
            More = '>',
            QExOpen = '{',
            QExClose = '}',
            SExOpen = '(',
            SExClose = ')',
            Symbol = '$',
            Number = '#',
            String = '@',
            Comment = ';'
        }

        public Type type;

        public string? str;

        public string? raw;

        public Num? num;

        public string parens = "";
    }

    private static readonly Dictionary<char,string> LParenPrefixes = // @"'#,>:./~*=";
        new Dictionary<char, string> {
            {'\'', "list"}, {'^', "head"}, {'$', "tail"}, {'.', "apply"}, {'|', "join"}, {'#', "hash-create"},
            {'~', "format"}, {'=', "set"}, {':', "def"}, {'@', "fn"}, {'!', "eval"}, {'?', "if"}
        };

    public static IEnumerable<Token> Tokenize(TextReader stream, string startingParens = "") {
        int peekInt() => stream.Peek();
        char peek() => (char)peekInt();
        int nextInt() => stream.Read();
        char next() => (char)nextInt();

        bool isNumberSeparator(int c, char? numBase = null) =>
            numBase != null && (c == '/' || c == '.' || numBase switch {
                'd' or 'D'=> c == '+' || c == '-' || c == '.' || c == 'e' || c == 'E',
                _ => false
            });

        bool isTerminator(int c, char? numBase = null) => c == -1 || Char.IsWhiteSpace((char)c) || c == ')' || c == '}' || isNumberSeparator(c, numBase);

        Token error(String reason, String? raw = null) => new Token { type = Token.Type.Error, str = reason, raw = raw };
        Token symbol(String sym, String? raw = null) => new Token { type = Token.Type.Symbol, str = sym, raw = raw ?? sym };
        Token paren(char p, string? str = null) => 
            new Token { 
                raw = str ?? p.ToString(),
                str = p.ToString(),
                type = p switch {
                    '(' => Token.Type.SExOpen,
                    ')' => Token.Type.SExClose,
                    '{' => Token.Type.QExOpen,
                    '}' => Token.Type.QExClose,
                    _   => Token.Type.Error
                }
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
                        // str.Append("\\");
                        // break;
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

        IEnumerable<Token> lexSymbol(char? pref = null) {
            char prefix = pref ?? next();
            if (peek() == '(' && LParenPrefixes.Keys.Contains(prefix)) {
                var raw = $"{prefix}(";
                yield return lexParen();
                yield return symbol(LParenPrefixes[prefix], raw);
            }
            else {
                var str = new StringBuilder();
                str.Append(prefix);

                while (!isTerminator(peekInt())) {
                    str.Append(next());
                }

                yield return symbol(str.ToString());
            }
        }

        IEnumerable<Token> lexNumber() {
            var sb = new StringBuilder();
            while (peekInt() != -1 && !Char.IsWhiteSpace(peek()) && !"})".Contains(peek())) {
                var c = next();
                if (c == '#' && peek() == '(') return lexSymbol(c);
                sb.Append(c);
            }
            var num = NumberParser.ParseString(sb.ToString());
            if (num == null) return new [] {symbol(sb.ToString())};
            return new[] {new Token {type = Token.Type.Number, num = num, raw = sb.ToString()}};
        }

        IEnumerable<Token> Lex() {
            var i = 0;
            while ((i = peekInt()) != -1) {
                var c = (char)i;
                if (Char.IsWhiteSpace(c)) next();
                else if (Char.IsControl(c)) {
                    yield return error("Control sequences not allowed");
                    next();
                }
                else if (!Char.IsAscii(c)) {
                    yield return error("Non-ASCII character outside of string literal");
                    next();
                }
                else if (c ==';') yield return lexComment();
                else if (c =='(' || c == ')' || c == '{' || c == '}') yield return lexParen();
                else if (((c >= '0' && c <= '9') || c == '+' || c == '-' || c == '#'))
                    foreach (var t in lexNumber()) yield return t;
                else if (c == '"') yield return lexString();
                else foreach (var t in lexSymbol()) yield return t;
            }
        }

        var parens = startingParens;
        foreach (var t in Lex()) {
            if (t.type == Token.Type.SExOpen) parens += ')';
            if (t.type == Token.Type.QExOpen) parens += '}';
            if (t.type == Token.Type.SExClose) {
                if (parens.EndsWith(')')) parens = parens.Remove(parens.Length - 1);
                else yield return error($"Closed a SExpr without opening: {parens}");
            }
            if (t.type == Token.Type.QExClose) {
                if (parens.EndsWith('}')) parens = parens.Remove(parens.Length - 1);
                else yield return error($"Closed a QExpr without opening: {parens}");
            }
            yield return t;
        }

        if (parens.Length > 0) yield return new Token { type = Token.Type.More, parens = parens };
        else yield return new Token { type = Token.Type.EOF };
    }
}
