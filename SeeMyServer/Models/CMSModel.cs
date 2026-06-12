using System.ComponentModel;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;

namespace SeeMyServer.Models
{
    public class CMSModel : INotifyPropertyChanged
    {
        private int _numberOfFailures;
        private int _numberOfFailuresSec;
        private string _numberOfFailuresStr;
        private string _osRelease;
        private string _cpuUsage;
        private string _cpuCoreNum;
        private string _cpuUserUsage;
        private string _cpuSysUsage;
        private string _cpuIdleUsage;
        private string _cpuIOUsage;
        private string _average1;
        private string _average5;
        private string _average15;
        private string _average1Percentage;
        private string _average5Percentage;
        private string _average15Percentage;
        private string _memUsage;
        private string _memUsagePageCache;
        private string _memFree;
        private string _memAvailable;
        private string _swapUsage;
        private string _swapCached;
        private string _swapFree;
        private string _swapCachedDisplay;
        private string _netReceived;
        private string _netSent;
        private string _diskRead;
        private string _diskWrite;
        private string _hostName;
        private string _upTime;
        private string _totalMEM;
        private string _totalSwap;
        private string[] _cpuCoreTokens;
        private List<MountInfo> _mountInfos;
        private List<NetworkInterfaceInfo> _networkInterfaceInfos;

        public int Id { get; set; }
        public string Name { get; set; }
        public string HostIP { get; set; }
        public string HostPort { get; set; }
        public string SSHUser { get; set; }
        public string SSHPasswd { get; set; }
        public string SSHKey { get; set; }
        public string SSHKeyId { get; set; }
        public string OSType { get; set; }
        public string SSHKeyIsOpen { get; set; }
        public int NumberOfFailures
        {
            get { return _numberOfFailures; }
            set
            {
                if (_numberOfFailures != value)
                {
                    _numberOfFailures = value;
                    OnPropertyChanged(nameof(NumberOfFailures));
                }
            }
        }
        public int NumberOfFailuresSec
        {
            get { return _numberOfFailuresSec; }
            set
            {
                if (_numberOfFailuresSec != value)
                {
                    _numberOfFailuresSec = value;
                    OnPropertyChanged(nameof(NumberOfFailuresSec));
                }
            }
        }
        public string NumberOfFailuresStr
        {
            get { return _numberOfFailuresStr; }
            set
            {
                if (_numberOfFailuresStr != value)
                {
                    _numberOfFailuresStr = value;
                    OnPropertyChanged(nameof(NumberOfFailuresStr));
                }
            }
        }
        public string OSRelease
        {
            get { return _osRelease; }
            set
            {
                if (_osRelease != value)
                {
                    _osRelease = value;
                    OnPropertyChanged(nameof(OSRelease));
                }
            }
        }
        public string CPUUsage
        {
            get { return _cpuUsage; }
            set
            {
                if (_cpuUsage != value)
                {
                    _cpuUsage = value;
                    OnPropertyChanged(nameof(CPUUsage));
                }
            }
        }
        public string CPUUserUsage
        {
            get { return _cpuUserUsage; }
            set
            {
                if (_cpuUserUsage != value)
                {
                    _cpuUserUsage = value;
                    OnPropertyChanged(nameof(CPUUserUsage));
                }
            }
        }
        public string CPUSysUsage
        {
            get { return _cpuSysUsage; }
            set
            {
                if (_cpuSysUsage != value)
                {
                    _cpuSysUsage = value;
                    OnPropertyChanged(nameof(CPUSysUsage));
                }
            }
        }
        public string CPUIdleUsage
        {
            get { return _cpuIdleUsage; }
            set
            {
                if (_cpuIdleUsage != value)
                {
                    _cpuIdleUsage = value;
                    OnPropertyChanged(nameof(CPUIdleUsage));
                }
            }
        }
        public string CPUIOUsage
        {
            get { return _cpuIOUsage; }
            set
            {
                if (_cpuIOUsage != value)
                {
                    _cpuIOUsage = value;
                    OnPropertyChanged(nameof(CPUIOUsage));
                }
            }
        }
        public string CPUCoreNum
        {
            get { return _cpuCoreNum; }
            set
            {
                if (_cpuCoreNum != value)
                {
                    _cpuCoreNum = value;
                    OnPropertyChanged(nameof(CPUCoreNum));
                }
            }
        }
        public string Average1
        {
            get { return _average1; }
            set
            {
                if (_average1 != value)
                {
                    _average1 = value;
                    OnPropertyChanged(nameof(Average1));
                }
            }
        }
        public string Average5
        {
            get { return _average5; }
            set
            {
                if (_average5 != value)
                {
                    _average5 = value;
                    OnPropertyChanged(nameof(Average5));
                }
            }
        }
        public string Average15
        {
            get { return _average15; }
            set
            {
                if (_average15 != value)
                {
                    _average15 = value;
                    OnPropertyChanged(nameof(Average15));
                }
            }
        }
        public string Average1Percentage
        {
            get { return _average1Percentage; }
            set
            {
                if (_average1Percentage != value)
                {
                    _average1Percentage = value;
                    OnPropertyChanged(nameof(Average1Percentage));
                }
            }
        }
        public string Average5Percentage
        {
            get { return _average5Percentage; }
            set
            {
                if (_average5Percentage != value)
                {
                    _average5Percentage = value;
                    OnPropertyChanged(nameof(Average5Percentage));
                }
            }
        }
        public string Average15Percentage
        {
            get { return _average15Percentage; }
            set
            {
                if (_average15Percentage != value)
                {
                    _average15Percentage = value;
                    OnPropertyChanged(nameof(Average15Percentage));
                }
            }
        }
        public string MEMUsage
        {
            get { return _memUsage; }
            set
            {
                if (_memUsage != value)
                {
                    _memUsage = value;
                    OnPropertyChanged(nameof(MEMUsage));
                }
            }
        }

