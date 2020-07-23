using CommandLine;
using PortTunneler.Common;
using System;
using System.Collections.Generic;
using System.Net;

namespace PortTunneler
{
    public class Program
    {
        [Verb("tunnel", HelpText = "Tunnel selected ports to a remote relay")]
        public class TunnelOptions
        {
            [Option('r', HelpText = "Remote relay's hostname", Required = true)]
            public string Relay { get; set; }
            [Option('p', HelpText = "Remote relay's main port", Required = true)]
            public ushort RelayPort { get; set; }
            [Option('o', "open-ports", Required = true, Separator = ',')]
            public IEnumerable<ushort> Ports { get; set; }
        }

        [Verb("relay", HelpText = "Run a relay that will forward selected ports to its tunnel")]
        public class RelayOptions
        {
            [Option('p', HelpText = "The relay's main port", Required = true)]
            public ushort RelayPort { get; set; }
            [Option('m', "map", Required = true, Separator = ';', HelpText = "Port mappings in the format 'target,internal,external;'")]
            public IEnumerable<string> Map { get; set; }
        }

        static int Main(string[] args)
        {
            return CommandLine.Parser.Default.ParseArguments<TunnelOptions, RelayOptions>(args).MapResult<TunnelOptions, RelayOptions, int>(RunTunnel, RunRelay, errs => 1);
        }

        public static int RunRelay(RelayOptions options)
        {
            Console.WriteLine($"Running relay on port {options.RelayPort}");
            var ports = new List<PortMapping>();
            foreach(var mapping in options.Map)
            {
                var split = mapping.Split(',');
                if(split.Length != 3 || !ushort.TryParse(split[0], out var target) || !ushort.TryParse(split[1], out var intern) || !ushort.TryParse(split[2], out var external))
                {
                    Console.WriteLine($"Invalid port mapping - '{mapping}'");
                    return 1;
                }
                ports.Add(new PortMapping(target, intern, external));
                Console.WriteLine($"{target}: ({intern}, {external})");
            }
            var server = new MainServer(IPAddress.Any, options.RelayPort, ports);
            server.Start();
            while (Console.ReadLine() != "q") ;
            return 0;
        }

        public static int RunTunnel(TunnelOptions options)
        {
            Console.WriteLine($"Running tunnel to {options.Relay}:{options.RelayPort}");
            Console.WriteLine("Open ports: ");
            foreach (var port in options.Ports)
                Console.WriteLine("\t" + port);
            var tunnel = new RelayEndpoint(IPAddress.Loopback, options.Relay, options.RelayPort, options.Ports);
            tunnel.Start();
            while (Console.ReadLine() != "q") ;
            return 0;
        }
    }
}
