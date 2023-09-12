using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TelloWatchdog.Models.SocketConnection;

namespace TelloWatchdog.Models
{
    public class Copilot
    {
        private TcpSocketClient TcpClient;
        private UdpSocketClient UdpClient;

        public Copilot(string targetAutopilotServerIPAddress, int tcpPort, int udpPort)
        {
            this.TcpClient = new TcpSocketClient(targetAutopilotServerIPAddress, tcpPort);
            this.UdpClient = new UdpSocketClient(targetAutopilotServerIPAddress, udpPort);
        }
    }
}
