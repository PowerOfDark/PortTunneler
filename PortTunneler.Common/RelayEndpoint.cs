using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace PortTunneler.Common
{
    public class RelayEndpoint
    {
        public IPAddress BoundInterface { get; private set; }
        public string RelayAddress { get; private set; }
        public ushort RelayPort { get; private set; }
        public HashSet<ushort> OpenPorts { get; private set; }
        public ProtocolSocket MainSocket { get; private set; }
        public Dictionary<int, ChannelProxy> Channels { get; private set; }
        public bool IsConnected => MainSocket != null && MainSocket.IsConnected;
        private Timer _reconnectTimer;
        public RelayEndpoint(IPAddress boundInterface, string relay, ushort relayPort, IEnumerable<ushort> openPorts)
        {
            BoundInterface = boundInterface;
            RelayAddress = relay;
            RelayPort = relayPort;
            OpenPorts = new HashSet<ushort>(openPorts);
            Channels = new Dictionary<int, ChannelProxy>();
            _reconnectTimer = new Timer(Reconnect, null, Timeout.Infinite, Timeout.Infinite);
        }
        private readonly object _sync = new object();
        private DateTimeOffset _lastConnected;
        private DateTimeOffset _lastPing;
        private DateTimeOffset _lastPong;
        private void Reconnect(object state)
        {
            lock (_sync)
            {
                try
                {

                    var now = DateTimeOffset.Now;

                    if (!IsConnected)
                    {
                        if ((now - _lastConnected).TotalSeconds > 5)
                            Start(true);
                    }
                    else if (_lastPing > _lastPong && (now - _lastPong).TotalSeconds > 20)
                    {
                        MainSocket?.Close();
                    }
                    else if ((now - _lastPing).TotalSeconds > 5)
                    {
                        _lastPing = DateTimeOffset.Now;
                        MainSocket.C2S_KeepAlive();
                    }
                }
                catch { }
                finally
                {
                    _reconnectTimer.Change(100, Timeout.Infinite);
                }
            }
        }

        public void Start(bool fromTimer = false)
        {
            if (IsConnected)
                return;
            lock (_sync)
            {
                MainSocket = null;
                _lastConnected = _lastPing = _lastPong = DateTimeOffset.Now;
                try
                {
                    var socket = TryConnectTcp(RelayAddress, RelayPort);
                    if (socket == null)
                        return;
                    MainSocket = new ProtocolSocket(socket);
                    MainSocket.S2C_OnStartNewConnection = HandleStartNewConnection;
                    MainSocket.S2C_OnKeepAlive = HandleServerKeepAlive;
                    MainSocket.Initialize();
                }
                catch
                {
                }
                finally
                {
                    if (!fromTimer)
                        _reconnectTimer.Change(100, Timeout.Infinite);
                }
            }
        }

        private void HandleServerKeepAlive(ProtocolSocket obj)
        {
            lock (_sync)
            {
                _lastPong = DateTimeOffset.Now;
                Console.WriteLine($"Roundtrip ping: {(_lastPong - _lastPing).TotalMilliseconds:0.00}ms");
            }
        }

        public void Stop()
        {
            lock (_sync)
            {
                _reconnectTimer.Change(Timeout.Infinite, Timeout.Infinite);
                MainSocket?.Close();
            }
        }


        private void HandleStartNewConnection(ProtocolSocket socket, int id, ushort targetPort, ushort internalPort)
        {
            Console.WriteLine($"HandleStartNewConnection #{id} target={targetPort} internal={internalPort}");
            var localSocket = TryConnect(BoundInterface.ToString(), targetPort);
            var remoteSocket = TryConnect(RelayAddress, internalPort);
            var success = localSocket != null && remoteSocket != null;
            if (IsConnected)
            {
                MainSocket.C2S_StartNewConnectionReply(id, internalPort, success);
            }
            var protoSocket = new ProtocolSocket(remoteSocket);
            protoSocket.C2S_StartRelay(id);
            var proxy = new ChannelProxy(localSocket);
            proxy.ChannelId = id;
            proxy.SetProxy(protoSocket);

        }

        public Socket TryConnect(string address, ushort port)
        {
            return TryConnectTcp(address, port);
        }

        public Socket TryConnectTcp(string address, ushort port)
        {
            var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            try
            {
                socket.Connect(address, (int)port);
                return socket;
            }
            catch
            {
            }
            return null;
        }
    }
}
