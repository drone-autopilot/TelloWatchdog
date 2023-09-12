using Prism.Mvvm;
using Reactive.Bindings;
using System.Collections.ObjectModel;
using System.Diagnostics;
using TelloWatchdog.ViewModels.SocketConnection;
using static System.Net.Mime.MediaTypeNames;

namespace TelloWatchdog.ViewModels
{
    public class MainWindowViewModel : BindableBase
    {
        public ReactiveProperty<string> Title { get; } = new ReactiveProperty<string>("TelloWatchdog");

        private ObservableCollection<string> _logs = new ObservableCollection<string>();
        public ObservableCollection<string> Logs
        {
            get => _logs;
            set => SetProperty(ref _logs, value);
        }

        public MainWindowViewModel()
        {
            this.Logs.Add("log1");
            this.Logs.Add("log2");
            this.Logs.Add("log3");
            this.SubscribeCommands();
        }

        private void SubscribeCommands()
        {
            //var sc = new UdpSocketClient("127.0.0.1", 12345);
            //sc.Connect();
            //sc.Send(System.Text.Encoding.ASCII.GetBytes("hogehogehogehoge"));
            //var r = sc.Receive();
            //Debug.Print(System.Text.Encoding.ASCII.GetString(r));
        }
    }
}
