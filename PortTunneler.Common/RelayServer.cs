using PortTunneler.Common;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace PortTunneler.Common
{
    public class RelayServer
    {
        public MainServer MainServer { get; private set; }
        public ProtocolSocket MainSocket => MainServer.MainSocket;
        public IPAddress ListeningAddress { get; private set; }
        public PortMapping Mapping { get; private set; }

        private TcpListener _externalListener;
        private Thread _externalListenerThread;

        private TcpListener _internalListener;
        private Thread _internalListenerThread;

        public Dictionary<int, ChannelProxy> Channels { get; private set; }
        private int _lastChannelId = 1;
        public ConcurrentDictionary<ProtocolSocket, DateTimeOffset> TemporarySockets { get; private set; }
        public RelayServer(MainServer server, IPAddress boundInterface, PortMapping mapping)
        {
            MainServer = server;
            ListeningAddress = boundInterface;
            Mapping = mapping;
            Channels = new Dictionary<int, ChannelProxy>();
            TemporarySockets = new ConcurrentDictionary<ProtocolSocket, DateTimeOffset>();
        }
        private readonly object _sync = new object();
        public ChannelProxy CreateNewChannel(Socket socket)
        {
            lock(_sync)
            {
                while (Channels.TryGetValue(_lastChannelId, out var channel))
                    _lastChannelId++;
                var ret = Channels[_lastChannelId] = new ChannelProxy(socket);
                ret.ChannelId = _lastChannelId++;
                ret.Closed += Ret_Closed;
                return ret;
            }
        }

        private void Ret_Closed(ChannelProxy channel, Exception ex)
        {
            lock (_sync)
            {
                Channels.Remove(channel.ChannelId);
            }
        }

        public void Start()
        {
            _externalListener = new TcpListener(ListeningAddress, Mapping.ExternalPort);
            _externalListener.Start();
            _externalListenerThread = new Thread(ExternalWorker);
            _externalListenerThread.Start();

            _internalListener = new TcpListener(ListeningAddress, Mapping.InternalPort);
            _internalListener.Start();
            _internalListenerThread = new Thread(InternalWorker);
            _internalListenerThread.Start();
        }

        private async void InternalWorker(object obj)
        {
            while (true)
            {
                var socket = await _internalListener.AcceptSocketAsync();
                _ = Task.Run(() => HandleNewInternalSocket(socket));
            }
        }

        private async void ExternalWorker(object obj)
        {
            while(true)
            {
                var socket = await _externalListener.AcceptSocketAsync();
                _ = Task.Run(() => HandleNewExternalSocket(socket));
            }
        }

        private void HandleNewExternalSocket(Socket socket)
        {
            Console.WriteLine($"New external socket from {socket.RemoteEndPoint}");
            if(MainSocket?.IsConnected??false)
            {
                var channel = CreateNewChannel(socket);
                try
                {
                    MainSocket.S2C_StartNewConnection(channel.ChannelId, Mapping.TargetPort, Mapping.InternalPort);
                }
                catch(Exception ex)
                {
                    socket.Close();
                    channel.Close(ex);
                }
            }
            
        }

        private void HandleNewInternalSocket(Socket socket)
        {
            var protoSocket = new ProtocolSocket(socket);
            protoSocket.Disconnected += ProtoSocket_Disconnected;
            protoSocket.C2S_OnStartRelay = HandleStartRelay;
            protoSocket.Initialize();
            Console.WriteLine($"New internal socket from {socket.RemoteEndPoint}");
        }

        private void HandleStartRelay(ProtocolSocket socket, int id)
        {
            if (!Channels.TryGetValue(id, out var channel))
                return;
            channel.SetProxy(socket);
        }

        private void ProtoSocket_Disconnected(ProtocolSocket socket, Exception exception)
        {
            TemporarySockets.TryRemove(socket, out _);
        }
    }
}
