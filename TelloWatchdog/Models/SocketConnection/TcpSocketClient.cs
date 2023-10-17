using ImTools;
using RustyOptions;
using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace TelloWatchdog.Models.SocketConnection
{
    public class TcpSocketClient
    {
        private IPAddress IPAddress;
        private int Port;
        private TcpClient Client;
        public NetworkStream Stream;

        private byte[] Buffer;

        public TcpSocketClient(string ipAddress, int port)
        {
            this.IPAddress = IPAddress.Parse(ipAddress);
            this.Port = port;
            this.Client = new TcpClient();
            this.Buffer = new byte[1024];
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
            try
            {
                this.Stream.Read(this.Buffer, 0, this.Buffer.Length);
            }
            catch (Exception ex)
            {
                return Result.Err<string, Exception>(ex);
            }

            var sum = 0;
            this.Buffer.ForEach(b => sum += (int)b);

            if (sum == 0)
            {
                return Result.Err<string, Exception>(new Exception("Buffer is all 0"));
            }
            
            return Result.Ok<string, Exception>(Encoding.UTF8.GetString(this.Buffer));
        }

        public void Close()
        {
            this.Client?.Close();
            this.Stream?.Close();
        }
    }
}
