using System;

internal class Program
{
    interface IFoo
    {
        string Frob();
    }

    class Foo : IFoo
    {
        public string Frob()
        {
            return "Hello";
        }
    }

    private static void Main(string[] args)
    {
        IFoo o = new Foo();
        Func<string> f = o.Frob;
        Console.WriteLine(f());
    }
}