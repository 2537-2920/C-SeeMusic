using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Net.Http;

namespace SeeMusicApp
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : Application
    {
        protected override async void OnExit(ExitEventArgs e)
        {
            // 程序关闭前调用登出接口
            if (!string.IsNullOrEmpty(ApiClient.AccessToken))
            {
                try
                {
                    var httpClient = new HttpClient();
                    var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost:5000/api/v1/auth/logout");
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ApiClient.AccessToken);
                    await httpClient.SendAsync(request);
                }
                catch { /* 忽略错误 */ }
            }

            base.OnExit(e);
        }
    }
}
