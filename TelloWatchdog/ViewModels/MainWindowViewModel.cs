using Prism.Mvvm;
using Reactive.Bindings;
using System.Collections.ObjectModel;
using System.Diagnostics;
using TelloWatchdog.Models;
using TelloWatchdog.Models.SocketConnection;

namespace TelloWatchdog.ViewModels
{
    public class MainWindowViewModel : BindableBase
    {
        public ReactiveProperty<string> Title { get; } = new ReactiveProperty<string>("TelloWatchdog");

        private ObservableCollection<Log> _logs = new ObservableCollection<Log>();
        public ObservableCollection<Log> Logs
        {
            get => _logs;
            set => SetProperty(ref _logs, value);
        }

        public MainWindowViewModel()
        {
            this.SubscribeCommands();
        }

        private void SubscribeCommands()
        {
            var sc = new TcpSocketClient("127.0.0.1", 52001);
            
            if (sc.Connect().IsErr(out var e1))
            {
                this.Logs.Add(new Log(LogLevel.Error, e1.Message));
                sc.Close();
                return;
            }

            if (sc.Send("hoge").IsErr(out var e2))
            {
                this.Logs.Add(new Log(LogLevel.Error, e2.Message));
                sc.Close();
                return;
            }

            var r = sc.Receive();

            if (r.IsOk(out var res))
            {
                this.Logs.Add(new Log(LogLevel.Info, $"Received: \"{res}\""));
            }
            else if (r.IsErr(out var e3))
            {
                this.Logs.Add(new Log(LogLevel.Error, e3.Message));
                sc.Close();
                return;
            }

            sc.Close();
        }
    }
}
