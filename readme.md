Work in progress LISP-inspired language.

## Running it

    dotnet run                      the REPL (lib/globals.dl loaded first; exit, or Ctrl-Z, to leave)
    dotnet run -- file.dl a b       a program: file.dl run, args {"file.dl" "a" "b"}; its status 0, 1 after an
                                    error (shown on stderr), or (exit n)'s
    dotnet run -- tests/regress/run.dl
                                    the regression suite (from danlang's folder): each file's checks, then the
                                    count; status 1 if any failed
    dotnet run -- -t                the C# tests of numbers in every base, and of the tokenizer

`load` finds `name`, `name.dl`, or, for a bare name, `lib/name.dl` (here, then beside danlang itself).

## The language

**Syntax.**  `(...)` is an S-expression, evaluated: its first item is called with the rest.  `{...}` is a
Q-expression: a list, data until `eval` runs it as code.  `;` starts a comment.  Strings are `"..."` with escapes
(`\n \t \r \0 \a \b \f \v \e \\ \" \xHH`), or here strings, opened and closed by the same odd number of quotes
(`"""say "hi" """`), whose text is as it is (a line's end is LF).  `\name` is a character (`\a`, `\space`,
`\lparen` ...), `:name` an atom; names are case-insensitive.  `T` is true; `NIL`, the empty list, is false (and is
the only false value).  A prefix before `(` or `{` is a shorthand: `'` list, `^` head, `$` tail, `.` unpack, `|` join,
`=` set, `:` def, `@` fn, `!` eval, `?` if, `#` hash-create, `<` hash-get, `>` hash-put, `*` hash-call, `~` format.

**Functions.**  `(fn {x y} {body})` makes one; `(fun {name x y} {body})` defines one.  Given fewer arguments than it
has formals, a function is partially applied (`((add 1) 2)` is 3, for an `add` of two), and so is a built-in given
fewer than it needs (`((eq 1) 1)` is T; not a special form, as `if` or `let`).  Extra arguments are `&1`, `&2` ...
(by position) and `&_` (a list of them), for a function whose body names them; given more arguments than it takes,
a function or a built-in is an error.  `(fexpr {x} {body})` makes a function whose arguments come as they're
written, unevaluated; `(eval x)` evaluates one where it was written.

**Scope is lexical.**  A function sees its own variables and those of the scope it was made in, never its caller's,
so closures keep theirs (`(fun {make-adder n} {fn {x} {+ x n}})`).  A Q-expression remembers the scope it was
written in, and `eval` runs it there: code passed to a function (`cond`'s clauses, `repeat`'s body) sees the
variables where it was written.  `(def {x} v)` defines globally; `(set {x} v)` (`=(x v)`) binds in the current
scope; `(set! {x} v)` changes the nearest binding of `x`, wherever it is.  `(let {{a 1} {b (+ a 1)}} body...)` binds
in a scope of its own, in turn; `(let {code})` runs code in one.

**Tail calls.**  A call in tail position (a function's last expression, through `if`, `do`, `let` and `eval`)
takes its caller's place, so a tail-recursive loop runs in constant space.  Other calls nest 10,000 deep at most:
deeper is an error.

**Errors are values that stop what they reach.**  `(error "message")` makes one; a function called with an error as
an argument is that error (it isn't run); `do`, `let`, `while` and the rest stop at one.  `(try expr handler)` is
expr's value, or, if that's an error, the handler's, evaluated with `&err` the message (a function handler is called
with it); `(try expr)` is NIL then.  `(error-message e)` is an error's message.  Built-ins and fexprs get errors as
values (`error?`, `type-of`).

**Control.**  `if`, `and`, `or` (short-circuited), `<=>` (on a number's sign), `do`, `while`, `(each f list)` or
`(each {x list} body...)`, `(dotimes n f)` or `(dotimes {i n} body...)`, `(range [from] to [step])`; `map`,
`filter`, `foldl`, `foldr`, `any?`, `all?`, `find`, `count` (built in) and the library's `cond`, `case` and the rest.
A list's items (`fst`, `nth`, `last` ... and what passes them on, as `map`) are as they're written, not evaluated.

**Data.**  Numbers: integers of any size, fixed decimals (`3.14159_26535`), rationals (`720/84`), complex numbers,
and many bases (`#xff`, `#b101`, `#16rFF`, balanced and negative ones; `(to-str n "x")`).  A sum or product is
complex if either number is, else rational, else fixed, else an integer; a quotient is exact (a rational, or an
integer when it's whole).  Strings (`+` joins, `substring`, `str-split`, `str-join`, `format`, `str-upper` ...),
characters (`char-code`, `code-char`, `alpha?` ...), atoms, symbols (`to-sym`, `gensym`), lists, and hashes (keys
are atoms, strings or integers; tags make them or their entries private, locked, read-only or not NIL; functions
in them are methods, `hash-call` giving them `&0`, the hash).  `type-of` names a value's type.

**Input and output.**  `print` (a string's text as it is, spaces between, a newline after) and `write` (no spaces,
no newline); `repr` is a value as the REPL shows it.  Streams: `(open path [:read | :write | :append])`, `close`,
`read-line`, `read-byte`, `read-all`, `print-to`, `write-to`, `seek`, `tell`, and `stdin`, `stdout`, `stderr`.
`(output-of expr...)` is what the expressions printed.  `save` writes a value to be read back; `load` runs a file;
`read` turns text into expressions.

**The system** (what the Hydra-16 has and a PC has too, so a program runs on both).  Files: `read-file`, `read-lines`,
`write-file`, `append-file`, `ls`, `dir` and `stat` (a hash: `:name`, `:length`, `:dir`, `:mtime`), `exists?`, `dir?`,
`file?`, `mkdir`, `remove`, `rename`, `copy-file`, `cd`, `cwd`, `glob` (rc's `*`, `?` and `[...]`).  Programs: `run`
(its exit code), `sh` and `sh-out` (a line for the shell, with input if it's given), `spawn`, `wait`, `kill`,
`pid`.  The environment: `env`, `setenv`, `unsetenv`.  The clock: `time` (seconds since 2000, as the Hydra counts),
`date`, `date-parts`, `seconds-of`, `ticks` and `tick-rate` (200 a second), `sleep` (seconds, a fraction too).
Bits and bytes: `bit-and`, `bit-or`, `bit-xor`, `bit-not`, `shl`, `shr`, `bit?`, `hex`, `bin`, `lo`, `hi`, `word`,
`bytes`, `from-bytes`, `read-bytes`, `write-bytes`.  A failure is the Hydra's error: its text after the name it's
about (`x: not found`) and its code, `(error-code e)` (`:noent`, `:exist`, `:notempty` ...; a program's own errors can
have one: `(error "msg" :code)`; a `try` handler has it as `&code`).  `(platform)` and `(hydra?)` say where it runs.

**The library.**  `lib/globals.dl`, loaded first: list functions, `cond` and `case`, math and string helpers (its most
used, `fst`, `map`, `foldl`, `sum`, `==`, `not`, `1+` ..., are built in: `LibraryBuiltins.cs`),
`sort-desc`, `best-of`, `use` (a library loaded once: `(use "dice")`) ...  `lib/dice.dl`: dice for games.
`lib/screen.dl`: the terminal's screen (`cls`, `at`, `color`, `bold`, `cursor-off` ...).  `lib/harn.dl` and `lib/cngh.dl` are
example programs.

## The regression suite

`tests/regress/`: `run.dl` loads `harness.dl` (`check`, `check-error`: fexprs) and then each test file: `reader`,
`eval`, `scope`, `control`, `errors`, `lists`, `strings`, `numbers`, `hashes`, `types`, `io`, `system`, `bits` and
`library`.  Every
built-in and every library function has checks; a new one should too.  The suite is in danlang itself, so another
implementation of the language (hylang, on the Hydra-16) can run it as it is.
