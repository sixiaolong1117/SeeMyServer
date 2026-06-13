using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Gaming.Preview.GamesEnumeration;
using System.Net.Http;
using Microsoft.UI.Xaml.Navigation;
using SeeMyServer.Helper;
using SeeMyServer.Models;
using Microsoft.UI.Xaml;

namespace SeeMyServer.Pages
{
    public sealed partial class About : Page
    {
        // 复用 HttpClient 实例（最佳实践）
        private static readonly HttpClient httpClient = new HttpClient();

        public About()
        {
            this.InitializeComponent();

            // 在构造函数或其他适当位置设置版本号
            var package = Package.Current;
            var version = package.Id.Version;

            // {version.Major}.{version.Minor}.{version.Build}.{version.Revision}
            APPVersion.Text = $"{version.Major}.{version.Minor}.{version.Build}";
        }
        private void AboutAliPay_Click(object sender, RoutedEventArgs e)
        {
            AboutAliPayTips.IsOpen = true;
        }
        private void AboutWePay_Click(object sender, RoutedEventArgs e)
        {
            AboutWePayTips.IsOpen = true;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            GetList();
        }
        private async Task<string> HTTPResponse(string http)
        {
            try
            {
                HttpResponseMessage response = await httpClient.GetAsync(http);
                if (response.IsSuccessStatusCode)
                {
                    // 从GitHub的响应中读取文件内容
                    return await response.Content.ReadAsStringAsync();
                }
                else
                {
                    return "";
                }
            }
            catch
            {
                return "";
            }
        }
        private async void GetList()
        {
            string nameList = null;
            try
            {
                nameList = await HTTPResponse("https://raw.githubusercontent.com/SIXiaolong1117/SIXiaolong1117/main/README/Sponsor/List");
            }
            catch (Exception ex)
            {
                try
                {
                    nameList = await HTTPResponse("https://gitee.com/XiaolongSI/SIXiaolong1117/raw/main/README/Sponsor/List");
                }
                catch (Exception ex2)
                {
                    nameList = "无法连接至 Github 或 Gitee。";
                }
            }
            NameList.Text = nameList;
        }
    }
}
