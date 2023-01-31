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
        "#v004112311",
        "#<-x004112311",
        "#5r004112311",
        "-#b1010111010110",
        "#q3020103020123010201301",
        "-000123123802380986654945715",
        "9876543210",
        "#-[9876543210]9876543210",
        "1.23",
        "0.01",
        "00000000123123123000000000"
    };

    public static void TestNumbers() {
        foreach (var xx in new[] {"0", "1", "9875", "4910956197564501375461209835638104736961", "500.75", "3.229124917509812370", "1/2", "93438974263/181277823"}) {
            foreach (var d in "<>") {
                foreach (var i in new[] {1, -1}) {
                    foreach (var s in "+-") {
                        Console.WriteLine($"Number: {(i < 0 ? "-" : "")}{xx}, LE: {(d == '<')}, Base: {(s == '+' ? "Positive" : "Negative")}");
                        foreach (var b in "bcdefgkmnoqstvxyz") {
                            var bs = $"#{d}{s}{b}";
                            Num x = NumberParser.ParseString(xx)! * i;
                            var ret = NumberParser.ToBase(x, bs);
                            var pStr = ret;
                            var cStart = ret.IndexOf('[');
                            if (cStart > 0) {
                                var cEnd = ret.IndexOf(']');
                                pStr = pStr.Remove(cStart, (cEnd - cStart) + 1);
                            }
                            Console.WriteLine($"\t{b} => {ret}");
                            var val = Parser.Tokenize(new StringReader(ret)).ToArray().First().num;
                            if (x.ToString() != val!.ToString()) Console.WriteLine($"\t\t*****MISMATCHED INPUT/OUTPUT: IN={x} => OUT={val}");
                        }
                    }
                }
            }
        }

        var bi = BigInteger.Parse("98765432100000000000000");
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

        Rat r = new Rat(10000, 1000);
        Console.WriteLine(r);  // "10"
        r = new Rat(169, 13);
        Console.WriteLine(r);  // "13"
        r = new Rat(712384, 32587);
        Console.WriteLine(r);
        Console.WriteLine(r.ToFix(100));
        Console.WriteLine(r.ToString("M"));
        Console.WriteLine((-r).ToString("M"));
        r = (Rat)Num.Parse(r?.ToString())!;
        Console.WriteLine(r);
        Console.WriteLine(r.ToFix(20));
        r = (Rat)Num.Parse("720/84")!;
        Console.WriteLine(r); // "60/7"
        Console.WriteLine(r.ToFix(100));
        Console.WriteLine(r.ToString("M"));

        Console.WriteLine(Num.Parse("2.5")! + Num.Parse("3/7")!);
        Console.WriteLine(Num.Parse("2.5")! - Num.Parse("3/7")!);
        Console.WriteLine(Num.Parse("2.5")! * Num.Parse("3/7")!);
        Console.WriteLine(Num.Parse("2.5")! / Num.Parse("3/7")!);

        Console.WriteLine(Rat.ToRat((Num.Parse("10000")! + Num.Parse("0.00001")!)).ToFix());
        Console.WriteLine(Num.Parse("10000")! - Num.Parse("0.00001")!);
        Console.WriteLine(Num.Parse("10000")! * Num.Parse("0.00001")!);
        Console.WriteLine(Num.Parse("10000")! / Num.Parse("0.00001")!);

        Console.WriteLine((Num.Parse("-192837475601928383446/6757191026728393927") as Rat)?.ToString("M") ?? "error");

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
                r = (Rat)Num.Parse(rat)!;
            }
            catch (Exception e) {
                Console.WriteLine(e.Message);
            }
    }

    private static string InvalidTokens = @"
        #xabcdefghij
        #b-1011
        #f0000
        -#dabc
        1a
        ""
        ""
        #c01
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
