using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PortTunneler.Common
{
    public class ProtocolSocket
    {
        public Socket Socket { get; private set; }
        public bool IsConnected { get; private set; }
        public const int MaxBufferSize = 65536 + 128;

        private BufferMemoryStream _sendStream;
        private byte[] _sendBuffer;

        private BufferMemoryStream _receiveStream;
        private byte[] _receiveBuffer;

        private readonly NetworkStream _stream;

        public delegate void DisconnectEventHandler(ProtocolSocket socket, Exception exception);
        public event DisconnectEventHandler Disconnected;

        public Dictionary<PacketType, Action> Decoders = new Dictionary<PacketType, Action>();

        public delegate void SendPayloadHandler(ProtocolSocket socket, byte[] buffer, int offset, int count);
        public SendPayloadHandler OnSendPayload { get; set; }
        public Action<ProtocolSocket> C2S_OnKeepAlive { get; set; }
        public delegate void S2C_OnStartNewConnectionHandler(ProtocolSocket socket, int id, ushort targetPort, ushort internalPort);
        public S2C_OnStartNewConnectionHandler S2C_OnStartNewConnection { get; set; }
        public Action<ProtocolSocket, int> C2S_OnStartRelay { get; set; }
        public delegate void C2S_StartNewConnectionReplyHandler(ProtocolSocket socket, int id, ushort internalPort, bool success);
        public C2S_StartNewConnectionReplyHandler C2S_OnStartNewConnectionReply { get; set; }
        public Action<ProtocolSocket> S2C_OnKeepAlive { get; set; }

        public ProtocolSocket(Socket socket)
        {
            Socket = socket;

            _stream = new NetworkStream(socket, false);

            _sendStream = new BufferMemoryStream(_sendBuffer = new byte[MaxBufferSize]);

            _receiveStream = new BufferMemoryStream(_receiveBuffer = new byte[MaxBufferSize]);

            Decoders[PacketType.SendPayload] = () => OnSendPayload?.Invoke(this, _receiveBuffer, HeaderLength, _contentLength);
            Decoders[PacketType.C2S_KeepAlive] = () => C2S_OnKeepAlive?.Invoke(this);
            Decoders[PacketType.S2C_StartNewConnection] = () => S2C_OnStartNewConnection?.Invoke(this, _receiveStream.ReadInt32(), _receiveStream.ReadUInt16(), _receiveStream.ReadUInt16());
            Decoders[PacketType.C2S_StartRelay] = () => C2S_OnStartRelay?.Invoke(this, _receiveStream.ReadInt32());
            Decoders[PacketType.C2S_StartNewConnectionReply] = () => C2S_OnStartNewConnectionReply?.Invoke(this, _receiveStream.ReadInt32(), _receiveStream.ReadUInt16(), _receiveStream.ReadBoolean());
            Decoders[PacketType.S2C_KeepAlive] = () => S2C_OnKeepAlive?.Invoke(this);
        }

        public void Close()
        {
            this.Close(null);
        }
        protected void Close(Exception ex = null)
        {
            IsConnected = false;
            try
            {
                Socket?.Close();
            }
            catch { }


            Disconnected?.Invoke(this, ex);
            Disconnected = null;
            Decoders = null;
            _sendBuffer = _receiveBuffer = null;
            _sendStream = _receiveStream = null;
            OnSendPayload = null;
            C2S_OnKeepAlive = null;
            C2S_OnStartNewConnectionReply = null;
            S2C_OnStartNewConnection = null;
            C2S_OnStartRelay = null;

        }

        public void Initialize()
        {
            if (IsConnected)
                return;
            IsConnected = Socket.Connected;
            Task.Factory.StartNew(Listen, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        const int HeaderLength = sizeof(byte) + sizeof(ushort);
        private int _contentLength = 0;
        protected async void Listen()
        {
            int left = HeaderLength;
            int read = 0;
            int offset = 0;
            PacketType packetType = default;
            _receiveStream.Position = 0;
            while (IsConnected)
            {
                try
                {
                    if (left == 0)
                    {
                        if (packetType == default)
                        {
                            packetType = (PacketType)_receiveStream.ReadByte();
                            _contentLength = _receiveStream.ReadUInt16();
                            left = _contentLength;
                            continue;
                        }
                        if (Decoders.TryGetValue(packetType, out var handler))
                        {
                            handler();
                        }
                        packetType = default;
                        _contentLength = 0;
                        _receiveStream.Position = 0;
                        offset = 0;
                        left = HeaderLength;
                    }
                    var toRead = (int)Math.Min(_receiveBuffer.Length - offset, left);
                    try
                    {
                        read = await _stream.ReadAsync(_receiveBuffer, (int)offset, toRead);
                    }
                    catch (Exception ex)
                    {
                        Close(ex);
                    }

                    if (read == 0)
                        Close();
                    left -= read;
                    offset += read;
                }
                catch(Exception ex)
                {
                    if(IsConnected)
                        Close(ex);
                }
            }
        }
        private ushort _sendLength = 0;
        private void BeginSend(PacketType type, ushort length = 0)
        {
            _sendStream.Position = 0;
            _sendStream.Write((byte)type);
            _sendStream.Write(length);
            _sendLength = checked((ushort)(length + sizeof(byte) + sizeof(ushort)));
        }

        private void Send()
        {
            int count = _sendLength;
            int index = 0;
            int sent = 0;
            while (count > 0)
            {
                try
                {
                    sent = Socket.Send(_sendBuffer, index, count, SocketFlags.None);
                }
                catch (Exception ex)
                {
                    Close(ex);
                    return;
                }
                if (sent == 0)
                {
                    Close();
                    return;
                }
                index += sent;
                count -= sent;
            }
        }

        public void SendPayload(byte[] payload, int offset, ushort count)
        {
            lock (_sendStream)
            {
                BeginSend(PacketType.SendPayload, count);
                Buffer.BlockCopy(payload, offset, _sendBuffer, (int)_sendStream.Position, count);
                Send();
            }
        }

        public void C2S_KeepAlive()
        {
            lock (_sendStream)
            {
                BeginSend(PacketType.C2S_KeepAlive);
                Send();
            }
        }
        public void S2C_KeepAlive()
        {
            lock (_sendStream)
            {
                BeginSend(PacketType.S2C_KeepAlive);
                Send();
            }
        }

        public void S2C_StartNewConnection(int id, ushort targetPort, ushort internalPort)
        {
            lock (_sendStream)
            {
                BeginSend(PacketType.S2C_StartNewConnection, sizeof(int) + sizeof(ushort)*2);
                _sendStream.Write(id);
                _sendStream.Write(targetPort);
                _sendStream.Write(internalPort);
                Send();
            }
        }
        public void C2S_StartNewConnectionReply(int id, ushort internalPort, bool success)
        {
            lock (_sendStream)
            {
                BeginSend(PacketType.C2S_StartNewConnectionReply, sizeof(int) + sizeof(ushort) + sizeof(bool));
                _sendStream.Write(id);
                _sendStream.Write(internalPort);
                _sendStream.Write(success);
                Send();
            }
        }
        public void C2S_StartRelay(int id)
        {
            lock (_sendStream)
            {
                BeginSend(PacketType.C2S_StartRelay, sizeof(int));
                _sendStream.Write(id);
                Send();
            }
        }

    }
}
