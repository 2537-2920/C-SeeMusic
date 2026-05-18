using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Newtonsoft.Json;

namespace SeeMusicApp
{
    public partial class ChangePasswordWindow : Window
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiBaseUrl;

        public ChangePasswordWindow(HttpClient httpClient, string apiBaseUrl)
        {
            InitializeComponent();
            _httpClient = httpClient;
            _apiBaseUrl = apiBaseUrl;
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed) this.DragMove();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private async void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            var currentPassword = TxtCurrentPassword.Password;
            var newPassword = TxtNewPassword.Password;
            var confirmPassword = TxtConfirmPassword.Password;

            if (string.IsNullOrEmpty(currentPassword))
            {
                MessageBox.Show("请输入当前密码", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrEmpty(newPassword))
            {
                MessageBox.Show("请输入新密码", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (newPassword != confirmPassword)
            {
                MessageBox.Show("两次输入的新密码不一致", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var changeData = new
                {
                    currentPassword = currentPassword,
                    newPassword = newPassword
                };

                var json = JsonConvert.SerializeObject(changeData);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var request = new HttpRequestMessage(HttpMethod.Put, $"{_apiBaseUrl}/users/me/password");
                request.Content = content;

                if (!string.IsNullOrEmpty(ApiClient.AccessToken))
                {
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ApiClient.AccessToken);
                }

                var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    this.DialogResult = true;
                    this.Close();
                }
                else
                {
                    var responseBody = await response.Content.ReadAsStringAsync();
                    MessageBox.Show($"修改失败：{response.StatusCode}\n{responseBody}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"修改异常：{ex.Message}", "异常", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
