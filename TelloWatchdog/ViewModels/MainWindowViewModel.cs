using Prism.Mvvm;
using Reactive.Bindings;
using System.Collections.ObjectModel;
using System.Diagnostics;
using TelloWatchdog.ViewModels.SocketConnection;

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
            //var sc = new TcpSocketClient("127.0.0.1", 52001);
            //sc.Connect();
            //sc.Send("hoge");
            //var r = sc.Receive();
            //Debug.Print(r);
        }
    }
}
