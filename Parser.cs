using System.Text;

public class Parser {
    public record Token {
        public enum Type {
            EOF      = '\0',
            Error    = '!',
            More     = '>',
            QExOpen  = '{',
            QExClose = '}',
            SExOpen  = '(',
            SExClose = ')',
            Symbol   = '$',
            Number   = '#',
            String   = '@',
            Comment  = ';'
        }

        public Type type;
        public string? str;
        public string? raw;
        public Num? num;
        public string parens = "";
    }

    // The shorthand: a prefix right before ( or { is the function it names, the call's first item: ?(c a b) is
    // (if c a b), ?{c a b} the body {if c a b}
    private static readonly Dictionary<char,string> LParenPrefixes =
        new Dictionary<char, string> {
            {'?', "if"},  {'=', "set"},    {':', "def"},    {'#', "hash-create"},
            {'@', "fn"},  {'.', "unpack"}, {'~', "format"},
        };

    public static IEnumerable<Token> Tokenize(TextReader stream, string startingParens = "") {
        int peekInt() => stream.Peek();
        char peek() => (char)peekInt();
        int nextInt() => stream.Read();
        char next() => (char)nextInt();

        bool isNumberSeparator(int c, char? numBase = null) =>
            numBase != null && (c == '/' || c == '.' || numBase switch {
                'd' or 'D'=> c == '+' || 
                             c == '-' ||
                             c == 'e' ||
                             c == 'E',
                _         => false
            });

        // (a character is a byte: a space is ASCII's, a control character one below 32, or 127)
        bool isSpace(int c) => c == ' ' || (c >= '\t' && c <= '\r');
        bool isControl(int c) => c < ' ' || c == 127;
        bool isOpener(int c) => c == '(' || c == '{' || c == '[';
        bool isTerminator(int c, char? numBase = null) =>
            c == -1 || isSpace(c) || c == ')' || c == '}' || c == ']' || isOpener(c) || isNumberSeparator(c, numBase);

        // A name (or a number) right against an opening bracket: an error, as f(x) or <(h k) isn't danlang
        Token? glued(string name) =>
            isOpener(peekInt()) ? error($"'{name}' touches '{peek()}': put a space between them", name) : null;

        // A byte that's never in a word, as it's never outside a string: a control character, or one past ASCII (its
        // error; null for any other)
        string? notInWord(int c) =>
            c < 0 ? null : isControl(c) ? "Control sequences not allowed" : c > 127 ? "Non-ASCII character outside of string literal" : null;

        Token error(String reason, String? raw = null) => new Token { type = Token.Type.Error,  str = reason, raw = raw };
        Token symbol(String sym,   String? raw = null) => new Token { type = Token.Type.Symbol, str = sym,    raw = raw ?? sym };
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

                    if (isSpace(c)) {
                        if ((c == '\n' || c == '\r') && quoteCount == 1)
                            return error("Newlines are not allowed in regular strings", raw.ToString());
                        if (c == '\r' && peek() == '\n') continue;  // a line's end is LF, whatever the file's
                        str.Append(c);
                    } else if (isControl(c)) {
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
                    case '\\' when quoteCount == 1:
                        // escape sequences, in a plain string (a here string's text is as it is)
                        var x = nextInt();
                        if (x == -1) return error("Incomplete string literal", raw.ToString());
                        raw.Append((char)x);
                        switch ((char)x) {
                        case 'n':  str.Append('\n'); break;
                        case 't':  str.Append('\t'); break;
                        case 'r':  str.Append('\r'); break;
                        case '0':  str.Append('\0'); break;
                        case 'a':  str.Append('\a'); break;
                        case 'b':  str.Append('\b'); break;
                        case 'f':  str.Append('\f'); break;
                        case 'v':  str.Append('\v'); break;
                        case 'e':  str.Append('\x1b'); break;
                        case '\\': str.Append('\\'); break;
                        case '"':  str.Append('"'); break;
                        case 'x': {
                            var hex = "";
                            while (hex.Length < 2 && Uri.IsHexDigit(peek())) {
                                hex += next();
                            }
                            if (hex.Length == 0) return error("\\x needs a hex digit", raw.Append(hex).ToString());
                            raw.Append(hex);
                            str.Append((char)Convert.ToInt32(hex, 16));
                            break;
                        }
                        default:
                            return error($"Unknown escape sequence \\{(char)x}", raw.ToString());
                        }
                        break;
                    default:
                        str.Append(c);
                        break;
                    }
                }
            } else {
                return new Token { type = Token.Type.String, str = str.ToString(), raw = raw.ToString() };
            }

            // the text's end in a here string: more is wanted, as for an open bracket (at the REPL, another line)
            if (quoteCount > 1) return new Token { type = Token.Type.More, parens = new string('"', quoteCount), raw = raw.ToString() };
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
            if ((peek() == '(' || peek() == '{') && LParenPrefixes.Keys.Contains(prefix)) {
                var raw = $"{prefix}(";
                yield return lexParen();
                yield return symbol(LParenPrefixes[prefix], raw);
            }
            else if (prefix == '$' && isOpener(peekInt())) {
                yield return error($"'${peek()}' isn't danlang: $name is the environment's variable", "$");
            }
            else if (prefix == '$' && !isTerminator(peekInt())) {
                // $name: the environment's variable, (env "name"), its name's case kept
                var name = new StringBuilder();
                while (!isTerminator(peekInt())) {
                    if (notInWord(peekInt()) is string m) {
                        next();
                        yield return error(m);
                        yield break;
                    }
                    name.Append(next());
                }
                yield return paren('(', $"${name}");
                yield return symbol("env", "");
                yield return new Token { type = Token.Type.String, str = name.ToString(), raw = "" };
                yield return paren(')', "");
                var g = glued($"${name}");
                if (g != null) yield return g;
            }
            else if (prefix == '\\' && "()[]{}".Contains(peek())) {
                // a bracket as a character: \( \] ...
                yield return symbol($"\\{next()}");
            }
            else {
                var str = new StringBuilder();
                str.Append(prefix);

                while (!isTerminator(peekInt())) {
                    if (notInWord(peekInt()) is string m) {
                        next();
                        yield return error(m);
                        yield break;
                    }
                    str.Append(next());
                }

                // a character: \x, or \name, a name it knows
                if (prefix == '\\' && LVal.CharOf(str.ToString().Substring(1)) == null)
                    yield return error(str.Length == 1 ? "A character needs a name" : $"Unknown character name {str}", str.ToString());
                else yield return symbol(str.ToString());
                var g = glued(str.ToString());
                if (g != null) yield return g;
            }
        }

        IEnumerable<Token> lexNumber() {
            var sb = new StringBuilder();
            while (peekInt() != -1 && !isSpace(peekInt()) && !"})]".Contains(peek())) {
                if (isOpener(peekInt())) {
                    // a digit set of its own, #[...] (after #, and < > = + -): to its ], part of the number
                    if (peek() == '[' && System.Text.RegularExpressions.Regex.IsMatch(sb.ToString(), @"^[+-]?#[<>]?=?[+-]?$")) {
                        while (peekInt() != -1 && peek() != ']') {
                            if (notInWord(peekInt()) is string m) {
                                next();
                                return new[] {error(m)};
                            }
                            sb.Append(next());
                        }
                        if (peekInt() != -1) sb.Append(next());
                        continue;
                    }
                    break;
                }
                if (notInWord(peekInt()) is string bad) {
                    next();
                    return new[] {error(bad)};
                }
                var c = next();
                // (# is a prefix only as a word's first: +#(a) is the word +# against a bracket)
                if (c == '#' && sb.Length == 0 && (peek() == '(' || peek() == '{')) return lexSymbol(c);
                sb.Append(c);
            }
            var g = glued(sb.ToString());
            Num? num;
            try {
                num = NumberParser.ParseString(sb.ToString());
            }
            catch (DivideByZeroException) {
                return new[] {error($"Division by zero: {sb}", sb.ToString())};
            }
            var t = num == null ? symbol(sb.ToString()) : new Token {type = Token.Type.Number, num = num, raw = sb.ToString()};
            return g == null ? new[] {t} : new[] {t, g};
        }

        IEnumerable<Token> Lex() {
            var i = 0;
            while ((i = peekInt()) != -1) {
                var c = (char)i;
                if (isSpace(c)) next();
                else if (isControl(c)) {
                    yield return error("Control sequences not allowed");
                    next();
                }
                else if (c > 127) {
                    yield return error("Non-ASCII character outside of string literal");
                    next();
                }
                else if (c ==';') yield return lexComment();
                else if (c =='(' || c == ')' || c == '{' || c == '}') yield return lexParen();
                else if (c == '[') {
                    // [a b c]: a list of the values, (list a b c)
                    next();
                    yield return paren('(', "[");
                    yield return symbol("list", "");
                }
                else if (c == ']') {
                    next();
                    yield return paren(')', "]");
                }
                else if (((c >= '0' && c <= '9') || c == '+' || c == '-' || c == '#'))
                    foreach (var t in lexNumber()) yield return t;
                else if (c == '"') yield return lexString();
                else foreach (var t in lexSymbol()) yield return t;
            }
        }

        var parens = startingParens;
        foreach (var t in Lex()) {
            if (t.type == Token.Type.More) {
                // (a here string open at the end: its quotes the last closer wanted)
                parens += t.parens;
                break;
            }
            if (t.type == Token.Type.SExOpen) parens += t.raw == "[" ? ']' : ')';
            if (t.type == Token.Type.QExOpen) parens += '}';
            if (t.type == Token.Type.SExClose) {
                var closer = t.raw == "]" ? ']' : ')';
                if (parens.EndsWith(closer)) parens = parens.Remove(parens.Length - 1);
                else yield return error(closer == ']' ? $"Closed a list without opening: {parens}" : $"Closed a SExpr without opening: {parens}");
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
