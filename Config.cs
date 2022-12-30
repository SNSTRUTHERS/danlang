public record Config {
    public enum Mode {
        Compile,
        RunTests
    }

    public Mode mode = Mode.Compile;

    private class CommandLineReader {
        private string[] args;
        private int index;
        private string? arg;

        public int Index { get { return index; } }

        public CommandLineReader(string[] args, int index, string? first_arg) {
            this.args = args;
            this.index = index;
            this.arg = first_arg;
        }

        public string? Next() {
            if (arg != null) {
                var ret = arg;
                arg = null;
                index++;
                return ret;
            } else if (index >= args.Length || args[index].StartsWith("-")) {
                return null;
            } else {
                return args[index++];
            }
        }
    }

    private struct CommandLineOption {
        public readonly char short_name = '\0';
        public readonly string? long_name = null;
        public readonly string? help = null;
        public readonly Func<CommandLineReader, Config, bool> callback;

        public CommandLineOption(char short_name, string help, Func<CommandLineReader, Config, bool> callback) {
            this.short_name = short_name;
            this.help = help;
            this.callback = callback;
        }

        public CommandLineOption(string long_name, string help, Func<CommandLineReader, Config, bool> callback) {
            this.long_name = long_name;
            this.help = help;
            this.callback = callback;
        }

        public CommandLineOption(char short_name, string long_name, string help, Func<CommandLineReader, Config, bool> callback) {
            this.short_name = short_name;
            this.long_name = long_name;
            this.help = help;
            this.callback = callback;
        }
    }

    private static CommandLineOption[] options = {
        new('h', "help", "Show this screen and exit", (reader, config) => {
            var max_length = options!.Select(option => option.long_name?.Length ?? 0).Aggregate(Math.Max);

            Console.WriteLine("usage: danlang [options...] [files...]");
            Console.WriteLine("options:");

            foreach (var option in options!) {
                if (option.short_name != '\0') {
                    Console.Write($"  -{option.short_name} ");
                } else {
                    Console.Write("     ");
                }

                if (option.long_name != null) {
                    Console.Write($"--{option.long_name}");
                }

                if (option.help != null) {
                    Console.WriteLine($"{new string(' ', max_length - (option.long_name?.Length ?? -2))} - {option.help}");
                } else {
                    Console.WriteLine();
                }
            }

            Environment.Exit(0);
            return true;
        }),
        new('t', "run-tests", "Runs the test suite", (reader, config) => {
            config.mode = Config.Mode.RunTests;
            return true;
        }),
        new("version", "Print version/vendor information and exit", (reader, config) => {
            Console.WriteLine($"danlang");
            Console.WriteLine("  authors:   Daniel & Simon Struthers");
            Console.WriteLine("  copyright: 2022-23 GPLv3");
            Console.WriteLine($"  version:   {Program.MAJOR_VERSION}.{Program.MINOR_VERSION}\n");
            Console.WriteLine("This is free software, and you are welcome to redistribute it under");
            Console.WriteLine("certain conditions. This program comes with ABSOLUTELY NO WARRANTY.");
            Environment.Exit(0);
            return true;
        })
    };

    public static Tuple<string[], Config> ParseCommandLine(string[] args) {
        var config = new Config();
        var short_opts = options.Where(opt => opt.short_name != 0).ToDictionary(x => x.short_name, x => x);
        var long_opts = options.Where(opt => opt.long_name != null).ToDictionary(x => x.long_name ?? "", x => x);

        int i;
        for (i = 0; i < args.Length;) {
            var arg = args[i];
            if (arg == "--") {
                ++i;
                break;
            } else if (arg.StartsWith("--")) {
                var delimiter = arg.IndexOf('=');
                if (delimiter == 2 || arg.IndexOf('-', 2) == 2) break;

                var name = arg.Substring(2, delimiter < 0 ? arg.Length - 2 : delimiter - 2);
                if (!long_opts.ContainsKey(name)) {
                    Console.Error.WriteLine($"Unknown option \"--{name}\"");
                    Environment.Exit(1);
                }

                var reader = new CommandLineReader(args, i, delimiter < 0 ? null : arg.Substring(delimiter + 1));
                if (!long_opts[name].callback(reader, config)) {
                    Environment.Exit(1);
                }
                i = reader.Index;
            } else if (arg.StartsWith("-") && arg.Length > 1) {
                for (var j = 1; j != arg.Length; ++j) {
                    var ch = arg[j];
                    if (!short_opts.ContainsKey(ch)) {
                        Console.Error.WriteLine($"Unknown option \"-{ch}\"");
                        Environment.Exit(1);
                    }

                    var reader = new CommandLineReader(args, i, j == arg.Length - 1 ? null : arg.Substring(j + 2));
                    if (!short_opts[ch].callback(reader, config)) {
                        Environment.Exit(1);
                    } else if (reader.Index > i) {
                        i = reader.Index;
                        break;
                    }
                }
            } else break;
        }

        return new(args.AsMemory(i).ToArray(), config);
    }
}