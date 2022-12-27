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
        "00000000123123123000000000"
    };

    public static void TestNumbers() {
 
        var bi = BigInteger.Parse("999999129321000000");
        var dp = -25;

        while (dp <= 25) {
            var i = new Fix(bi, dp++);
            Console.WriteLine(i.ToString());
        }

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

    private static string ValidTokens = @"
        0x00ab123
        -1231
        -0b0100101 ; this is a comment
        ; this is a comment, too
        '(a b c d)
        ()
        .(a to-string 0 1 2 3)
        symbol-test
        ""string""
        """"""
    HERE STRING
    MULTIPLE LINES
    ""Quoted text""
    """"Doubley-quoted text""""
    AND SPACES   
""""""
    ";

    private static string InvalidTokens = @"
        0xabcdefghij
        0b-1011
        0f0000
        -0dabc
        1a
        ""
        ""
    ";

    public static void TestTokens() {
        foreach (var t in Parser.Tokenize(new System.IO.StringReader(ValidTokens))) {
            Console.WriteLine(t);
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