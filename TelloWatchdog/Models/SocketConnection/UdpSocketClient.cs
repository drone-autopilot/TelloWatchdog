using System.Text;
using System.Net;
using System.Net.Sockets;
using RustyOptions;
using System;

namespace TelloWatchdog.Models.SocketConnection
{
    public class UdpSocketClient
    {
        private IPAddress IPAddress;
        private int Port;
        private UdpClient Client;

        public UdpSocketClient(string ipAddress, int port)
        {
            this.IPAddress = IPAddress.Parse(ipAddress);
            this.Port = port;
            this.Client = new UdpClient();
        }

        public Result<bool, Exception> Connect()
        {
            try
            {
                this.Client.Connect(IPAddress, Port);
            }
            catch (Exception ex)
            {
                return Result.Err<bool, Exception>(ex);
            }

            return Result.Ok<bool, Exception>(true);
        }

        public Result<bool, Exception> Send(byte[] data)
        {
            try
            {
                this.Client.Send(data);
            }
            catch (Exception ex)
            {
                return Result.Err<bool, Exception>(ex);
            }

            return Result.Ok<bool, Exception>(true);
        }

        public Result<byte[], Exception> Receive()
        {
            IPEndPoint remoteEP = null;
            
            try
            {
                var result = this.Client.Receive(ref remoteEP);
                return Result.Ok<byte[], Exception>(result);
            }
            catch(Exception ex)
            {
                return Result.Err<byte[], Exception>(ex);
            }
        }

        public void Close()
        {
            this.Client?.Close();
        }
    }
}
