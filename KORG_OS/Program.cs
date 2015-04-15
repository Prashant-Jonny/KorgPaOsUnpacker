using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using ZLibNet;

namespace KORG_OS
{
    class Program
    {
        static void Main(string[] args)
        {
            if(args.Length >= 1)
            {
                Package pkg = new Package(args[0]);
                Console.WriteLine("Unpacking successful");
            }else{
                Console.WriteLine("Please drop .pkg file on this program");
            }

            Console.WriteLine("Press any key...");
            Console.ReadKey();
        }
    }
}
