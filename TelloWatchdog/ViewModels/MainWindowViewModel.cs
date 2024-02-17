using LiveCharts;
using LiveCharts.Defaults;
using LiveCharts.Wpf;
using Newtonsoft.Json;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using Prism.Mvvm;
using Reactive.Bindings;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using TelloWatchdog.Models;
using TelloWatchdog.Models.SocketConnection;

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
        public ReactiveProperty<string> TelemetryElapsedTimeLabel { get; } = new ReactiveProperty<string>("<NO CONNECTION>");
        public ReactiveProperty<SeriesCollection> TelemetryXSeriesCollection { get; } = new ReactiveProperty<SeriesCollection>(new SeriesCollection());
        public ReactiveProperty<SeriesCollection> TelemetryYSeriesCollection { get; } = new ReactiveProperty<SeriesCollection>(new SeriesCollection());
        public ReactiveProperty<SeriesCollection> TelemetryZSeriesCollection { get; } = new ReactiveProperty<SeriesCollection>(new SeriesCollection());
        public ReactiveProperty<SeriesCollection> TelemetryAngleSeriesCollection { get; } = new ReactiveProperty<SeriesCollection>(new SeriesCollection());
        public ReactiveProperty<SeriesCollection> TelemetryGlobalSeriesCollection { get; } = new ReactiveProperty<SeriesCollection>(new SeriesCollection());
        public ReactiveProperty<string[]> TelemetryXSeriesLabels { get; } = new ReactiveProperty<string[]>(new string[] {"X.Speed", "X.Accel"});
        public ReactiveProperty<string[]> TelemetryYSeriesLabels { get; } = new ReactiveProperty<string[]>(new string[] { "Y.Speed", "Y.Accel" });
        public ReactiveProperty<string[]> TelemetryZSeriesLabels { get; } = new ReactiveProperty<string[]>(new string[] { "Z.Speed", "Z.Accel" });
        public ReactiveProperty<string[]> TelemetryAngleSeriesLabels { get; } = new ReactiveProperty<string[]>(new string[] { "Pitch", "Roll", "Yaw" });

        public ObservableCollection<Log> Logs { get; } = new ObservableCollection<Log>();

        public ReactiveCommand ConnectToAutopilotServerUDPVideoStreamButton_Clicked { get; } = new ReactiveCommand();
        public ReactiveCommand ConnectToAutopilotServerTCPButton_Clicked { get; } = new ReactiveCommand();
        public ReactiveCommand SendCommandToAutopilotServerButton_Clicked { get; } = new ReactiveCommand();
        public ReactiveCommand<string> CommandButton_Clicked { get; } = new ReactiveCommand<string>();

        private Stopwatch StateConnectionStopWatch = new Stopwatch();

        private readonly int TCPClientTimeout = 5000;
        private readonly int TCPErrorRange = 5;
        private readonly int ParseStateErrorRange = 1000;
        private int TCPClientForStateErrorCount = 0;
        private int FailedToParseStateErrorCount = 0;
        private int TCPClientForCommandErrorCount = 0;

        private int TelemetryGlobalMaxRange = 100;

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
            Application.Current.Dispatcher.Invoke(() => this.WriteLog(Models.LogLevel.Info, $"Video stream: Connecting to \"{address}\"..."));
            var capture = VideoCapture.FromFile(address);
            var frame = new Mat();

            if (capture.IsOpened())
                Application.Current.Dispatcher.Invoke(() => this.WriteLog(Models.LogLevel.Info, "Video stream: Connected!"));

            while (capture.IsOpened())
            {
                if (!capture.Read(frame) || frame.Empty() || frame.ElemSize() == 0)
                { 
                    continue;
                }

                var writableBitmap = frame.ToWriteableBitmap();
                writableBitmap.Freeze();
                Application.Current.Dispatcher.Invoke(() => this.VideoImage.Value = writableBitmap);
            }

            capture.Dispose();

            Application.Current.Dispatcher.Invoke(() => this.WriteLog(Models.LogLevel.Info, "Video stream: Disconnected!"));
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
                Application.Current.Dispatcher.Invoke(() => this.WriteLog(Models.LogLevel.Error, $"State client: {e.Message}"));
                return;
            }

            Application.Current.Dispatcher.Invoke(() => this.WriteLog(Models.LogLevel.Info, $"State client: Connecting to \"{this.AutopilotServerTCPAddressForState.Value}\"..."));
            if (sc.Connect(this.TCPClientTimeout).IsErr(out var connectError))
            {
                Application.Current.Dispatcher.Invoke(() => this.WriteLog(Models.LogLevel.Error, $"State client: {connectError.Message}"));
                sc.Close();
                return;
            }

            Application.Current.Dispatcher.Invoke(() => this.IsConnectedWithAutopilotServer.Value = true);
            Application.Current.Dispatcher.Invoke(() => this.WriteLog(Models.LogLevel.Info, "State client: Connected!"));

            // start stopwatch
            Application.Current.Dispatcher.Invoke(() => this.StateConnectionStopWatch.Start());

            while (this.IsConnectedWithAutopilotServer.Value)
            {
                if (this.TCPClientForStateErrorCount > this.TCPErrorRange || this.FailedToParseStateErrorCount > this.ParseStateErrorRange)
                {
                    break;
                }

                // wait for response
                var r = sc.Receive();
                if (r.IsOk(out var res))
                {
                    // tello state
                    TelloState state = null;
                    try
                    {
                        state = JsonConvert.DeserializeObject<TelloState>(res);
                    }
                    catch (JsonException) { }

                    if (state == null)
                    {
                        this.FailedToParseStateErrorCount++;
                        Application.Current.Dispatcher.Invoke(() => this.WriteLog(Models.LogLevel.Error, $"State client: Failed to parse tello state"));
                        continue;
                    }

                    this.FailedToParseStateErrorCount = 0;
                    Application.Current.Dispatcher.Invoke(() => this.TelloState.Value = state);
                    Application.Current.Dispatcher.Invoke(() => this.UpdateTelemetryMonitor());
                }
                else if (r.IsErr(out var resError))
                {
                    Application.Current.Dispatcher.Invoke(() => this.WriteLog(Models.LogLevel.Error, $"State client: {resError.Message}"));
                    this.TCPClientForStateErrorCount++;
                    continue;
                }

                this.TCPClientForStateErrorCount = 0;
            }

            sc.Close();
            this.TCPClientForStateErrorCount = 0;
            Application.Current.Dispatcher.Invoke(() => this.IsConnectedWithAutopilotServer.Value = false);
            Application.Current.Dispatcher.Invoke(() => this.WriteLog(Models.LogLevel.Info, "State client: Disconnected!"));

            // reset state
            Application.Current.Dispatcher.Invoke(() => this.TelloState.Value = new TelloState());

            // stop stopwatch
            Application.Current.Dispatcher.Invoke(() =>
            {
                this.StateConnectionStopWatch.Stop();
                this.StateConnectionStopWatch.Reset();
                this.ResetTelemetryMonitor();
            });
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
                Application.Current.Dispatcher.Invoke(() => this.WriteLog(Models.LogLevel.Error, $"Command client: {e.Message}"));
                return;
            }

            Application.Current.Dispatcher.Invoke(() => this.WriteLog(Models.LogLevel.Info, $"Command client: Connecting to \"{this.AutopilotServerTCPAddressForCommand.Value}\"..."));
            if (sc.Connect(this.TCPClientTimeout).IsErr(out var connectError))
            {
                Application.Current.Dispatcher.Invoke(() => this.WriteLog(Models.LogLevel.Error, $"Command client: {connectError.Message}"));
                sc.Close();
                return;
            }

            Application.Current.Dispatcher.Invoke(() => this.IsConnectedWithAutopilotServer.Value = true);
            Application.Current.Dispatcher.Invoke(() => this.WriteLog(Models.LogLevel.Info, "Command client: Connected!"));

            while (this.IsConnectedWithAutopilotServer.Value)
            {
                if (this.TCPClientForCommandErrorCount > this.TCPErrorRange)
                {
                    break;
                }

                if (this.IsSendingCommandToAutopilotServer.Value)
                {
                    var command = this.AutopilotServerCommand.Value;
                    Application.Current.Dispatcher.Invoke(() => this.WriteLog(Models.LogLevel.Info, $"Command client: Sending command: \"{command}\""));
                    var result = sc.Send(command + "A");
                    Application.Current.Dispatcher.Invoke(() => this.IsSendingCommandToAutopilotServer.Value = false);

                    if (result.IsErr(out var sendError))
                    {
                        Application.Current.Dispatcher.Invoke(() => this.WriteLog(Models.LogLevel.Error, $"Command client: {sendError.Message}"));
                        this.TCPClientForCommandErrorCount++;
                        continue;
                    }
                }

                // wait for response
                var r = sc.Receive();
                if (r.IsOk(out var res))
                {
                    if (!res.All(c => c == '\0'))
                    {
                        Application.Current.Dispatcher.Invoke(() => this.WriteLog(Models.LogLevel.Info, $"Command client: Received: \"{res}\""));
                    }
                }
                else if (r.IsErr(out var resError))
                {
                    Application.Current.Dispatcher.Invoke(() => this.WriteLog(Models.LogLevel.Error, $"Command client: {resError.Message}"));
                }

                this.TCPClientForCommandErrorCount = 0;
            }

            sc.Close();
            this.TCPClientForCommandErrorCount = 0;
            Application.Current.Dispatcher.Invoke(() => this.IsConnectedWithAutopilotServer.Value = false);
            Application.Current.Dispatcher.Invoke(() => this.WriteLog(Models.LogLevel.Info, "Command client: Disconnected!"));
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
        }

        private void UpdateTelemetryMonitor()
        {
            // elapsed
            var res = this.StateConnectionStopWatch.Elapsed;

            if (res.Milliseconds >= 100)
            {
                return;
            }

            var label = $"{res.Minutes:00}:{res.Seconds:00}:{(res.Milliseconds / 10):00}";
            this.TelemetryElapsedTimeLabel.Value = label;

            var telloSpeeds = this.TelloState.Value.Speeds;
            var telloAccs = this.TelloState.Value.Accelerations;
            var pitch = this.TelloState.Value.Pitch;
            var roll = this.TelloState.Value.Roll;
            var yaw = this.TelloState.Value.Yaw;
            var tof = this.TelloState.Value.TimeOfFlight;

            // x
            this.TelemetryXSeriesCollection.Value.Clear();
            this.TelemetryXSeriesCollection.Value.Add(new RowSeries
            {
                Values = new ChartValues<long> { telloSpeeds.X, telloAccs.X },
            });

            // y
            this.TelemetryYSeriesCollection.Value.Clear();
            this.TelemetryYSeriesCollection.Value.Add(new RowSeries
            {
                Values = new ChartValues<long> { telloSpeeds.Y, telloAccs.Y },
            });

            // z
            this.TelemetryZSeriesCollection.Value.Clear();
            this.TelemetryZSeriesCollection.Value.Add(new RowSeries
            {
                Values = new ChartValues<long> { telloSpeeds.Z, telloAccs.Z },
            });

            // angle
            this.TelemetryAngleSeriesCollection.Value.Clear();
            this.TelemetryAngleSeriesCollection.Value.Add(new RowSeries
            {
                Values = new ChartValues<long> { pitch, roll, yaw },
            });

            // global
            // values[0] -> Speed.X
            // values[1] -> Speed.Y
            // values[2] -> Speed.Z
            // values[3] -> ToF
            var values = this.TelemetryGlobalSeriesCollection.Value;
            var nextV0Value = new ObservablePoint(0, telloSpeeds.X);
            var nextV1Value = new ObservablePoint(0, telloSpeeds.Y);
            var nextV2Value = new ObservablePoint(0, telloSpeeds.Z);
            var nextV3Value = new ObservablePoint(0, tof);


            if (values.Count == 0)
            {
                // v0
                values.Add(new LineSeries
                {
                    Values = new ChartValues<ObservablePoint>() { nextV0Value },
                    Title = "Speed.X"
                });

                // v1
                values.Add(new LineSeries
                {
                    Values = new ChartValues<ObservablePoint>() { nextV1Value },
                    Title = "Speed.Y"
                });

                // v2
                values.Add(new LineSeries
                {
                    Values = new ChartValues<ObservablePoint>() { nextV2Value },
                    Title = "Speed.Z"
                });

                // v3
                values.Add(new LineSeries
                {
                    Values = new ChartValues<ObservablePoint>() { nextV3Value },
                    Title = "ToF"
                });
            }
            else
            {
                var v0 = values[0];
                var v1 = values[1];
                var v2 = values[2];
                var v3 = values[3];

                var valuesMaxCount = new int[] { v0.Values.Count, v1.Values.Count, v2.Values.Count, v3.Values.Count }.Max();

                if (valuesMaxCount == this.TelemetryGlobalMaxRange)
                {
                    v0.Values.RemoveAt(0);
                    v1.Values.RemoveAt(0);
                    v2.Values.RemoveAt(0);
                    v3.Values.RemoveAt(0);
                }

                nextV0Value.X = ((ObservablePoint)v0.Values[v0.Values.Count - 1]).X + 1;
                nextV1Value.X = ((ObservablePoint)v1.Values[v1.Values.Count - 1]).X + 1;
                nextV2Value.X = ((ObservablePoint)v2.Values[v2.Values.Count - 1]).X + 1;
                nextV3Value.X = ((ObservablePoint)v3.Values[v3.Values.Count - 1]).X + 1;

                v0.Values.Add(nextV0Value);
                v1.Values.Add(nextV1Value);
                v2.Values.Add(nextV2Value);
                v3.Values.Add(nextV3Value);
            }
        }

        private void ResetTelemetryMonitor()
        {
            this.TelemetryElapsedTimeLabel.Value = "<DISCONNECTED>";

            this.TelemetryXSeriesCollection.Value.Clear();
            this.TelemetryYSeriesCollection.Value.Clear();
            this.TelemetryZSeriesCollection.Value.Clear();
            this.TelemetryAngleSeriesCollection.Value.Clear();

            // global
            var values = this.TelemetryGlobalSeriesCollection.Value;
            values.Clear();
        }
    }
}
