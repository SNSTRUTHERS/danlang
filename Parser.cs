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

        public char? numBase;
    }

    private const string NumChars = "0123456789abcdefghijklmnopqrstuvwxyz";
    private static readonly Dictionary<char, int> NumBases =
        new Dictionary<char, int> {{'b', 2}, {'c', 3}, {'d', 10}, {'o', 8}, {'q', 4}, {'t', 3}, {'x', 16}, {'z', 36}};

    abstract class IntAcc {
        protected string Chars;
        private int? _dec = null;
        public IntAcc(int numDigits, string chars = NumChars) => Chars = chars.Substring(0, numDigits);
        public BigInteger Val { get; private set; }
        public Num Num { get => _dec == null ? new Int(Val) : new Fix(Val, _dec ?? 0); }
        public abstract BigInteger NewVal(int c);
        public bool Add(int c) {
            if (IsPlaceholder(c)) {
                if (c == '.') {
                    if (_dec != null) return false; // already consumed a '.'
                    _dec = 0;
                }
                return true;
            }
            if (!IsDigit(c)) return false;
            if (_dec != null) ++_dec;
            Val = NewVal(c);
            return true;
        }
        public bool IsDigit(int c) => c >= 0 && Chars.IndexOf((char)c) >= 0;
        public virtual BigInteger DigitVal(int c) => Chars.IndexOf((char)c);
        public virtual bool IsPlaceholder(int c) => c == '_' || c == '.';
    }

    class StandardAcc : IntAcc {
        protected int _b;
        public StandardAcc(int b = 10, string chars = NumChars) : base(Math.Abs(b), chars) => _b = b;
        public override BigInteger NewVal(int c) => Val * _b + DigitVal(c);
    }

    class BalancedTerneryAcc : StandardAcc {
        public BalancedTerneryAcc(int b = 3) : base(b, "-0+") {}
        private BigInteger _factor = 1;
        public override BigInteger NewVal(int c) {
            var v = Val + DigitVal(c);
            _factor *= _b;
            return v;
        }
        public override BigInteger DigitVal(int c) => _factor * (base.DigitVal(c) - 1);
    }

    private static IntAcc GetAcc(char b = 'd', int mult = 1) =>
        Char.ToLower(b) switch {
            'c' => new BalancedTerneryAcc(NumBases[b] * mult),
            _ => new StandardAcc(NumBases[b] * mult)
        };

    private static readonly Dictionary<char,string> LParenPrefixes = // @"'#,>:./~";
        new Dictionary<char, string> {
            {'\'', "quote"}, {'#', "block"}, {',', "list"}, {'~', "format"},
            {':', "fn"}, {'.', "apply"}, {'/', "rational"}, {'c', "complex"}
        };

    public static IEnumerable<Token> Tokenize(TextReader stream) {
        int peekInt() => stream.Peek();
        char peek() => (char)peekInt();
        int nextInt() => stream.Read();
        char next() => (char)nextInt();

        bool isNumberSeparator(int c, char? numBase = null) =>
            numBase != null && (c == '/' || numBase switch {
                'd' or 'D'=> c == '+' || c == '-' || c == '.' || c == 'e' || c == 'E',
                'c' or 'C'=> c == '.',
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
            var isQuote = prefix == '\'';
            if (peek() == '(' && LParenPrefixes.Keys.Contains(prefix)) {
                next();
                var raw = $"{prefix}(";
                yield return paren('(', raw);
                yield return symbol(LParenPrefixes[prefix], raw);
            }
            else {
                if (isQuote) {
                    yield return paren('(', "'");
                    yield return symbol("quote", "'");
                    foreach (var t in lexSymbol()) yield return t;
                    yield return paren(')', "'");
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
        }

        Token lexIntegerOrFixed() {
            char numBase = 'd';
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

            var acc = GetAcc('d');

            // check for base
            if (nextChar == '0') {
                // consume leading zero always
                advance();

                // see if there is a base modifier (+/-)
                int? baseMult = null;
                if ("+-".Contains((char)nextChar)) {
                    baseMult = nextChar ==  '-' ? -1 : 1;
                    advance();
                }

                var lc = Char.ToLower((char)nextChar);
                if (NumBases.ContainsKey(lc)) {
                    // set numBase, which could be negative if mult == -1
                    numBase = lc;

                    // consume base
                    advance();

                    // get the new accumulator based on the numBase and sign
                    acc = GetAcc(numBase, baseMult ?? 1);
                }
                // else if IsDigit(nextChar) then is valid (possibly) complex of the for 0+xi or 0-xi
                else if (baseMult != null) return error("Invalid sign in integer/fixed number sequence", raw.ToString());
            }

            while (acc.Add(nextChar)) {
                advance();
            }

            if (!isTerminator(nextChar, numBase))
                return error($"Invalid number terminator ({(char)nextChar}) for base ({numBase})", raw.Append((char)nextChar).ToString());
            else {
                return new Token { type = Token.Type.Number, num = acc.Num * mult, numBase = numBase, raw = raw.ToString() };
            }
        }

        IEnumerable<Token> lexNumber() {
            var tokens = new List<Tuple<Token, int>>();
            Tuple<Token, int> t;
            do {
                t = new (lexIntegerOrFixed(), peekInt());
                tokens.Add(t);
                if (isNumberSeparator(t.Item2, t.Item1.numBase)) next();
            } while (t.Item1.type == Token.Type.Number && isNumberSeparator(t.Item2, t.Item1.numBase));
            
            var token = tokens[0];
            if (tokens.Count == 1) yield return token.Item1;
            else {
                var raw = new StringBuilder();
                foreach (var tt in tokens) {
                    raw.Append(tt.Item1.raw);
                    if (isNumberSeparator(tt.Item2, tt.Item1.numBase)) raw.Append((char)tt.Item2);
                }

                var i = 0;
                Num? n = null;
                while (i < tokens.Count) {
                    token = tokens[i];
                    if (token.Item1.type != Token.Type.Number) {
                        yield return token.Item1;
                        yield break;
                    }
                    
                    if (!isNumberSeparator(token.Item2, token.Item1.numBase)) {
                        if (i == 0) {
                            n = token.Item1.num;
                            break;
                        }
                        else {

                        }
                    }
                    
                    if (i < tokens.Count - 1) {
                        if ("+-".Contains((char)token.Item2)) { // complex
                            if (n == null) {
                                
                            }
                        }
                        else if (token.Item2 == '/') { // Rational
                            n = new Rat((token.Item1.num as Int)!.num, (tokens[++i].Item1.num as Int)!.num);
                            ++i;
                        }
                        else {
                            // error, missing denominator
                            yield return error("Missing denominator in number", raw.ToString());
                        }
                    }
                }
                yield return new Token {type = Token.Type.Number, num = n, raw = raw.ToString()};
            }
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
            } else if (c ==';') yield return lexComment();
            else if (c =='(' || c == ')') yield return lexParen();
            else if ((c >= '0' && c <= '9') || c == '+' || c == '-') foreach (var t in lexNumber()) yield return t;
            else if (c == '"') yield return lexString();
            else {
                foreach (var t in lexSymbol()) yield return t;
            }
        }

        yield return new Token { type = Token.Type.EOF };
    }
}
