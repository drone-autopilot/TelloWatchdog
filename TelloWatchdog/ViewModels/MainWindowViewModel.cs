﻿using Newtonsoft.Json;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using Prism.Mvvm;
using Reactive.Bindings;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TelloWatchdog.Models;
using TelloWatchdog.Models.SocketConnection;
using ZXing;
using ZXing.QrCode;
using ZXing.Windows.Compatibility;

namespace TelloWatchdog.ViewModels
{
    public class MainWindowViewModel : BindableBase
    {
        public ReactiveProperty<string> Title { get; } = new ReactiveProperty<string>("TelloWatchdog");
        public ReactiveProperty<TelloState> TelloState { get; } = new ReactiveProperty<TelloState>(new TelloState());
        public ReactiveProperty<ImageSource> VideoImage { get; } = new ReactiveProperty<ImageSource>();
        public ReactiveProperty<bool> IsConnectedWithAutopilotServer { get; } = new ReactiveProperty<bool>(false);
        public ReactiveProperty<bool> IsSendingCommandToAutopilotServer { get; } = new ReactiveProperty<bool>(false);
        public ReactiveProperty<string> AutopilotServerUDPVideoStreamAddress { get; } = new ReactiveProperty<string>("udp://127.0.0.1:11112");
        public ReactiveProperty<string> AutopilotServerTCPAddressForState { get; } = new ReactiveProperty<string>("127.0.0.1:8990");
        public ReactiveProperty<string> AutopilotServerTCPAddressForCommand { get; } = new ReactiveProperty<string>("127.0.0.1:8989");
        public ReactiveProperty<string> AutopilotServerCommand { get; } = new ReactiveProperty<string>("");
        public ReactiveProperty<string> QRCodeText { get; } = new ReactiveProperty<string>("");
        public ReactiveProperty<ImageSource> QRCodeImage { get; } = new ReactiveProperty<ImageSource>();
        public ReactiveProperty<int> QRCodeImageHeight { get; } = new ReactiveProperty<int>(150);
        public ReactiveProperty<int> QRCodeImageWidth { get; } = new ReactiveProperty<int>(150);
        public ObservableCollection<Log> Logs { get; } = new ObservableCollection<Log>();

        public ReactiveCommand ConnectToAutopilotServerUDPVideoStreamButton_Clicked { get; } = new ReactiveCommand();
        public ReactiveCommand ConnectToAutopilotServerTCPButton_Clicked { get; } = new ReactiveCommand();
        public ReactiveCommand SendCommandToAutopilotServerButton_Clicked { get; } = new ReactiveCommand();
        public ReactiveCommand CopyQRCodeImageButton_Clicked { get; } = new ReactiveCommand();
        public ReactiveCommand<string> CommandButton_Clicked { get; } = new ReactiveCommand<string>();

        private BarcodeWriter BarcodeWriter;

        private readonly int TCPClientTimeout = 5000;
        private readonly int TCPErrorRange = 5;
        private int TCPErrorCount = 0;

