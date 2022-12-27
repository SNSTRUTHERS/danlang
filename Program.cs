using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

class Parser {
    [StructLayout(LayoutKind.Explicit)]
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

        [FieldOffset(0)]
        public Type type;

        [FieldOffset(8)]
        public string? str;

        [FieldOffset(16)]
        public string? raw;
    }

    public static IEnumerable<Token> Tokenize(TextReader stream) {
        Token? reserve = null;
        var rx = new Regex(@"['#,>:./~]");

        Token error(String reason) {
            return new Token { type = Token.Type.Error, str = reason };
        }
        Token symbol(String sym, String? raw = null) {
            return new Token { type = Token.Type.Symbol, str = sym, raw = raw ?? sym };
        }

        Token lexString() {
            var str = new StringBuilder();
            var raw = new StringBuilder();
            raw.Append('"');

            for (char c; (c = (char)stream.Peek()) != -1;) {
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
            if (stream.Peek() == '(' && rx.IsMatch(Char.ToString(prefix))) {
                stream.Read();
                reserve = symbol(prefix switch {
                    '\'' => "quote",
                    '#' => "begin",
                    ',' => "list",
                    ':' => "fn",
                    '>' => "apply",
                    '~' => "complex",
                    _ => "!?!?"
                }, Char.ToString(prefix));
                return new Token { type = Token.Type.LParen };
            }

            var str = new StringBuilder();
            str.Append(prefix);

            for (char c; (c = (char)stream.Peek()) != -1 && !Char.IsWhiteSpace(c) && c != ')';) {
                str.Append((char)stream.Read());
            }

            return symbol(str.ToString());
        }

        Token lexNumber(char c) {
            if ((c == '-' || c == '+') && !Char.IsDigit((char)stream.Peek()))
                return lexSymbol(c);

            // TODO: number parsing
            return new Token { type = Token.Type.Number };
        }

        for (char c; (c = (char)stream.Read()) != -1;) {
            if (Char.IsWhiteSpace(c)) {
                continue;
            } else if (Char.IsControl(c)) {
                yield return error("Control sequences not allowed");
            } else if (!Char.IsAscii(c)) {
                yield return error("Non-ASCII character outside of string literal");
            } else yield return c switch {
                '(' => new Token { type = Token.Type.LParen },
                ')' => new Token { type = Token.Type.RParen },
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
        var reader = args.Length > 1 ? new StreamReader(args[1]) : Console.In;

        foreach (var tok in Parser.Tokenize(reader)) {
            Console.WriteLine(tok);
        }

        if (reader != Console.In) reader.Dispose();
        return 0;
    }
}
