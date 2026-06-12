using System.ComponentModel;

namespace SeeMyServer.Models
{
    public class TopProcessInfo : INotifyPropertyChanged
    {
        private string _pid;
        private string _user;
        private string _cpuPercent;
        private string _memPercent;
        private string _time;
        private string _command;
        private string _status;
        private string _virtMem;
        private string _resMem;
        private string _sharedMem;

        public string PID { get => _pid; set { _pid = value; OnPropertyChanged(nameof(PID)); } }
        public string User { get => _user; set { _user = value; OnPropertyChanged(nameof(User)); } }
        public string CPUPercent { get => _cpuPercent; set { _cpuPercent = value; OnPropertyChanged(nameof(CPUPercent)); } }
        public string MEMPercent { get => _memPercent; set { _memPercent = value; OnPropertyChanged(nameof(MEMPercent)); } }
        public string Time { get => _time; set { _time = value; OnPropertyChanged(nameof(Time)); } }
        public string Command { get => _command; set { _command = value; OnPropertyChanged(nameof(Command)); } }
        public string Status { get => _status; set { _status = value; OnPropertyChanged(nameof(Status)); } }
        public string VirtMem { get => _virtMem; set { _virtMem = value; OnPropertyChanged(nameof(VirtMem)); } }
        public string ResMem { get => _resMem; set { _resMem = value; OnPropertyChanged(nameof(ResMem)); } }
        public string SharedMem { get => _sharedMem; set { _sharedMem = value; OnPropertyChanged(nameof(SharedMem)); } }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