        public MainWindowViewModel()
        {
            this.BarcodeWriter = new BarcodeWriter
            {
                Format = BarcodeFormat.QR_CODE,
                Options = new QrCodeEncodingOptions
                {
                    CharacterSet = "UTF-8",
                    Height = this.QRCodeImageHeight.Value,
                    Width = this.QRCodeImageWidth.Value,
                    Margin = 1,
                }
            };

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

        private void TcpClientForState()
        {
            TcpSocketClient sc = null;
            try
            {
                sc = new TcpSocketClient(this.AutopilotServerTCPAddressForState.Value);
            }
            catch (Exception e)
            {
                Application.Current.Dispatcher.Invoke(() => this.WriteLog(Models.LogLevel.Error, e.Message));
                return;
            }

            Application.Current.Dispatcher.Invoke(() => this.WriteLog(Models.LogLevel.Info, $"Connecting to \"{this.AutopilotServerTCPAddressForState.Value}\"..."));
            if (sc.Connect(this.TCPClientTimeout).IsErr(out var connectError))
            {
                Application.Current.Dispatcher.Invoke(() => this.WriteLog(Models.LogLevel.Error, connectError.Message));
                sc.Close();
                return;
            }

            Application.Current.Dispatcher.Invoke(() => this.WriteLog(Models.LogLevel.Info, "Connected!"));

            while (true)
            {
                if (this.TCPErrorCount > this.TCPErrorRange)
                {
                    Application.Current.Dispatcher.Invoke(() => this.WriteLog(Models.LogLevel.Info, "Auto disconnected!"));
                    break;
                }

                // wait for response
                var r = sc.Receive();
                if (r.IsOk(out var res))
                {
                    // tello state
                    try
                    {
                        var state = JsonConvert.DeserializeObject<TelloState>(res);
                        Application.Current.Dispatcher.Invoke(() => this.TelloState.Value = state);
                        this.TCPErrorCount = 0;
                    }
                    catch (JsonException) { }

                    //Application.Current.Dispatcher.Invoke(() => this.Logs.Add(new Log(Models.LogLevel.Info, $"Received: \"{res}\"")));
                }
                else if (r.IsErr(out var resError))
                {
                    // TODO: timeout error
                    Application.Current.Dispatcher.Invoke(() => this.WriteLog(Models.LogLevel.Error, resError.Message));
                    this.TCPErrorCount++;
                }
            }

            sc.Close();
        }

        private void TcpClientForCommand()
        {
            TcpSocketClient sc = null;
            try
            {
                sc = new TcpSocketClient(this.AutopilotServerTCPAddressForCommand.Value);
            }
            catch (Exception e)
            {
                Application.Current.Dispatcher.Invoke(() => this.WriteLog(Models.LogLevel.Error, e.Message));
                return;
            }

            Application.Current.Dispatcher.Invoke(() => this.WriteLog(Models.LogLevel.Info, $"Connecting to \"{this.AutopilotServerTCPAddressForCommand.Value}\"..."));
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
                if (this.TCPErrorCount > this.TCPErrorRange)
                {
                    Application.Current.Dispatcher.Invoke(() => this.WriteLog(Models.LogLevel.Info, "Auto disconnected!"));
                    break;
                }

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
                    this.TCPErrorCount = 0;
                }
                else if (r.IsErr(out var resError))
                {
                    Application.Current.Dispatcher.Invoke(() => this.WriteLog(Models.LogLevel.Error, resError.Message));
                    this.TCPErrorCount++;
                }
            }

            sc.Close();
            Application.Current.Dispatcher.Invoke(() => this.IsConnectedWithAutopilotServer.Value = false);
        }

        private void SubscribeCommands()
        {
            this.ConnectToAutopilotServerUDPVideoStreamButton_Clicked.Subscribe(() => Task.Run(() => this.CaptureUdpVideoStream()));
            this.ConnectToAutopilotServerTCPButton_Clicked.Subscribe(() =>
            {
                Task.Run(() => this.TcpClientForCommand());
                Task.Run(() => this.TcpClientForState());
            });

            this.SendCommandToAutopilotServerButton_Clicked.Subscribe(() =>
            {
                var command = this.AutopilotServerCommand.Value;

                if (command.Length != 0)
                {
                    this.IsSendingCommandToAutopilotServer.Value = true;
                }
            });

            this.CommandButton_Clicked.Subscribe(cmd => this.AutopilotServerCommand.Value = cmd);

            this.QRCodeText.Subscribe(text => 
            {
                if (string.IsNullOrEmpty(text))
                {
                    this.QRCodeImage.Value = null;
                    return;
                }

                using var bitmap = this.BarcodeWriter.Write(text);
                using var ms = new MemoryStream();
                bitmap.Save(ms, ImageFormat.Bmp);
                this.QRCodeImage.Value = BitmapFrame.Create(ms, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            });

            this.CopyQRCodeImageButton_Clicked.Subscribe(() =>
            {
                Clipboard.SetImage((BitmapSource)this.QRCodeImage.Value);
            });
        }
    }
}
