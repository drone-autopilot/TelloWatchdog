using System.Text;
using System.Net;
using System.Net.Sockets;
using RustyOptions;
using System;

namespace TelloWatchdog.Models.SocketConnection
{
    public class TcpSocketClient
    {
        private IPAddress IPAddress;
        private int Port;
        private TcpClient Client;
        public NetworkStream Stream;

        public TcpSocketClient(string ipAddress, int port)
        {
            this.IPAddress = IPAddress.Parse(ipAddress);
            this.Port = port;
            this.Client = new TcpClient();
        }

        public TcpSocketClient(string address)
        {
            var aap = address.Split(":");
            this.IPAddress = IPAddress.Parse(aap[0]);
            this.Port = int.Parse(aap[1]);
            this.Client = new TcpClient();
        }

        public Result<bool, Exception> Connect(int timeout)
        {
            try
            {
                this.Client.Connect(this.IPAddress, this.Port);
                this.Client.ReceiveTimeout = timeout;
                this.Client.SendTimeout = timeout;
                this.Stream = Client.GetStream();
            }
            catch (Exception ex)
            {
                return Result.Err<bool, Exception>(ex);
            }

            return Result.Ok<bool, Exception>(true);
        }

        public Result<bool, Exception> Send(string text)
        {
            try
            {
                var data = Encoding.UTF8.GetBytes(text);
                this.Stream.Write(data, 0, data.Length);
            }
            catch (Exception ex)
            {
                return Result.Err<bool, Exception>(ex);
            }

            return Result.Ok<bool, Exception>(true);
        }

        public Result<string, Exception> Receive()
        {
            var buffer = new byte[1024];

            try
            {
                this.Stream.Read(buffer, 0, buffer.Length);
            }
            catch (Exception ex)
            {
                return Result.Err<string, Exception>(ex);
            }
            
            return Result.Ok<string, Exception>(Encoding.UTF8.GetString(buffer));
        }

        public void Close()
        {
            this.Client?.Close();
            this.Stream?.Close();
        }
    }
}
