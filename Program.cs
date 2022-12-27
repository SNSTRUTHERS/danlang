﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;

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
    }

    public static IEnumerable<Token> Tokenize(TextReader stream) {
        Token? reserve = null;
        var rx = new Regex(@"['#,>:./~]");

        char peek() {
            return (char)stream.Peek();
        }
        char next() {
            return (char)stream.Read();
        }

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

            for (char c; (c = peek()) != -1;) {
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
            if (peek() == '(' && rx.IsMatch(Char.ToString(prefix))) {
                next();
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

            for (char c; (c = peek()) != -1 && !Char.IsWhiteSpace(c) && c != ')';) {
                str.Append(next());
            }

            return symbol(str.ToString());
        }

        Token lexNumber(char c) {
            if ((c == '-' || c == '+') && !Char.IsDigit(peek()))
                return lexSymbol(c);

            // TODO: number parsing
            return new Token { type = Token.Type.Number };
        }

        for (char c; (c = next()) != -1;) {
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
