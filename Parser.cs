using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
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

        public Num? num;

        public IntAcc? acc;
    }

    private static readonly Dictionary<char,string> LParenPrefixes = // @"'#,>:./~";
        new Dictionary<char, string> {
            {'#', "block"}, {',', "list"}, {'~', "format"},
            {':', "fn"}, {'.', "apply"}, {'/', "rational"}, {'c', "complex"}
        };

    public static IEnumerable<Token> Tokenize(TextReader stream) {
        int peekInt() => stream.Peek();
        char peek() => (char)peekInt();
        int nextInt() => stream.Read();
        char next() => (char)nextInt();

        bool isNumberSeparator(int c, char? numBase = null) =>
            numBase != null && (c == '/' || c == '.'|| numBase switch {
                'd' or 'D'=> c == '+' || c == '-' || c == '.' || c == 'e' || c == 'E',
                _ => false
            });

        bool isTerminator(int c, char? numBase = null) => c == -1 || Char.IsWhiteSpace((char)c) || c == ')' || isNumberSeparator(c, numBase);

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

        IEnumerable<Token> lexSymbol(char? pref = null) {
            char prefix = pref ?? next();
            if (peek() == '(' && LParenPrefixes.Keys.Contains(prefix)) {
                next();
                var raw = $"{prefix}(";
                yield return paren('(', raw);
                yield return symbol(LParenPrefixes[prefix], raw);
            }
            else {
                var str = new StringBuilder();
                str.Append(prefix);

                while (!isTerminator(peek())) {
                    str.Append(next());
                }

                yield return symbol(str.ToString());
            }
        }

        Token lexIntegerOrFixed(IntAcc? acc = null) {
            var nextChar = peekInt();
            var raw = new StringBuilder();

            void advance() { raw.Append(next()); nextChar = peekInt(); }

            var mult = nextChar == '-' ? -1 : 1;

            if (mult < 0 || nextChar == '+') {
                var prefix = (char)nextChar;
                advance();
                if (!Char.IsDigit((char)nextChar)) {
                    var str = new StringBuilder();
                    str.Append(prefix);

                    while (!isTerminator(peek())) {
                        str.Append(next());
                    }

                    return symbol(str.ToString());
                }
            }

            // check for base
            if (nextChar == '0') {
                // consume leading zero always
                advance();

                // see if there is a base modifier (+-), a balanced indicator (%), and a direction identifier (<>)
                int? baseMult = null;
                bool? le = null;
                string? charset = null;
                bool? comp = null;
                while (true) {
                    if (nextChar == '[') {
                        var charsetSb = new StringBuilder();

                        // consume the '['
                        advance();
                        while (nextChar != ']') {
                            charsetSb.Append((char)nextChar);
                            advance();
                        }

                        // consume the ']';
                        advance();
                        charset = charsetSb.ToString();
                    }
                    if ("+-".Contains((char)nextChar)) {
                        if (baseMult == null) {
                            baseMult = nextChar ==  '-' ? -1 : 1;
                            advance();
                        }
                        else return error("Duplicate base multiplier in number prefix", raw.ToString());
                    }
                    else if ("<>".Contains((char)nextChar)) {
                        if (le == null) {
                            le = nextChar ==  '>';
                            advance();
                        }
                        else return error("Duplicate direction indicator in number prefix", raw.ToString());
                    }
                    else if (nextChar == '~') {
                        if (comp == null) {
                            comp = true;
                            advance();
                        }
                        else return error("Duplicate complement indicator in number prefix", raw.ToString());
                    } else break;
                }

                var lc = Char.ToLower((char)nextChar);
                if (IntAcc.NumBases.ContainsKey(lc)) {
                    // consume base
                    advance();

                    // get the new accumulator based on the numBase and sign
                    acc = IntAcc.Create(lc, baseMult ?? 1, le, comp, charset);
                }
                // else if IsDigit(nextChar) then is valid (possibly) complex of the for 0+xi or 0-xi
                else if (baseMult != null || le != null) return error("Invalid character(s) in integer/fixed number sequence", raw.ToString());
            }
            else if (acc != null) acc.Reset();

            acc = acc ?? IntAcc.Create();

            while (acc.Add(nextChar)) {
                advance();
            }

            if (!isTerminator(nextChar, acc.NumberBase))
                return error($"Invalid number terminator ({(char)nextChar}) for base ({acc.NumberBase})", raw.Append((char)nextChar).ToString());
            else {
                var str = raw.ToString();
                return new Token { type = Token.Type.Number, num = acc.Num * mult, acc = acc, str = str, raw = str };
            }
        }

        Token lexNumber() {
            var tokens = new List<Tuple<Token, int>>();
            Tuple<Token, int>? t = null;
            do {
                var token = lexIntegerOrFixed(t?.Item1.acc);
                t = new (token, peekInt());
                tokens.Add(t);
                if (isNumberSeparator(t.Item2, t.Item1.acc?.NumberBase)) next();
            } while (t.Item1.type == Token.Type.Number && isNumberSeparator(t.Item2, t.Item1.acc!.NumberBase));
            
            t = tokens[0];
            if (tokens.Count == 1) return t.Item1;
            else {
                var raw = new StringBuilder();
                foreach (var tt in tokens) {
                    raw.Append(tt.Item1.raw);
                    if (isNumberSeparator(tt.Item2, tt.Item1.acc!.NumberBase)) raw.Append((char)tt.Item2);
                }

                var i = 0;
                Num? n = null;
                while (i < tokens.Count) {
                    t = tokens[i];
                    if (t.Item1.type != Token.Type.Number) {
                        return t.Item1;
                    }
                    
                    if (!isNumberSeparator(t.Item2, t.Item1.acc!.NumberBase)) {
                        if (i == 0) {
                            n = t.Item1.num;
                            break;
                        }
                        else {

                        }
                    }
                    
                    if (i < tokens.Count - 1) {
                        if ("+-".Contains((char)t.Item2)) { // complex
                            if (n == null) {
                                
                            }
                        }
                        else if (t.Item2 == '/') { // Rational
                            n = new Rat((t.Item1.num as Int)!.num, (tokens[++i].Item1.num as Int)!.num);
                            ++i;
                        }
                        else {
                            // error, missing denominator
                            return error("Missing denominator in number", raw.ToString());
                        }
                    }
                }
                return new Token {type = Token.Type.Number, num = n, raw = raw.ToString()};
            }
        }

        IEnumerable<Token> Lex(bool isQuote = false) {
            var i = 0;
            var listDepth = 0;
            while ((i = peekInt()) != -1) {
                var c = (char)i;
                if (Char.IsWhiteSpace(c)) {
                    next();
                }
                else if (Char.IsControl(c)) {
                    yield return error("Control sequences not allowed");
                    next();
                }
                else if (!Char.IsAscii(c)) {
                    yield return error("Non-ASCII character outside of string literal");
                    next();
                }
                else if (c ==';')
                    yield return lexComment();
                else if (c =='(' || c == ')') {
                    yield return lexParen();
                    if (c == '(') ++listDepth;
                    else --listDepth;
                    if (listDepth == 0 && isQuote) break;
                }
                else if ((!isQuote || listDepth > 0) && ((c >= '0' && c <= '9') || c == '+' || c == '-'))
                    yield return lexNumber();
                else if ((!isQuote || listDepth > 0) && c == '"')
                    yield return lexString();
                else if (c == '\'') {
                    next();
                    yield return paren('(', "'");
                    yield return symbol("quote", "'");
                    foreach (var t1 in Lex(true)) yield return t1;
                    yield return paren(')', "'");
                }
                else  {
                    foreach (var t in lexSymbol()) yield return t;
                    if (isQuote && listDepth == 0) break;
                }
            }
        }

        foreach (var t in Lex()) yield return t;

        yield return new Token { type = Token.Type.EOF };
    }
}
