using System.Text;
using System.Net;
using System.Net.Sockets;

namespace TelloWatchdog.ViewModels.SocketConnection
{
    public class TcpSocketClient
    {
        private IPAddress IPAddress;
        private int Port;
        private TcpClient TcpClient;
        private NetworkStream Stream;

        public TcpSocketClient(string ipAddress, int port)
        {
            this.IPAddress = IPAddress.Parse(ipAddress);
            this.Port = port;
            this.TcpClient = new TcpClient();
        }

        public void Connect()
        {
            this.TcpClient.Connect(this.IPAddress, this.Port);
            this.Stream  = this.TcpClient.GetStream();
        }

        public void Send(string text)
        {
            var data = Encoding.UTF8.GetBytes(text);
            this.Stream.Write(data, 0, data.Length);
        }

        public string Receive()
        {
            var buffer = new byte[1024];
            this.Stream.Read(buffer, 0, buffer.Length);
            return Encoding.UTF8.GetString(buffer);
        }

        public void Close()
        {
            this.TcpClient.Close();
            this.Stream.Close();
        }
    }
}
