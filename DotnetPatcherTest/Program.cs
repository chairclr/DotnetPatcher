public class TestClass
{

    public static int TestFunction(int n)
    {
        return n * n - 1;
    }

    public static void Main(string[] args)
    {
        int n = 42;
        Console.WriteLine($"Output of function we want to change: {TestFunction(n)}");
    }
}