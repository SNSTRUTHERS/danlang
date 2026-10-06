# danlang: the reference

This is danlang's specification: what every implementation (the C# interpreter here, and hylang on the Hydra-16)
must do.  The regression suite, `tests/regress/`, is its executable form: where this text and the suite disagree, one
of them has a bug.  Where the two implementations may differ (the machine's limits, its paths), the last section says
so.

Notation: `(name x y [z])` takes `x` and `y`, and `z` if it's given; `x...` is any number of them.

## 1. Reading

**Text is bytes.**  A character is a byte, 0-255; a string is bytes.  Source files, the console and every file are
read and written as bytes, so UTF-8 text is its bytes (`(len "ÃÂ©")` is 2).  Outside a string or a comment, only ASCII
is allowed.  A space is ASCII's (space, tab, LF, VT, FF, CR); a control character is one below 32, or 127.

| Form | Is |
| --- | --- |
| `; text` | A comment, to the line's end |
| `(a b c)` | An S-expression: evaluated, a call of `a` |
| `{a b c}` | A Q-expression: a list, as it's written (data, or code for `eval`) |
| `[a b c]` | `(list a b c)`: a list of the values; `[]` is NIL |
| `"text"` | A string, with escapes `\n \t \r \0 \a \b \f \v \e \\ \" \xHH` (one or two hex digits); a line's end inside one is an error |
| `"""text"""` | A here string: opened and closed by the same odd number of quotes, its text as it is (escapes not read); a line's end in it is LF |
| `\x` | A character: the character itself, or one of the names below; a bracket too (`\(`, `\]`) |
| `:name` | An atom (its name in lower case) |
| `name` | A symbol (its name in lower case: names are case-insensitive) |
| `T`, `NIL` | True, and the empty list (false) |
| `exit` | The exit value: `(exit n)` ends the program with status `n` |
| `$name` | `(env "name")`: the environment's variable, its name's case kept; NIL if there's none |
| `P(...)`, `P{...}` | A prefix: see below |

**Character names** (case and `-` ignored): `space` `sp`; `tab`; `linefeed` `newline` `lf` `nl`; `return`
`carriagereturn` `cr`; `formfeed` `ff`; `verticaltab` `vtab` `vt` `verticalspace` `vspace` `vs`; `bell`; `backspace`
`bksp`; `null`; `backslash` `bslash` `bs`; `backtick` `btick` `bt`; `quote` `doublequote`; `tick` `singlequote`;
`lparen` `leftparen` `lp`; `rparen` `rightparen` `rp`; `lbrace` `leftcurly` `lcurly` `lc`; `rbrace` `rightcurly`
`rcurly` `rc`; `lbracket` `leftbracket` `lb`; `rbracket` `rightbracket` `rb`; `slash` `sl`; `tilde`; `bang`
`warning` `exclaim` `exclamation` `exclamationpoint`; `at`; `poundsign` `pound` `hash`; `dollarsign` `dollar`
`dollars` `ds`; `percentsign` `percent` `mod` `modulo` `modulus` `pc`; `caret` `uparrow`; `amp` `and` `ampersand`;
`pipe` `or` `vbar` `verticalbar` `vb`; `star` `splat` `mult` `st`; `gt` `greater` `greaterthan`; `lt` `less`
`lessthan`; `comma`; `colon`; `semicolon` `semi` `sc`; `dot` `period` `point`; `qmark` `question` `questionmark`
`qm`; `underbar` `ub` `underscore`; `minus` `hyphen` `dash` `sub` `subtract`; `plus` `add`; `escape` `esc`;
`delete` `del`.  A character with a name prints by one of them: `\backslash` `\backtick` `\bell` `\backspace` `\lf`
`\ff` `\null` `\quote` `\cr` `\rparen` `\lparen` `\rcurly` `\lcurly` `\rbracket` `\lbracket` `\slash` `\space` `\tab`
`\tick` `\tilde` `\vtab` `\bang` `\at` `\hash` `\dollar` `\percent` `\caret` `\amp` `\pipe` `\star` `\gt` `\lt`
`\comma` `\colon` `\semicolon` `\dot` `\qmark` `\underbar` `\minus` `\plus` `\escape` `\delete`; any other as itself
(`\a`).

**The shorthand.**  A prefix right before `(` or `{` is the function it names, put first, as a C-style call:
`?(c a b)` is `(if c a b)`, and `?{c a b}` is `{if c a b}` (a function's body).

| Prefix | Function |
| --- | --- |
| `?` | `if` |
| `=` | `set` |
| `:` | `def` |
| `#` | `hash-create` |
| `@` | `fn` |
| `.` | `unpack` |
| `~` | `format` |

**Numbers.**  `_` is ignored in a number (`1_000_000`).

| Form | Is |
| --- | --- |
| `42`, `-7`, `+5` | An integer, of any size |
| `3.25` | A fixed decimal, with the places written (a digit before the point: `.5` isn't a number) |
| `720/84` | A rational, in lowest terms (`60/7`); an integer when it's whole; both parts integers, in any base; `1/0` is a reader error |
| `#xFF`, `#b101` ... | A named base: `#` and its letter (below), then its digits, a `.` and more for a fraction |
| `#16rFF` | A radix of 2 to 80, its digits `0-9`, `A-Z`, `a-z`, then ``-=+`~!@#$%^&*,;:|?`` (case matters past 36) |
| `#[01]101` | Digits of its own, in order from zero (2 to 80 of them, each once) |
| `#<x...`, `#>x...` | The digits least first, or most first |
| `#=...` | Balanced (a radix or digits of its own): the middle digit is zero |
| `#+...`, `#-...` | A positive base, or a negative one |
| `-#x10` | A sign before `#` |

A fraction in base 10 is a fixed decimal; in any other base, a rational.  The named bases (balanced and
least-digit-first ones first): `c` `-0+`, `e` `=-0+#`, `g` `~=-0+#*`, `i` `UON`, `j` `WUONM`, `m` `DanielStphrus`
(negative); `b` binary, `t` 3, `q` 4, `v` 5, `f` 6, `s` 7, `o` 8, `n` 9, `d` 10, `x` 16, `z` 36; balanced, most
digit first: `k` (27: `ZYX...N0AB...M`), `y` (53: `zy...a0AB...Z`).  A word that starts like a number and isn't one
is a symbol (`1+`, `1/x`).

**Reader errors.**  An unclosed or wrongly closed bracket (`(1 2]`), an unknown escape or character name, a line's
end in a plain string, a control character, non-ASCII outside a string (in a word too: `a\x01b`), `1/0`, `$(`, and a
name or number right against an opening bracket (`f(x)`, `<(h k)`, `x[1]`, `+#(a)`: a prefix is a word's first
character).  At the REPL, an unclosed bracket or here string asks for another line (the open closers shown,
innermost last: `\t}) <`), the lines so far read again with it; an error ends the expression at once; the input's
end in one is the error `missing }`.

## 2. Values

| `type-of` | Values |
| --- | --- |
| `:number` | Integers, fixed decimals, rationals, complex numbers |
| `:char` | Characters |
| `:string` | Strings |
| `:atom` | Atoms (`:x`) |
| `:symbol` | Symbols (a list's items as written, `gensym`'s, `to-sym`'s) |
| `:list` | Q-expressions, NIL too |
| `:sexpr` | S-expressions (an fexpr's arguments, `read`'s items) |
| `:t` | T |
| `:function` | Functions and built-ins, partially applied ones too |
| `:hash` | Hashes |
| `:stream` | Streams |
| `:error` | Errors |
| `:exit` | `exit` |

**Truth.**  NIL (the empty list, `{}`, `()`) is false; everything else is true.  The tests give T or NIL.

**Numbers.**  A sum, difference or product is complex if either number is; else rational if either is; else
fixed if either is; else an integer.  A quotient is exact: a rational, or an integer when it's whole (complex if
either is).  A rational whose denominator is 1 is an integer, and a complex number whose imaginary part is 0 is real.
A fixed decimal keeps its places (`(+ 0.5 0.25)` is `0.75`) and prints without zeros at its end (`3`, not `3.0`).

**Equality** (`eq`): numbers by value whatever their kinds (`(eq 1 1.0)`, `(eq 1/2 0.5)`); characters, strings,
atoms and symbols by their text (a character isn't a string); lists item by item, and a Q-expression isn't an
S-expression; T is T; hashes by their entries' keys and values; functions by their formals and body, built-ins by
which and the values given them; errors by their messages.  Values of different kinds aren't equal.

**Order** (`cmp`, `<`, `>`, `sort`): by kind first, in this order: numbers, characters, strings, atoms, symbols,
Q-expressions, S-expressions, T, functions, hashes, streams, errors; then by value: numbers by value (a complex number
by its real part, then its imaginary), characters and strings by their bytes, atoms and symbols by name, lists item by
item (one that's the start of another first).  Equal values are in the same place.

**Printing.**  Two forms: as `print` shows a value (display) and as the REPL does (`repr`).  They differ only for
strings and characters.

| Value | `repr` | `print` |
| --- | --- | --- |
| A string | `"a\nb"`, escaped so it reads back (`\0 \a \b \f \n \r \t \v \e \\ \"`, other control characters `\xHH`; bytes past ASCII as they are) | Its text |
| A character | `\a`, `\space` | The character |
| A number | `42`, `1.5`, `7/2`, `1+2i`, `1i` | The same |
| An atom, a symbol | `:a`, `a` | The same |
| A list | `{1 "a" \b}`, `(+ 1 2)`; empty, `NIL` | Items in `repr` form |
| A function | `<function>(fn {x} {* x 2})` | The same |
| A built-in | `<function>(+)`; partially applied, `<function>(eq 1)` | The same |
| A hash | `<hash>{{:a 1 :tag} :hashtag}` | The same |
| A stream, T, exit | `<stream>`, `T`, `exit` | The same |
| An error | `Error: message` | The same |

## 3. Evaluation

* **A symbol** is its value, from the innermost scope out (lexical scope); unbound, it's the error `Unbound
  Symbol 'name'`.  **A number, string, character, atom, T, hash, stream or error** is itself.
* **A Q-expression** is itself; the first time it's evaluated it remembers the scope it's in, where `eval` runs it.
* **An S-expression** is a call.  `()` is NIL.  Its first item is evaluated: a function or built-in is called with
  the rest; a hash is called (below); `(x)` of anything else is `x`; anything else with arguments is an error.
* **A hash called**, `(h key arg...)`: the value at `key` (evaluated).  A function there is a method: it's applied to
  the arguments that follow with `&0` the hash (through which its private entries are had), as any function is: with
  fewer than its formals, it's partially applied.  Any other value is the value, and arguments after it are an error.

**Functions.**  `(fn {formals} {body})` makes a function; its body runs in a new scope of its own whose parent is
the scope the function was made in, so closures keep their variables.

* Its arguments are evaluated left to right; the first error stops them, and is the call's value (the function
  isn't run).
* Given fewer arguments than formals, it's partially applied: a function with those bound, waiting for the rest.
* The arguments past its formals are `&1`, `&2` ... (the first extra is `&1`) and `&_`, a list of them (NIL for
  none).  Given more arguments than formals is an error (`The function takes 1 argument, not 2`), unless its body
  names `&_` or `&N`.
* An fexpr, `(fexpr {formals} {body})`, gets its arguments as they're written, each remembering the caller's scope;
  `(eval x)` evaluates one there.  An fexpr gets errors as values.

**Built-ins** have the arguments they need and the most they take (section 4).  Given fewer, an ordinary built-in is
partially applied (`((eq 1) 1)` is T); given more, it's an error (`'len' takes 1 argument, not 2`).  An ordinary
built-in's arguments are evaluated first, and the first error is the call's value, but `error?`, `type-of`,
`error-message`, `error-code`, `repr`, `print`, `write`, `print-to`, `write-to` and the type tests take errors as
values.  **Special forms**
take their arguments as they're written and are never partially applied: `def`, `set`, `set!`, `let`, `do`, `eval`,
`if`, `and`, `or`, `while`, `each`, `dotimes`, `try`, `<=>`, `output-of`, `defined?`, `expr?`.

**Tail calls.**  A call in tail position takes its caller's place: a function body's last expression, and through
`if`'s branches, `do`'s last, `let`'s last, `eval` and a method.  So a tail-recursive loop runs in constant space.
Other calls nest only so deep (section 6); deeper is the error `Too deep: more than N calls nested`.

**Scope.**  `(def {x y} 1 2)` binds in the global scope; `(set {x} v)` (`=(x v)`) in the current one; `(set! {x} v)`
changes the nearest binding of `x` (unbound: an error).  The names may be one symbol, a list of them, or an
S-expression that evaluates to a list of them; a value for each; anything else is the error `'def' cannot define
non-symbols`.  Each is NIL.

**Errors are values.**  `(error message [code])` makes one; it stops whatever it reaches: a call it's an argument
of, `do`, `let`, `and`, `or`, the loops.  A failure of the system's is its text after the name it's about (`x: not
found`) and its code (`:noent` ...: section 4's system list).  `(try x [handler])` catches one.

**Ctrl-C** stops what's running: at the next call or loop step it's the error `interrupted` (`:intr`), which `try`
can catch; at the prompt it gives up the line.

**`(exit n)`** ends the program, its status `n` (`exit` alone, at the REPL, ends it).

## 4. The built-ins

Each takes what its form shows; fewer is partial application (but a special form's), more an error.  A count or an
index is a whole number (`2.0` and `4/2` are too); an index outside a list or string is an error; a count past its
end takes what there is; a negative count is an error.

### Definitions and functions

| Built-in | Value |
| --- | --- |
| `(fn {formals} {body})` | A function (section 3); its formals a list of symbols and its body a list, or an error as it's made |
| `(fexpr {formals} {body})` | A function whose arguments come as they're written |
| `(fun {name formals...} {body})` | NIL: `name` defined globally as `(fn {formals...} {body})` |
| `(def names values...)`, `(set ...)`, `(set! ...)` | NIL; special (section 3) |
| `(let {{name value} ...} body...)`, `(let body)` | The last body expression's value, in a scope of its own; the bindings made in turn, each value evaluated there (`{name}` alone is NIL).  A body expression written as a Q-expression is run as code; anything else is evaluated (a name is its value, a list too).  Special; the last in tail position |
| `(do x...)` | The last value (NIL for none); the first error stops it.  Special; the last in tail position |
| `(gensym [prefix])` | A new symbol: `g__1`, `g__2` ..., or `prefix__N` |

### Control

| Built-in | Value |
| --- | --- |
| `(if test then [else])` | `then`'s value if `test` isn't NIL, else `else`'s (NIL if none).  A branch written as a Q-expression is that data, not run.  Special; the branch in tail position |
| `(and x...)` | The first NIL (the rest not evaluated), or the last value; T for none.  An error stops it.  Special |
| `(or x...)` | The first value that isn't NIL (the rest not evaluated), or NIL.  An error stops it.  Special |
| `(<=> n case...)` | `n` a number (as `cmp` gives); each case `{test value}`, the last maybe `{value}` alone: the first whose test is true of `n` (`=`, `<>`, `>`, `<`, `>=` or `=>`, `<=` or `=<`, against 0), its value evaluated; NIL if none.  At least one case.  Special |
| `(while test body...)` | Runs the body while `test` isn't NIL; the last body value, or NIL.  `test` and each body expression: a Q-expression is run as code (in its scope), anything else evaluated.  Special |
| `(each f list)`, `(each {x list} body...)` | NIL: `f` called with each item, or each character of a string; or the body run with `x` each of them (in a scope of its own; a Q-expression run as code).  Special |
| `(dotimes n f)`, `(dotimes {i n} body...)` | NIL: as `each`, over `0` to `n-1` (`n` a count).  Special |
| `(try x [handler])` | `x`'s value, or if it's an error, NIL, or the handler's: evaluated with `&err` the message and `&code` the code (or NIL); a handler that's a function is called with the message.  A Q-expression is run as code.  Special |
| `(error message [code])` | An error: the message as `print` shows it; the code an atom |
| `(error-message x)` | An error's message; NIL for anything else |
| `(error-code x)` | An error's code (an atom); NIL if it has none, or for anything else |

### Evaluation and lists

| Built-in | Value |
| --- | --- |
| `(eval x)` | A Q-expression run as code, in the scope it remembers; an S-expression or a symbol (an fexpr's argument) evaluated in its scope; anything else itself.  Special; in tail position |
| `(list x...)` | A list of the values (`[x...]`) |
| `(head l)`, `(end l)` | A list of the first item, or the last; an empty list is an error |
| `(tail l)`, `(init l)` | All but the first item, or all but the last; an empty list is an error |
| `(join l...)` | The lists' items in one list (at least one, each a list) |
| `(len x)` | A list's items, a string's bytes, a hash's entries |
| `(item-at l i)` | Item `i` (from 0), as it's written |
| `(subset l i [n])` | `n` items from `i` (to the end without `n`; `i` may be the length: NIL) |
| `(reverse x)` | A list's items, or a string's characters, the other way round |
| `(range to)`, `(range from to [step])` | The numbers from `from` (0) up to, not including, `to`, by `step` (1); down, with a negative step; numbers of any real kind; a step of 0 is an error |
| `(sort l [less])` | The items in order (stable): by `cmp`, or by `less`, a function of two that's true when the first goes first |

### Numbers

| Built-in | Value |
| --- | --- |
| `(+ x...)` | The sum (0 for none); or, the first a string or character, the values as `print` shows them, joined (`(+ "n=" 5)` is `"n=5"`); anything else first: numbers only |
| `(- x...)` | `x` minus the rest; `(- x)` is its negation; 0 for none |
| `(* x...)` | The product; 1 for none |
| `(/ x...)` | `x` divided by the rest, exactly; `(/ x)` is 1/x; 1 for none; by zero, the error `Division by zero.` |
| `(rational.n x)`, `(rational.d x)` | A real number's numerator, or denominator, in lowest terms |
| `(to-rational x)` | A real number as a rational (an integer when it's whole) |
| `(to-fixed x [places])` | A real number as a fixed decimal of `places` (10, 0 to 10000) places, cut short |
| `(truncate x)` | A real number's integer part (toward zero) |
| `(complex re im)` | A complex number of two real numbers (a real number when `im` is 0) |
| `(random n)` | An integer from 0 up to, not including, `n` (an integer, 1 or more) |
| `(fib n)` | The `n`th Fibonacci number (`n` 0 or more) |
| `(val s)` | The number a string is written as (spaces around it allowed); not one, an error |

### Comparison and types

| Built-in | Value |
| --- | --- |
| `(eq x y)`, `(neq x y)` | Whether they're equal (section 2), or not |
| `(cmp x y)` | -1, 0 or 1, by the order (section 2) |
| `(< x y)`, `(> x y)` | By the order |
| `(type-of x)` | Its type's atom (section 2) |
| `(t? x)`, `(nil? x)`, `(num? x)`, `(int? x)`, `(fixed? x)`, `(rational? x)`, `(complex? x)`, `(atom? x)`, `(symbol? x)`, `(string? x)`, `(char? x)`, `(function? x)`, `(error? x)`, `(qexpr? x)`, `(sexpr? x)` | Whether it's one; NIL for anything else.  `nil?` is true of `()` too |
| `(defined? x)` | Whether the name, as it's written, is bound (T is); `&1`, `&_` ... in this call's scopes only; a list of names, all of them.  Special |
| `(expr? x)` | T when given an expression, NIL when given none.  Special |

### Strings and characters

| Built-in | Value |
| --- | --- |
| `(index-of s sub)`, `(last-index-of s sub)` | Where `sub` (a string or character) is first, or last, in `s`, or -1; for a list, where an equal item is |
| `(substring s i [n])` | `n` bytes from `i` (to the end without `n`; `i` may be the length: `""`) |
| `(char-at s i)` | The character at `i` |
| `(str-split s [sep])` | The pieces between the separators (a string, or a list of strings; default space, tab, LF, CR); empty ones kept |
| `(str-upper s)`, `(str-lower s)` | ASCII's letters changed; a character stays a character |
| `(str-trim s)` | Without the spaces (ASCII's) at its ends |
| `(str-replace s old new)` | Every `old` (not empty) replaced |
| `(str-join l [sep])` | The items as `print` shows them, `sep` between |
| `(str-chars s)` | Its characters, a list |
| `(str-pad-left s n [c])`, `(str-pad-right s n [c])` | `s` made `n` long with `c` (a space; a character or a string of one) before or after it; a longer `s` as it is |
| `(str-repeat s n)` | `s` `n` times |
| `(format text x...)` | The text with each `{}` the next value as `print` shows it (`{{` and `}}` a brace); too few values or too many is an error |
| `(char-code c)` | A character's code (or a string of one's), 0-255 |
| `(code-char n)` | The character with code `n`, 0-255 |
| `(alpha? c)`, `(digit? c)`, `(space? c)`, `(upper? c)`, `(lower? c)` | Whether a character is one, or every character of a string that isn't empty (ASCII's classes) |

### Conversion

| Built-in | Value |
| --- | --- |
| `(to-str x [base])` | As `print` shows it; a number in a base (`"x"`, `"#x"`, `"#16r"`, with `<` `>` `+` `-`): `"#xFF"`, a rational `"#x1/#x3"`, a fixed decimal as a rational; a complex number in a base is an error |
| `(repr x)` | As the REPL shows it |
| `(to-sym x)` | A symbol: of a symbol, an atom or a string (its text in lower case) |
| `(to-atom x)` | An atom: of an atom, a symbol, a string or a number (as it prints) |

### Input and output

| Built-in | Value |
| --- | --- |
| `(print x...)` | NIL: the values as `print` shows them, spaces between, LF after, to stdout |
| `(write x...)` | NIL: the same, no spaces, no LF |
| `(output-of x...)` | What the expressions printed to stdout, a string; an error, if one is one.  Special |
| `(read text)` | The expressions in the text, unevaluated, a list; a reader error is an error |
| `(load path...)` | Each file's expressions run at the top level in turn; the last value.  A path is as it is, or with `.dl`; a bare name is also looked for in the library's folder.  An error is `file: message` |
| `(save x)`, `(save x path [mode])` | NIL: `x` as it would be read back, printed, or written to the file with LF (`:overwrite` or `:append`; an existing file without one is an error; its folder must be there, else `:noent`) |
| `(open path [mode])` | A stream on the file: `:read` (the default), `:write` (made, or emptied), `:append`; a directory is `:isdir` |
| `(close s)` | NIL; the console's streams can't be closed |
| `(read-line [s])` | A line without its LF (a CR before it dropped), from `s` or stdin; NIL at the end |
| `(read-byte [s])`, `(read-all [s])` | A byte (0-255), NIL at the end; or the rest, `""` at the end |
| `(seek s n)`, `(tell s)` | The position set (0 to the length), or the position |
| `(print-to s x...)`, `(write-to s x...)` | As `print` and `write`, to a stream |
| `stdin`, `stdout`, `stderr` | The console's streams |
| `args` | The program's file and its arguments, strings (NIL at the REPL) |

### Hashes

Keys are atoms, strings and integers; an entry has a key, a value and tags (atoms); the hash has tags too.  The
reserved tags: `:__locked` (no entries added or removed), `:__read-only` (nothing changed), `:__private` (an entry
only its methods reach), `:__not_nil` (an entry that can't be NIL).  Putting NIL takes an entry out.

| Built-in | Value |
| --- | --- |
| `(hash-create [entries])`, `#(entries)` | A hash: the entries a list of `{key value tag...}` (each value evaluated) and the hash's tags; a bad entry is an error |
| `(to# entries)`, `(from# h)` | A hash from such a list, or the list from a hash |
| `(hash-get h key)` | The value (NIL if none; NIL for a NIL hash); a private entry is an error |
| `(hash-put h entry...)` | The old value (NIL if none); an entry `{key value tag...}`, its value evaluated (`{tag}` alone: a tag on the hash); several: a list of them.  Anything else, or a non-atom tag, is an error |
| `(hash-remove h key)` | The entry taken out: its value, or NIL |
| `(hash-key? h key)`, `(hash-keys h)`, `(hash-values h)` | Whether it has the key; the keys; the values |
| `(hash-call h key x...)` | As `(h key x...)` (section 3) |
| `(hash-clone h [override...])` | A copy, each override put (a tag, an entry `{key value tag...}` with its value as it is, or a list of them), then the hash's own tags; a bad override is an error |
| `(hash-add-tag h tag...)` | A tag (an atom) on the hash, or `{key tag}` on an entry: whether it was new |
| `(hash-lock h [key...])`, `(hash-make-const h ...)`, `(hash-make-private h ...)`, `(hash-make-not-nil h ...)` | The reserved tags, on the hash or on the keys' entries |
| `(hash-tag? h tag)`, `(hash-locked? h [key])`, `(hash-private? h [key])`, `(hash-const? h [key])` | Whether the hash (or the entry) has it |

### The library's built-ins

| Built-in | Value |
| --- | --- |
| `(not x)` | T for NIL, NIL for anything else |
| `(== x y)`, `(>= x y)`, `(<= x y)` | `eq`, and the order's |
| `(neg? x)`, `(pos? x)`, `(zero? x)`, `(one? x)` | Of a number |
| `(1+ x)`, `(1- x)`, `(abs x)` | Of a number |
| `(cons x l)` | `x` and the list's items, a list |
| `(fst l)`, `(snd l)`, `(thd l)`, `(last l)`, `(nth i l)` | An item, as it's written |
| `(take n l)`, `(drop n l)` | The first `n` items, or all but them |
| `(elem? x l)`, `(in? x l)` | Whether an item is equal to `x` |
| `(map f l)`, `(filter f l)` | `f` of each item; or the items `f` is true of |
| `(foldl f z l)`, `(foldr f z l)` | `f` from the left `(f (f z a) b)`, or from the right `(f a (f b z))` |
| `(any? f l)`, `(all? f l)`, `(find f l)`, `(count f l)` | T at the first item `f` is true of, else NIL; NIL at the first it's false of, else T; that item, else NIL; how many it's true of |
| `(sum l)`, `(product l)` | `+` (from 0), `*` (from 1), over the items |
| `(min x...)`, `(max x...)` | The least, or most, by the order |

### The system

A failure is the system's error: the text after the name it's about, and its code: `:perm` not allowed, `:inval`
invalid argument, `:intr` interrupted, `:nomem` out of memory, `:noent` not found, `:exist` already exists,
`:notdir` not a directory, `:isdir` is a directory, `:notempty` directory not empty, `:nospc` disk full, `:rofs`
read-only, `:io` i/o error, `:noexec` not a program, `:eof` end of file, `:srch` no such task, `:child` no such child
(and `:nosys`, `:busy`, `:range`, `:nametoolong`, `:badf`).  An argument one can't take is the error saying so, its code
`:inval`.  A path is a string (or an atom's or symbol's name).

| Built-in | Value |
| --- | --- |
| `(read-file path)`, `(read-lines path)` | The file's bytes, a string; or its lines (LF or CR LF; a last one with no LF too) |
| `(write-file path x...)`, `(append-file path x...)` | NIL: the values as `print` shows them, joined, written, or added at its end |
| `(ls [path])`, `(dir [path])` | The names in a directory (`.`), sorted; or their stat records |
| `(stat path)` | A hash: `:name`, `:length`, `:dir` (T for a directory), `:mtime` (seconds since 2000-01-01) |
| `(exists? path)`, `(dir? path)`, `(file? path)` | Whether it's there, a directory, a file |
| `(mkdir path)`, `(remove path)` | NIL: a directory made (its parent must be there); a file or an empty directory removed |
| `(rename from to)`, `(copy-file from to)` | NIL |
| `(cd [path])`, `(cwd)` | NIL: the current directory changed (home); or it |
| `(glob pattern)` | The paths matching, sorted: rc's `*`, `?`, `[...]` (`a-z` ranges, `~` first: none of them) in each part |
| `(run prog arg...)` | The program's exit code, once it's done |
| `(sh line [input])`, `(sh-out line [input])` | A command line run by the shell: its exit code; or its output, a string |
| `(spawn prog arg...)`, `(wait task)`, `(kill task)`, `(pid)` | A task started (its number); its exit code once it ends; NIL; this one's number |
| `(env [name])`, `(setenv name value)`, `(unsetenv name)` | A variable (NIL if none), or a hash of them all; NIL (a list is its items, spaces between) |
| `(time)`, `(date [t])`, `(date-parts [t])`, `(seconds-of y m d [h mi s])` | Seconds since 2000-01-01; `"YYYY-MM-DD hh:mm:ss"`; a hash of `:year` ... `:weekday`; a time's seconds |
| `(ticks)`, `(tick-rate)`, `(sleep secs)` | The clock's ticks (0-32767, 200 a second); 200; NIL after that long (a fraction too) |
| `(bit-and n...)`, `(bit-or n...)`, `(bit-xor n...)`, `(bit-not n)`, `(shl n k)`, `(shr n k)`, `(bit? n k)` | On integers (two's complement, of any size) |
| `(hex n [width])`, `(bin n [width])` | Digits in upper case, at least `width` of them |
| `(lo n)`, `(hi n)`, `(word lo hi)` | A word's low byte, high byte; a word of two bytes |
| `(bytes s)`, `(from-bytes l)` | A string's bytes, a list; a string of a list of bytes |
| `(read-bytes s n)`, `(write-bytes s l)` | Up to `n` bytes from a stream (NIL at its end); NIL: bytes written |
| `(platform)`, `(hydra?)` | `:windows`, `:linux`, `:macos`, `:hydra`; whether it's the Hydra |

## 5. The library

`lib/globals.dl` (on the Hydra `/lib/hylang/globals.hl`) is loaded first.  Besides the library's built-ins:

| Name | Is |
| --- | --- |
| `math.pi`, `math.e`, `math.phi`, `math.gamma` | Constants, fixed decimals |
| `flip`, `comp`, `ghost` | `(flip f a b)` is `(f b a)`; `(comp f g x)` is `(f (g x))`; `(ghost f x...)` is `(f x...)` |
| `<>`, `!=` | `neq` |
| `=0`, `=1`, `0?`, `1?` | `zero?`, `one?` |
| `xor` | Of two |
| `>=0`, `<=0`, `>0`, `<0`, `!=0`, `!=1` | Against 0 or 1 |
| `square`, `cube`, `recip`, `inverse` | |
| `trunc` | `truncate` |
| `unpack`, `apply`, `curry` | `(unpack f l)`: `f` called with the list's items |
| `pack`, `uncurry` | `(pack f x...)`: `f` called with a list of them |
| `first` ... `tenth` | `(nth 0)` ... `(nth 9)` |
| `??` | The first argument that isn't NIL |
| `skip`, `split` | `drop`; `(split n l)`: `{(take n l) (drop n l)}` |
| `has-element?` | `(has-element? l x)` |
| `begin`, `block` | `do` |
| `zip`, `flatten`, `distinct` | Pairs; nested lists' items in one; each item once |
| `avg` | A list's mean |
| `cond`, `case` | `(cond {test value}...)`, `(case x {key value}...)`: the first clause whose test is true (whose key equals `x`), its value, each run where it was written; none, an error (`Selection not found`, `Case not found`) |
| `floor`, `ceil` | Toward minus or plus infinity |
| `div`, `mod`, `%`, `divmod` | Rounding down: `(+ (* b (div a b)) (mod a b))` is `a`; `divmod` both |
| `fdiv`, `frecip`, `finverse` | A quotient, a reciprocal, as fixed decimals (`&1` places, 10) |
| `odd?`, `even?`, `positive?`, `negative?`, `+?`, `-?` | Of numbers; anything else is an error |
| `pow` | `(pow a b)`: `b` a whole number |
| `minimum`, `maximum` | Of a list |
| `starts-with?`, `ends-with?`, `contains?` | Of strings |
| `repeat`, `times` | `(repeat n code)`: a list of `n` runs' values |
| `use`, `used` | `(use "name")` loads a library once; `used`, the names |
| `remove-fst`, `remove-all`, `remove-min`, `remove-max` | A list without the first (every) item equal to one; without its least, or most |
| `sort-desc`, `best-of`, `worst-of`, `top`, `bottom` | Most first; the most `n`, the least `n` |
| `between?` (`><?`), `not-between?` (`!><?`), `all-between?` (`*><?`), `all-not-between?` (`*!><?`), `not-all-between?` (`!*><?`) | `(between? a b x)`: `x` from `a` to `b`, either way round |

`lib/dice.dl`: dice for games.  `lib/screen.dl`: the terminal's screen (`cls`, `at`, `color` ...).

## 6. The two implementations

| | C# danlang | hylang on the Hydra-16 |
| --- | --- | --- |
| Integers | Any size | Up to 255 bytes (about 614 digits) |
| Calls nested (not in tail position) | 10,000 | About 2,500 |
| `range` | 1,000,000 items at most | As memory allows |
| `load`, `use` | `name`, `name.dl`, then `lib/name.dl` | `name`, `name.hl`, then `/lib/hylang/name.hl` |
| `sh`, `sh-out` | `cmd.exe /c` or `/bin/sh -c` | rc (`rc -c`) |
| `stat` | `:name`, `:length`, `:dir`, `:mtime` | Those, and `:mode`, `:qid`, `:dev` |
| `platform` | `:windows`, `:linux`, `:macos` | `:hydra` |
| Ctrl-C | The console's | The window's `interrupt` note |
| Strings, lists, hashes | As memory allows | As memory allows |
