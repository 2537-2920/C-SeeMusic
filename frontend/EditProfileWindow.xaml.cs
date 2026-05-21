using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Newtonsoft.Json;

namespace SeeMusicApp
{
    public partial class EditProfileWindow : Window
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiBaseUrl;

        public EditProfileWindow(HttpClient httpClient, string apiBaseUrl)
        {
            InitializeComponent();
            _httpClient = httpClient;
            _apiBaseUrl = apiBaseUrl;

            // 打开弹窗时先加载用户数据
            LoadUserData();
        }

        private async void LoadUserData()
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, $"{_apiBaseUrl}/users/me");
                
                if (!string.IsNullOrEmpty(ApiClient.AccessToken))
                {
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ApiClient.AccessToken);
                }

                var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var apiResponse = JsonConvert.DeserializeObject<ApiResponse<UserDto>>(json);
                    
                    if (apiResponse?.Data != null)
                    {
                        TxtDisplayName.Text = apiResponse.Data.DisplayName;
                        TxtEmail.Text = apiResponse.Data.Email;
                        TxtBio.Text = apiResponse.Data.Bio;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EditProfileWindow] Load user data failed: {ex.Message}");
            }
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
            try
            {
                var updateData = new
                {
                    displayName = TxtDisplayName.Text,
                    email = TxtEmail.Text,
                    bio = TxtBio.Text
                };

                var json = JsonConvert.SerializeObject(updateData);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var request = new HttpRequestMessage(HttpMethod.Put, $"{_apiBaseUrl}/users/me");
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
                    MessageBox.Show($"保存失败：{response.StatusCode}\n{responseBody}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存异常：{ex.Message}", "异常", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
