using System.Data;
using System.Numerics;
public static class Tests {
    private static string[] ValidNumbers = new string[] {
        "100000000000000000000000000000000000000000000000002",
        "100000000000000000.00000000000000000",
        "100000.00000000100000",
        "100000.00000000100009",
        "720/84",
        "712381/32882",
        "100000/1000",
        "169/13",
        "0x004112311",
        "-0b1010111010110",
        "0q3020103020123010201301",
        "-000123123802380986654945715",
        "9876543210",
        "1.23",
        "0.01",
        "+0.01-3.64i",
        "1+3i",
        "-1/3+3/7i",
        "1/3-3i",
        "00000000123123123000000000"
    };

    public static void TestNumbers() {
        foreach (var xx in new[] {"0", "1", "9875", "4910956197564501375461209835638104736961"}) {
            foreach (var d in "<>") {
                foreach (var i in new[] {1, -1}) {
                    foreach (var s in "+-") {
                        foreach (var chars in new[] {"", "[qwertyuiopasdfghjklzxcvbnm)(*&^%$#@!-0123456789MNBVCXZLKJHGFDSAPOIUYTREWQ]"}) {
                        Console.WriteLine($"Number: {(i < 0 ? "-" : "")}{xx}, LE: {(d == '>')}, Base: {(s == '+' ? "Positive" : "Negative")}");
                        foreach (var b in "bcdefgknoqstvxyz") {
                            var bs = $"0{d}{s}{chars}{b}";
                            BigInteger x = BigInteger.Parse(xx) * i;
                            var ret = Parser.IntAcc.ToBase(x, bs);
                            var pStr = ret;
                            var cStart = ret.IndexOf('[');
                            if (cStart > 0) {
                                var cEnd = ret.IndexOf(']');
                                pStr = pStr.Remove(cStart, (cEnd - cStart) + 1);
                            }
                            Console.WriteLine($"\t{b} => {ret}"); // {pStr.Substring(pStr.IndexOf(b) + 1)}");
                            var val = Parser.Tokenize(new StringReader(ret)).ToArray().First().num;
                            if (x != (val as Int)!.num) Console.WriteLine($"\t\t*****MISMATCHED INPUT/OUTPUT: IN={x} => OUT={val}");
                        }
                        }
                    }
                }
            }
        }

        var bi = BigInteger.Parse("999999129321000000");
        var dp = -25;

        while (dp <= 25) {
            var i = new Fix(bi, dp++);
            Console.WriteLine(i.ToString());
        }

        var r1 = new Rat(1, 5);
        var r2 = new Int(7);
        var r3 = new Fix(123812871238, 9);
        var r4 = new Rat(1719, -342);

        Console.WriteLine($"{r1}, double {(double)r1}, long {(long)r1}");
        Console.WriteLine($"{r2}, double {(double)r2}, long {(long)r2}");
        Console.WriteLine($"{r3}, double {(double)r3}, long {(long)r3}");
        Console.WriteLine($"{r4}, double {(double)r4}, long {(long)r4}");

        Console.WriteLine(r1+r2);
        Console.WriteLine(r1-r2);
        Console.WriteLine(r2-r1);
        Console.WriteLine(r1/r2);
        Console.WriteLine(r2/r1);
        Console.WriteLine(r1*r2);
        Console.WriteLine(r2*r1);

        Console.WriteLine(r1+r3);
        Console.WriteLine(r1-r3);
        Console.WriteLine(r3-r1);
        Console.WriteLine(r1/r3);
        Console.WriteLine(r3/r1);
        Console.WriteLine(r1*r3);
        Console.WriteLine(r3*r1);

        Console.WriteLine(r4+r3);
        Console.WriteLine(r4-r3);
        Console.WriteLine(r3-r4);
        Console.WriteLine(r4/r3);
        Console.WriteLine(r3/r4);
        Console.WriteLine(r4*r3);
        Console.WriteLine(r3*r4);

        Console.WriteLine(r4+r1);
        Console.WriteLine(r4-r1);
        Console.WriteLine(r1-r4);
        Console.WriteLine(r4/r1);
        Console.WriteLine(r1/r4);
        Console.WriteLine(r4*r1);
        Console.WriteLine(r1*r4);

        Num? r = new Rat(10000, 1000);
        Console.WriteLine(r);  // "10"
        r = new Rat(169, 13);
        Console.WriteLine(r);  // "13"
        r = new Rat(712381, 32882);
        Console.WriteLine(r); // "712381/32882"
        r = Num.Parse(r?.ToString());
        Console.WriteLine(r); // "712381/32882"
        r = Num.Parse("720/84");
        Console.WriteLine(r); // "60/7"

        foreach (var rat in ValidNumbers)
            try {
                Console.Write(rat + " => "); Console.WriteLine(Num.Parse(rat));
            }
            catch (Exception e) {
                Console.WriteLine(e.Message);
            }

        var badRats = new [] {"/1", "1/", "1/0"};
        foreach (var rat in badRats)
            try {
                r = Num.Parse(rat);
            }
            catch (Exception e) {
                Console.WriteLine(e.Message);
            }
    }

    private static string InvalidTokens = @"
        0xabcdefghij
        0b-1011
        0f0000
        -0dabc
        1a
        ""
        ""
        0c01
    ";

    public static void TestTokens() {
        var tests = new System.IO.DirectoryInfo(@".\tests");
        foreach(var test in tests.GetFiles()) {
            foreach(var t in Parser.Tokenize(new StreamReader(test.OpenRead()))) Console.WriteLine(t);
        }
        foreach (var t in Parser.Tokenize(new System.IO.StringReader(InvalidTokens))) {
            try {
                Console.WriteLine(t);
            }
            catch (Exception e) {
                Console.WriteLine(e.Message);
            }
        }
    }
}
