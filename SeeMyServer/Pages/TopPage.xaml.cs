using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SeeMyServer.Helper;
using SeeMyServer.Methods;
using SeeMyServer.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.Resources;

namespace SeeMyServer.Pages
{
    public sealed partial class TopPage : Page
    {
        ResourceLoader resourceLoader = new ResourceLoader();
        private DispatcherTimer timer;
        private Logger logger;
        CMSModel dataList;

        private enum SortColumn { None, PID, Command, CPU, MEM, Time, Status }
        private SortColumn _currentSort = SortColumn.None;
        private bool _sortDescending = true;

        public TopPage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            dataList = (CMSModel)e.Parameter;
            base.OnNavigatedTo(e);

            this.DataContext = dataList;

            this.Loaded += Page_Loaded;
            this.Unloaded += Page_Unloaded;

            logger = new Logger(1);
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            var window = App.MainWindow;
            if (window != null)
            {
                window.Activated += Window_Activated;
            }

            timer = new DispatcherTimer();
            Timer_Tick(null, null);
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += Timer_Tick;
            timer.Start();

            if (dataList.UpdateSemaphore.WaitAsync(0).Result)
            {
                dataList.UpdateSemaphore.Release();
            }
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            var window = App.MainWindow;
            if (window != null)
            {
                window.Activated -= Window_Activated;
            }

            if (timer != null)
            {
                timer.Stop();
                timer.Tick -= Timer_Tick;
                timer = null;
            }
        }

        private void Window_Activated(object sender, Microsoft.UI.Xaml.WindowActivatedEventArgs e)
        {
            if (timer != null)
            {
                if (e.WindowActivationState == Microsoft.UI.Xaml.WindowActivationState.Deactivated)
                {
                    timer.Stop();
                }
                else if (e.WindowActivationState == Microsoft.UI.Xaml.WindowActivationState.CodeActivated ||
                         e.WindowActivationState == Microsoft.UI.Xaml.WindowActivationState.PointerActivated)
                {
                    timer.Start();
                }
            }
        }

        private async Task UpdateLinuxCMSModelAsync(CMSModel cmsModel)
        {
            var Usages = Method.GetLinuxCPUUsageAsync(cmsModel);
            await Task.WhenAll(Usages);

            if (Usages.Result != null)
            {
                Method.UpdateCMSModelFromUsageResult(cmsModel, Usages.Result, logger);
            }
            else
            {
                cmsModel.NumberOfFailuresSec = 60;
            }
        }

        private async void Timer_Tick(object sender, object e)
        {
            List<Task> tasks = new List<Task>();
            if (dataList.NumberOfFailuresSec <= 1)
            {
                dataList.NumberOfFailuresStr = $"";
                if (dataList.NumberOfFailures <= 5)
                {
                    dataList.NumberOfFailures = 0;

                    if (await dataList.UpdateSemaphore.WaitAsync(0))
                    {
                        Task updateTask = dataList.OSType switch
                        {
                            "Linux" => UpdateLinuxCMSModelAsync(dataList),
                            _ => Task.CompletedTask
                        };
                        tasks.Add(updateTask);
                    }
                    else
                    {
                        dataList.NumberOfFailures += 1;
                    }
                }
                else
                {
                    dataList.NumberOfFailuresSec = 60;
                    dataList.NumberOfFailures = 0;
                }
            }
            else
            {
                dataList.NumberOfFailuresSec -= 1;
                dataList.NumberOfFailuresStr = $"SSH failed ({dataList.NumberOfFailuresSec})";
            }

            await Task.WhenAll(tasks);
            ApplySort();
        }

