using Prism.Mvvm;
using Reactive.Bindings;
using System.Collections.ObjectModel;
using TelloWatchdog.Models;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows;
using System;
using TelloWatchdog.Models.SocketConnection;
using System.Net;

namespace TelloWatchdog.ViewModels
{
    public class MainWindowViewModel : BindableBase
    {
        public ReactiveProperty<string> Title { get; } = new ReactiveProperty<string>("TelloWatchdog");
        public ReactiveProperty<ImageSource> VideoImage { get; } = new ReactiveProperty<ImageSource>();
        public ReactiveProperty<bool> IsConnectedWithAutopilotServer { get; } = new ReactiveProperty<bool>(false);
        public ReactiveProperty<bool> IsSendingCommandToAutopilotServer { get; } = new ReactiveProperty<bool>(false);
        public ReactiveProperty<string> AutopilotServerUDPVideoStreamAddress { get; } = new ReactiveProperty<string>("udp://0.0.0.0:11112");
        public ReactiveProperty<string> AutopilotServerTCPAddress { get; } = new ReactiveProperty<string>("127.0.0.1:8891");
        public ReactiveProperty<string> AutopilotServerCommand { get; } = new ReactiveProperty<string>("");
        public ObservableCollection<Log> Logs { get; } = new ObservableCollection<Log>();

        public ReactiveCommand ConnectToAutopilotServerUDPVideoStreamButton_Clicked { get; } = new ReactiveCommand();
        public ReactiveCommand ConnectToAutopilotServerTCPButton_Clicked { get; } = new ReactiveCommand();
        public ReactiveCommand SendCommandToAutopilotServerButton_Clicked { get; } = new ReactiveCommand();

        private readonly int TCPClientTimeout = 5000;

        public MainWindowViewModel()
        {
            this.SubscribeCommands();
        }

        private void WriteLog(Models.LogLevel logLevel, string message)
        {
            this.Logs.Add(new Log(logLevel, message));
        }

        private void CaptureUdpVideoStream()
        {
            var address = this.AutopilotServerUDPVideoStreamAddress.Value;
            Application.Current.Dispatcher.Invoke(() => this.WriteLog(Models.LogLevel.Info, $"Connecting to \"{address}\"..."));
            var capture = VideoCapture.FromFile(address);
            var frame = new Mat();

            if (capture.IsOpened())
                Application.Current.Dispatcher.Invoke(() => this.WriteLog(Models.LogLevel.Info, "Connected!"));

            while (capture.IsOpened())
            {
                capture.Read(frame);

                if (frame.Empty() || frame.ElemSize() == 0)
                { 
                    continue;
                }

                var writableBitmap = frame.ToWriteableBitmap();
                writableBitmap.Freeze();
                Application.Current.Dispatcher.Invoke(() => this.VideoImage.Value = writableBitmap);
            }

            Application.Current.Dispatcher.Invoke(() => this.WriteLog(Models.LogLevel.Info, "Disconnected!"));
        }

        private void TcpClient()
        {
            TcpSocketClient sc = null;
            try
            {
                sc = new TcpSocketClient(this.AutopilotServerTCPAddress.Value);
            }
            catch (Exception e)
            {
                Application.Current.Dispatcher.Invoke(() => this.WriteLog(Models.LogLevel.Error, e.Message));
                return;
            }

            Application.Current.Dispatcher.Invoke(() => this.WriteLog(Models.LogLevel.Info, $"Connecting to \"{this.AutopilotServerTCPAddress.Value}\"..."));
            if (sc.Connect(this.TCPClientTimeout).IsErr(out var connectError))
            {
                Application.Current.Dispatcher.Invoke(() => this.WriteLog(Models.LogLevel.Error, connectError.Message));
                sc.Close();
                return;
            }

            Application.Current.Dispatcher.Invoke(() => this.IsConnectedWithAutopilotServer.Value = true);
            Application.Current.Dispatcher.Invoke(() => this.WriteLog(Models.LogLevel.Info, "Connected!"));

            while (true)
            {
                if (this.IsSendingCommandToAutopilotServer.Value)
                {
                    var command = this.AutopilotServerCommand.Value;
                    Application.Current.Dispatcher.Invoke(() => this.WriteLog(Models.LogLevel.Info, $"Sending command: \"{command}\""));
                    var result = sc.Send(command);
                    Application.Current.Dispatcher.Invoke(() => this.IsSendingCommandToAutopilotServer.Value = false);

                    if (result.IsErr(out var sendError))
                    {
                        Application.Current.Dispatcher.Invoke(() => this.WriteLog(Models.LogLevel.Error, sendError.Message));
                        break;
                    }
                }

                // wait for response
                var r = sc.Receive();
                if (r.IsOk(out var res))
                {
                    Application.Current.Dispatcher.Invoke(() => this.Logs.Add(new Log(Models.LogLevel.Info, $"Received: \"{res}\"")));
                }
                else if (r.IsErr(out var resError))
                {
                    // TODO: timeout error
                    Application.Current.Dispatcher.Invoke(() => this.WriteLog(Models.LogLevel.Error, resError.Message));
                    //break;
                }
            }

            sc.Close();
            Application.Current.Dispatcher.Invoke(() => this.IsConnectedWithAutopilotServer.Value = false);
        }

        private void SubscribeCommands()
        {
            this.ConnectToAutopilotServerUDPVideoStreamButton_Clicked.Subscribe(() => Task.Run(() => this.CaptureUdpVideoStream()));
            this.ConnectToAutopilotServerTCPButton_Clicked.Subscribe(() => Task.Run(() => this.TcpClient()));

            this.SendCommandToAutopilotServerButton_Clicked.Subscribe(() =>
            {
                var command = this.AutopilotServerCommand.Value;

                if (command.Length != 0)
                {
                    this.IsSendingCommandToAutopilotServer.Value = true;
                }
            });
        }
    }
}
