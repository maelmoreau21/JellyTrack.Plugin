using System;
using System.Reflection;

class Program
{
    static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Usage: rescheck <path-to-dll>");
            return 2;
        }

        var path = args[0];
        try
        {
            var asm = Assembly.LoadFrom(path);
            var names = asm.GetManifestResourceNames();
            if (names.Length == 0)
            {
                Console.WriteLine("<no embedded resources>");
                return 0;
            }
            foreach (var n in names)
            {
                Console.WriteLine(n);
            }
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }
}
