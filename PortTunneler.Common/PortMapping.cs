using System;
using System.Collections.Generic;
using System.Text;

namespace PortTunneler.Common
{
    public struct PortMapping
    {
        public ushort InternalPort;
        public ushort ExternalPort;
        public ushort TargetPort;

        public PortMapping(ushort targetPort, ushort internalPort, ushort externalPort)
        {
            TargetPort = targetPort;
            InternalPort = internalPort;
            ExternalPort = externalPort;
        }
    }
}
