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

    public static readonly Dictionary<char, int> NumBases =
        new Dictionary<char, int> {{'b', 2}, {'c', 3}, {'d', 10}, {'e', 5}, {'f', 6}, {'g', 7}, {'k', 27}, {'n', 9}, {'o', 8}, {'q', 4}, {'s', 7}, {'t', 3}, {'v', 5}, {'x', 16}, {'y', 53}, {'z', 36}};

    public class IntAcc {
        private const string NumChars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        private const string BalancedChars = "zyxwvutsrqponmlkjihgfedcba0ABCDEFGHIJKLMNOPQRSTUVWXYZ";   
        private const string BalancedTernQuineryChars = "~=-0+#*";

        private BigInteger? _den = null;
        private BigInteger _factor = 1;
        private int _base = 10;
        private int _zeroIndex = 0;
        protected bool _ltr = false;
        protected bool _balanced = false;

        public static int Log10(BigInteger den) {
            if (den < 0) den = -den;
            int log = 0;
            while (den >= 10) { ++log; den /= 10; }
            return log;
        }

        public string Chars;
        public char NumberBase { get; private set; }
        public int Base { get => _base; }
        public bool IsBalanced { get => _balanced; }
        public BigInteger Val { get; private set; }
        public Num Num { get => (_den == null || _den == 1) ?
            new Int(Val) :
            (NumberBase == 'd' ?
                new Fix(_den > 0 ? Val : -Val, Log10(_den ?? 1)) :
                new Rat(Val, _den ?? 1)); }

        public IntAcc(char b = 'd', int mult = 1, bool ltr = false) {
            b = Char.ToLower(b);
            NumberBase = b;
            _base = NumBases[b];
            _balanced = "cegky".Contains(b);
            Chars = _balanced ?
                (_base > BalancedTernQuineryChars.Length ?
                    BalancedChars :
                    BalancedTernQuineryChars) :
                (b == 'k' ?
                    BalancedChars :
                    NumChars);

            _ltr = ltr;
            _zeroIndex = Chars.IndexOf('0');
            Chars = Chars.Substring(_balanced ? _zeroIndex - (_base / 2) : _zeroIndex, _base);
            _zeroIndex = Chars.IndexOf('0');
            _base = _base * mult;
        }

        public void Reset() {
            _factor = 1; _den = null; Val = 0;
        }

        private char DigitOf(int i) => _balanced ? (char)i : Char.ToUpper((char)i);
        
        private BigInteger DigitVal(int c) => Chars.IndexOf(DigitOf(c)) - Chars.IndexOf('0');

        public char CharFor(int v) => Chars[v + _zeroIndex];
        
        private bool IsPlaceholder(int c) => c == '_' || c == '.';
        
        public bool IsDigit(int i) => i >= 0 && Chars.IndexOf(DigitOf(i)) >= 0;
        public int MinDigitVal { get => -_zeroIndex; }
        public int MaxDigitVal{ get => _balanced ? _zeroIndex : Chars.Length - 1; }
        
        public BigInteger NewVal(int c) => _ltr ? Val + (_factor * DigitVal(c)) : Val * _base + DigitVal(c);

        public bool Add(int c) {
            if (IsPlaceholder(c)) {
                if (c == '.') {
                    if (_den != null) return false; // already consumed a '.'
                    _den = _ltr ? _factor : 1;
                }
                return true;
            }
            if (!IsDigit(c)) return false;
            if (!_ltr && _den != null) _den = _den * _base;
            Val = NewVal(c);
            _factor *= _base;
            return true;
        }

        public override string ToString() => $"IntAcc: base={_base}, chars={Chars}, ltr={_ltr}, balanced={_balanced}";

        public static string ToBase(BigInteger n, string b) {
            b = b.ToLower();

            // validate incoming b
            var reg = new Regex("^0([<>]?[+-]?|[+-]?[<>]?)[bcdefgknoqstvxyz]$", RegexOptions.IgnoreCase);
            if (!reg.IsMatch(b)) throw new FormatException($"Invalid base specifier {b}");

            // normalize b
            b = b.Replace("+", "");
            var isCEG = "ceg".Contains(b.Last());
            if (b.Contains('>') && isCEG) b = b.Replace(">", "");
            if (b.Contains('<') && !isCEG) b = b.Replace("<", "");

            var baseMult = b.Substring(1).Contains('-') ? -1 : 1;
            var ltr = b.Contains('>') || isCEG;

            var acc = new IntAcc(b.Last(), baseMult, ltr);

            // final normalize
            if (b == "0d") b = "";

            var str = new StringBuilder();
            var mult = (acc.Base > 0 && !acc.IsBalanced && n < 0) ? -1 : 1;
            var scratch = n * mult;
            BigInteger p = 1;
            BigInteger min = acc.MinDigitVal;
            BigInteger max = acc.MaxDigitVal;

            while (scratch > max || scratch < min) {
                p = p * acc.Base;
                min = min + (p * ((p > 0) ? acc.MinDigitVal : acc.MaxDigitVal));
                max = max + (p * ((p > 0) ? acc.MaxDigitVal : acc.MinDigitVal));
            }

            while (p != 0) {
                var d = (int)(scratch / p);
                scratch = scratch % p;

                min = min - (p * ((p > 0) ? acc.MinDigitVal : acc.MaxDigitVal));
                max = max - (p * ((p > 0) ? acc.MaxDigitVal : acc.MinDigitVal));

                var m = 0;
                if (scratch > max) m = (scratch > p ? -1 : 1);
                else if (scratch < min) m = (scratch > p ? 1 : -1);

                d = d + m;
                scratch = scratch - (m * p);

                try {
                    if (acc._ltr) str.Insert(0, acc.CharFor(d));
                    else str.Append(acc.CharFor(d));
                } catch {
                    Console.WriteLine($"*** Exception: (d:{d} b:{acc.Base} p:{p} scratch:{scratch} max:{max} min:{min})");
                }

                p = p / acc.Base;
            }

            str.Insert(0, b);
            if (mult < 0) str.Insert(0, '-');
            var ret = str.ToString();

            // TODO: validate that ret parses to n
            return ret;
        }
    }

    private static IntAcc GetAcc(char b = 'd', int mult = 1, bool? ltr = null) =>
        Char.ToLower(b) switch {
            'c' or 'e' or 'g' => new IntAcc(b, mult, ltr ?? true),
            _ => new IntAcc(b, mult, ltr ?? false)
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
                bool? ltr = null;
                while (true) {
                    if ("+-".Contains((char)nextChar)) {
                        if (baseMult == null) {
                            baseMult = nextChar ==  '-' ? -1 : 1;
                            advance();
                        }
                        else return error("Duplicate base multiplier in number prefix", raw.ToString());
                    }
                    else if ("<>".Contains((char)nextChar)) {
                        if (ltr == null) {
                            ltr = nextChar ==  '>';
                            advance();
                        }
                        else return error("Duplicate direction indicator in number prefix", raw.ToString());
                    } else break;
                }

                var lc = Char.ToLower((char)nextChar);
                if (NumBases.ContainsKey(lc)) {
                    // consume base
                    advance();

                    // get the new accumulator based on the numBase and sign
                    acc = GetAcc(lc, baseMult ?? 1, ltr);
                }
                // else if IsDigit(nextChar) then is valid (possibly) complex of the for 0+xi or 0-xi
                else if (baseMult != null || ltr != null) return error("Invalid character(s) in integer/fixed number sequence", raw.ToString());
            }
            else if (acc != null) acc.Reset();

            acc = acc ?? GetAcc();

            while (acc.Add(nextChar)) {
                advance();
            }

            if (!isTerminator(nextChar, acc.NumberBase))
                return error($"Invalid number terminator ({(char)nextChar}) for base ({acc.NumberBase})", raw.Append((char)nextChar).ToString());
            else {
                return new Token { type = Token.Type.Number, num = acc.Num * mult, acc = acc, raw = raw.ToString() };
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

        var i = 0;
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
            else if (c =='(' || c == ')')
                yield return lexParen();
            else if ((c >= '0' && c <= '9') || c == '+' || c == '-')
                yield return lexNumber();
            else if (c == '"')
                yield return lexString();
            else foreach (var t in lexSymbol())
                yield return t;
        }

        yield return new Token { type = Token.Type.EOF };
    }
}
