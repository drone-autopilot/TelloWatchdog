using Prism.Mvvm;
using Reactive.Bindings;
using System.Collections.ObjectModel;
using TelloWatchdog.Models;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Data;
using System.Windows;
using System.Windows.Media.Imaging;

namespace TelloWatchdog.ViewModels
{
    public class MainWindowViewModel : BindableBase
    {
        public ReactiveProperty<string> Title { get; } = new ReactiveProperty<string>("TelloWatchdog");
        public ReactiveProperty<ImageSource> VideoImage { get; } = new ReactiveProperty<ImageSource>();
        public ObservableCollection<Log> Logs { get; } = new ObservableCollection<Log>();

        public ReactiveCommand ConnectToDroneButton_Clicked { get; } = new ReactiveCommand();

        public MainWindowViewModel()
        {
            this.SubscribeCommands();
        }

        private void CaptureUdpVideoStream(string fileName)
        {
            Application.Current.Dispatcher.Invoke(() => this.Logs.Add(new Log(Models.LogLevel.Info, $"Connecting to \"{fileName}\"...")));
            var capture = VideoCapture.FromFile(fileName);
            var frame = new Mat();
            Application.Current.Dispatcher.Invoke(() => this.Logs.Add(new Log(Models.LogLevel.Info, $"Connected!")));

            while (true)
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
        }

        private void SubscribeCommands()
        {
            //var sc = new TcpSocketClient("127.0.0.1", 52001);

            //if (sc.Connect().IsErr(out var e1))
            //{
            //    this.Logs.Add(new Log(Models.LogLevel.Error, e1.Message));
            //    sc.Close();
            //    return;
            //}

            //if (sc.Send("hoge").IsErr(out var e2))
            //{
            //    this.Logs.Add(new Log(Models.LogLevel.Error, e2.Message));
            //    sc.Close();
            //    return;
            //}

            //var r = sc.Receive();

            //if (r.IsOk(out var res))
            //{
            //    this.Logs.Add(new Log(Models.LogLevel.Info, $"Received: \"{res}\""));
            //}
            //else if (r.IsErr(out var e3))
            //{
            //    this.Logs.Add(new Log(Models.LogLevel.Error, e3.Message));
            //    sc.Close();
            //    return;
            //}

            //sc.Close();
            this.ConnectToDroneButton_Clicked.Subscribe(() => Task.Run(() => this.CaptureUdpVideoStream("udp://127.0.0.1:1234")));
        }
    }
}