        private void ApplySort()
        {
            if (_currentSort == SortColumn.None) return;
            if (dataList.TopProcesses.Count == 0) return;

            var list = dataList.TopProcesses.ToList();

            Func<TopProcessInfo, string> keySelector = _currentSort switch
            {
                SortColumn.PID => p => p.PID,
                SortColumn.Command => p => p.Command ?? "",
                SortColumn.CPU => p => p.CPUPercent ?? "0",
                SortColumn.MEM => p => p.MEMPercent ?? "0",
                SortColumn.Time => p => p.Time ?? "",
                SortColumn.Status => p => p.Status ?? "",
                _ => p => p.PID
            };

            IOrderedEnumerable<TopProcessInfo> sorted;
            if (_currentSort is SortColumn.CPU or SortColumn.MEM or SortColumn.Time)
            {
                Func<TopProcessInfo, double> numSelector = _currentSort switch
                {
                    SortColumn.CPU => p => double.TryParse(p.CPUPercent?.TrimEnd('%'), out double v) ? v : 0,
                    SortColumn.MEM => p => double.TryParse(p.MEMPercent?.TrimEnd('%'), out double v) ? v : 0,
                    SortColumn.Time => p => ParseTimeToSeconds(p.Time),
                    _ => p => 0
                };
                sorted = _sortDescending
                    ? list.OrderByDescending(numSelector)
                    : list.OrderBy(numSelector);
            }
            else if (_currentSort == SortColumn.PID)
            {
                Func<TopProcessInfo, int> intSelector = p => int.TryParse(p.PID, out int v) ? v : 0;
                sorted = _sortDescending
                    ? list.OrderByDescending(intSelector)
                    : list.OrderBy(intSelector);
            }
            else
            {
                sorted = _sortDescending
                    ? list.OrderByDescending(keySelector)
                    : list.OrderBy(keySelector);
            }

            var result = sorted.ToList();
            dataList.TopProcesses.Clear();
            foreach (var p in result)
            {
                dataList.TopProcesses.Add(p);
            }
        }

        private void SetSortIndicator(SortColumn column)
        {
            var indicators = new Dictionary<SortColumn, TextBlock>
            {
                { SortColumn.PID, SortPIDIcon },
                { SortColumn.Command, SortCommandIcon },
                { SortColumn.CPU, SortCPUIcon },
                { SortColumn.MEM, SortMEMIcon },
                { SortColumn.Time, SortTimeIcon },
                { SortColumn.Status, SortStatusIcon },
            };

            foreach (var kv in indicators)
            {
                kv.Value.Text = kv.Key == column ? (_sortDescending ? "\u25BC" : "\u25B2") : "";
            }
        }

        private void ToggleSort(SortColumn column)
        {
            if (_currentSort == column)
            {
                _sortDescending = !_sortDescending;
            }
            else
            {
                _currentSort = column;
                _sortDescending = column == SortColumn.PID;
            }
            SetSortIndicator(column);
            ApplySort();
        }

        private void SortByPID_Click(object sender, RoutedEventArgs e) => ToggleSort(SortColumn.PID);
        private void SortByCommand_Click(object sender, RoutedEventArgs e) => ToggleSort(SortColumn.Command);
        private void SortByCPU_Click(object sender, RoutedEventArgs e) => ToggleSort(SortColumn.CPU);
        private void SortByMEM_Click(object sender, RoutedEventArgs e) => ToggleSort(SortColumn.MEM);
        private void SortByTime_Click(object sender, RoutedEventArgs e) => ToggleSort(SortColumn.Time);
        private void SortByStatus_Click(object sender, RoutedEventArgs e) => ToggleSort(SortColumn.Status);

        private static double ParseTimeToSeconds(string time)
        {
            if (string.IsNullOrEmpty(time)) return 0;
            double totalSeconds = 0;
            int daySplit = time.IndexOf('-');
            if (daySplit >= 0)
            {
                if (int.TryParse(time.Substring(0, daySplit), out int days))
                    totalSeconds += days * 86400;
                time = time.Substring(daySplit + 1);
            }
            var parts = time.Split(':');
            if (parts.Length == 3)
            {
                if (double.TryParse(parts[0], out double h)) totalSeconds += h * 3600;
                if (double.TryParse(parts[1], out double m)) totalSeconds += m * 60;
                if (double.TryParse(parts[2], out double s)) totalSeconds += s;
            }
            else if (parts.Length == 2)
            {
                if (double.TryParse(parts[0], out double m)) totalSeconds += m * 60;
                if (double.TryParse(parts[1], out double s)) totalSeconds += s;
            }
            return totalSeconds;
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            dataList.NumberOfFailuresStr = null;
            dataList.NumberOfFailures = 0;
            dataList.NumberOfFailuresSec = 0;
        }
    }
}
