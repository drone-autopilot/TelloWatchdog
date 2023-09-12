using System.Text;
using System.Net;
using System.Net.Sockets;

namespace TelloWatchdog.ViewModels.SocketConnection
{
    public class UdpSocketClient
    {
        private IPAddress IPAddress;
        private int Port;
        private UdpClient UdpClient;

        public UdpSocketClient(string ipAddress, int port)
        {
            this.IPAddress = IPAddress.Parse(ipAddress);
            this.Port = port;
            this.UdpClient = new UdpClient();
        }

        public void Connect()
        {
            this.UdpClient.Connect(this.IPAddress, this.Port);
        }

        public void Send(byte[] data)
        {
            this.UdpClient.Send(data);
        }

        public byte[] Receive()
        {
            IPEndPoint remoteEP = null;
            return this.UdpClient.Receive(ref remoteEP);
        }

        public void Close()
        {
            this.UdpClient.Close();
        }
    }
}
