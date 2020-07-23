using System;
using System.Collections.Generic;
using System.Text;

namespace PortTunneler.Common
{
    public enum PacketType
    {
        Unknown = 0, C2S_KeepAlive = 1, S2C_KeepAlive, C2S_StartRelay, C2S_StopRelay,
        S2C_RelayStatus, S2C_StartNewConnection, C2S_StartNewConnectionReply, 
        SendPayload, S2C_StartNewChannel
    }
}
