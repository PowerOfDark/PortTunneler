using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PortTunneler.Common
{
    public class ChannelProxy
    {
        public ProtocolSocket Proxy { get; set; }
        public Socket Client { get; set; }
        private NetworkStream _clientStream;
        private byte[] _buffer;
        public int ChannelId { get; set; }
        public bool IsActive { get; set; }
        public bool IsClientConnected { get; set; }

        public delegate void ChannelClosedHandler(ChannelProxy channel, Exception ex);
        public event ChannelClosedHandler Closed;

        public void Close(Exception ex = null)
        {
            Console.WriteLine($"Channel #{ChannelId} closed");
            try
            {
                Proxy?.Socket?.Close();
            }
            catch { }
            try
            {
                Client?.Close();
            }
            catch { }


            IsActive = false;
            Closed?.Invoke(this, ex);
            Closed = null;
            _buffer = null;



        }

        public ChannelProxy(ProtocolSocket proxy)
        {
            SetProxy(proxy);
        }
        public ChannelProxy(Socket client)
        {
            SetClient(client);
        }

        public void SetClient(Socket client)
        {
            if (IsActive)
                return;
            Client = client;
            IsClientConnected = client.Connected;
            _clientStream = new NetworkStream(client, false);
            TryInitialize();
        }

        private void TryInitialize()
        {
            if (IsClientConnected && (Proxy?.IsConnected ?? false) && !IsActive)
            {
                Console.WriteLine($"Channel #{ChannelId} ready");
                _buffer = new byte[65535 - 128];
                IsActive = true;
                Task.Run(ReceiveFromClient);
            }
        }

        private async void ReceiveFromClient()
        {
            int read = 0;
            _settingUp.Wait();
            while (IsActive)
            {
                try
                {
                    read = await _clientStream.ReadAsync(_buffer, 0, _buffer.Length);
                }
                catch (Exception ex)
                {
                    Close(ex);
                }
                if (read == 0)
                    Close();
                else
                    try
                    {
                        Proxy.SendPayload(_buffer, 0, (ushort)read);
                    }
                    catch (Exception ex)
                    {
                        Close(ex);
                    }
            }
        }

        private readonly ManualResetEventSlim _settingUp = new ManualResetEventSlim(false);
        public void SetProxy(ProtocolSocket proxy)
        {
            if (IsActive)
                return;

            _settingUp.Reset();
            Proxy = proxy;
            Proxy.OnSendPayload = HandleSendPayload;
            Proxy.Disconnected += Proxy_Disconnected;

            proxy.Initialize();
            TryInitialize();
            _settingUp.Set();
           
        }

        private void Proxy_Disconnected(ProtocolSocket socket, Exception exception)
        {
            Close(exception);
        }

        private void HandleSendPayload(ProtocolSocket proxy, byte[] buffer, int offset, int count)
        {
            int sent = 0;
            _settingUp.Wait();
            while (IsActive && count > 0)
            {
                try
                {
                    sent = Client.Send(buffer, offset, count, SocketFlags.None);
                }
                catch (Exception ex)
                {
                    Close(ex);
                }
                if (sent == 0)
                    Close();
                else
                {
                    offset += sent;
                    count -= sent;
                }
            }
        }


    }
}