        public string MEMUsagePageCache
        {
            get { return _memUsagePageCache; }
            set
            {
                if (_memUsagePageCache != value)
                {
                    _memUsagePageCache = value;
                    OnPropertyChanged(nameof(MEMUsagePageCache));
                }
            }
        }

        public string MEMFree
        {
            get { return _memFree; }
            set
            {
                if (_memFree != value)
                {
                    _memFree = value;
                    OnPropertyChanged(nameof(MEMFree));
                }
            }
        }

        public string MEMAvailable
        {
            get { return _memAvailable; }
            set
            {
                if (_memAvailable != value)
                {
                    _memAvailable = value;
                    OnPropertyChanged(nameof(MEMAvailable));
                }
            }
        }

        public string SwapUsage
        {
            get { return _swapUsage; }
            set
            {
                if (_swapUsage != value)
                {
                    _swapUsage = value;
                    OnPropertyChanged(nameof(SwapUsage));
                }
            }
        }

        public string SwapCached
        {
            get { return _swapCached; }
            set
            {
                if (_swapCached != value)
                {
                    _swapCached = value;
                    OnPropertyChanged(nameof(SwapCached));
                }
            }
        }

        public string SwapFree
        {
            get { return _swapFree; }
            set
            {
                if (_swapFree != value)
                {
                    _swapFree = value;
                    OnPropertyChanged(nameof(SwapFree));
                }
            }
        }

        public string SwapCachedDisplay
        {
            get { return _swapCachedDisplay; }
            set
            {
                if (_swapCachedDisplay != value)
                {
                    _swapCachedDisplay = value;
                    OnPropertyChanged(nameof(SwapCachedDisplay));
                }
            }
        }

        public string NETReceived
        {
            get { return _netReceived; }
            set
            {
                if (_netReceived != value)
                {
                    _netReceived = value;
                    OnPropertyChanged(nameof(NETReceived));
                }
            }
        }

        public string NETSent
        {
            get { return _netSent; }
            set
            {
                if (_netSent != value)
                {
                    _netSent = value;
                    OnPropertyChanged(nameof(NETSent));
                }
            }
        }

        public string DISKRead
        {
            get { return _diskRead; }
            set
            {
                if (_diskRead != value)
                {
                    _diskRead = value;
                    OnPropertyChanged(nameof(DISKRead));
                }
            }
        }

        public string DISKWrite
        {
            get { return _diskWrite; }
            set
            {
                if (_diskWrite != value)
                {
                    _diskWrite = value;
                    OnPropertyChanged(nameof(DISKWrite));
                }
            }
        }

        public string HostName
        {
            get { return _hostName; }
            set
            {
                if (_hostName != value)
                {
                    _hostName = value;
                    OnPropertyChanged(nameof(HostName));
                }
            }
        }

        public string UpTime
        {
            get { return _upTime; }
            set
            {
                if (_upTime != value)
                {
                    _upTime = value;
                    OnPropertyChanged(nameof(UpTime));
                }
            }
        }

        public string TotalMEM
        {
            get { return _totalMEM; }
            set
            {
                if (_totalMEM != value)
                {
                    _totalMEM = value;
                    OnPropertyChanged(nameof(TotalMEM));
                }
            }
        }
        public string TotalSwap
        {
            get { return _totalSwap; }
            set
            {
                if (_totalSwap != value)
                {
                    _totalSwap = value;
                    OnPropertyChanged(nameof(TotalSwap));
                }
            }
        }

        private string _topRes;
        public string TopRes
        {
            get { return _topRes; }
            set
            {
                if (_topRes != value)
                {
                    _topRes = value;
                    OnPropertyChanged(nameof(TopRes));
                }
            }
        }

        public ObservableCollection<TopProcessInfo> TopProcesses { get; set; } = new ObservableCollection<TopProcessInfo>();

        private string _linuxKernelVersionRes;
        public string LinuxKernelVersionRes
        {
            get { return _linuxKernelVersionRes; }
            set
            {
                if (_linuxKernelVersionRes != value)
                {
                    _linuxKernelVersionRes = value;
                    OnPropertyChanged(nameof(LinuxKernelVersionRes));
                }
            }
        }

        public string[] CPUCoreTokens
        {
            get { return _cpuCoreTokens; }
            set
            {
                if (_cpuCoreTokens != value)
                {
                    _cpuCoreTokens = value;
                    OnPropertyChanged(nameof(CPUCoreTokens));
                }
            }
        }

        // 为每个CMSModel添加一个信号量，初始化为1，表示一次只允许一个线程进入
        public SemaphoreSlim UpdateSemaphore { get; } = new SemaphoreSlim(1, 1);

        public List<MountInfo> MountInfos
        {
            get { return _mountInfos; }
            set
            {
                if (_mountInfos != value)
                {
                    _mountInfos = value;
                    OnPropertyChanged(nameof(MountInfos));
                }
            }
        }
        public List<NetworkInterfaceInfo> NetworkInterfaceInfos
        {
            get { return _networkInterfaceInfos; }
            set
            {
                if (_networkInterfaceInfos != value)
                {
                    _networkInterfaceInfos = value;
                    OnPropertyChanged(nameof(NetworkInterfaceInfos));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
