using PortTunneler.Common;
using System;
using System.IO;
using System.Net;
using System.Text;

namespace PortTunneler.Relay
{
    class Program
    {
        static void Main(string[] args)
        {
            var server = new MainServer(IPAddress.Any, new PortMapping[] { new PortMapping(25565, 55565, 55555) });
            server.Start();
            while (true)
                Console.ReadLine();

        }
    }
}
