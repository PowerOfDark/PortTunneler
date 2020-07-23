using PortTunneler.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PortTunneler.Common
{
    public class MainServer
    {
        public ProtocolSocket MainSocket { get; private set; }
        public IPAddress ListeningAddress { get; private set; }
        public ushort ListeningPort { get; private set; }
        private TcpListener _listener;
        private Thread _listenerThread;
        /// <summary>
        /// Maps internal ports to Relays
        /// </summary>
        public Dictionary<ushort, RelayServer> Relays { get; private set; }
        public MainServer(IPAddress boundInterface, ushort boundPort, IEnumerable<PortMapping> portMap)
        {
            ListeningPort = boundPort;
            ListeningAddress = boundInterface;
            Relays = new Dictionary<ushort, RelayServer>();
            foreach(var port in portMap)
            {
                var server = Relays[port.InternalPort] = new RelayServer(this, boundInterface, port);
                server.Start();
            }
        }

        public void Start()
        {
            _listener = new TcpListener(ListeningAddress, ListeningPort);
            _listener.Start();
            _listenerThread = new Thread(ListenerWorker);
            _listenerThread.Start();
        }

        private async void ListenerWorker(object obj)
        {
            while (true)
            {
                var socket = await _listener.AcceptSocketAsync();
                _ = Task.Run(() => HandleNewSocket(socket));
            }
        }
        private DateTimeOffset _lastPing;
        private void HandleNewSocket(Socket socket)
        {
            if (MainSocket == null || !MainSocket.IsConnected || (DateTimeOffset.Now - _lastPing).TotalSeconds > 15)
            {
                Console.WriteLine($"Replacing the old socket with one from {socket.RemoteEndPoint}");
                if (MainSocket != null)
                    MainSocket.Close();
                _lastPing = DateTimeOffset.Now;
                MainSocket = new ProtocolSocket(socket);
                MainSocket.C2S_OnStartNewConnectionReply = HandleConnectionReply;
                MainSocket.C2S_OnKeepAlive = HandleKeepAlive;
                MainSocket.Initialize();

            }
            else
            {
                socket.Close();
            }
        }

        private void HandleKeepAlive(ProtocolSocket obj)
        {
            Console.WriteLine("Sending keep alive");
            _lastPing = DateTimeOffset.Now;
            obj.S2C_KeepAlive();
        }

        private void HandleConnectionReply(ProtocolSocket socket, int id, ushort internalPort, bool success)
        {
            Console.WriteLine($"HandleConnectionReply id={id}, internal={internalPort}, success={success}");
            if (!Relays.TryGetValue(internalPort, out var relay))
                return;
            if (!relay.Channels.TryGetValue(id, out var channel))
                return;
            if (channel.IsActive)
                return;
            if (!success)
            {
                channel.Close();
            }
        }
    }
}
