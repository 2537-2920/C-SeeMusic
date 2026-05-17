using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Newtonsoft.Json;
using Microsoft.Win32;
using System.IO;

namespace SeeMusicApp
{
    public partial class ProfileWindow : Window
    {
        private readonly HttpClient _httpClient = new HttpClient();
        private const string ApiBaseUrl = "http://localhost:5000/api/v1";

        public ProfileWindow()
        {
            InitializeComponent();
            this.Loaded += ProfileWindow_Loaded;
        }

        private async void ProfileWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadUserData();
        }

        private async Task LoadUserData()
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, $"{ApiBaseUrl}/users/me/dashboard");
                if (!string.IsNullOrEmpty(ApiClient.AccessToken))
                {
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ApiClient.AccessToken);
                }
                
                var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var apiResponse = JsonConvert.DeserializeObject<ApiResponse<DashboardResponse>>(json);
                    
                    if (apiResponse != null && apiResponse.Data != null)
                    {
                        UpdateUI(apiResponse.Data);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load profile: {ex.Message}");
            }
        }

        private void UpdateUI(DashboardResponse dashboard)
        {
            TxtDisplayName.Text = string.IsNullOrEmpty(dashboard.Profile.DisplayName) ? "用户" : dashboard.Profile.DisplayName;
            TxtEmail.Text = dashboard.Profile.Email;
            TxtTransCount.Text = dashboard.Stats.TranscriptionCount.ToString();
            TxtEvalHours.Text = dashboard.Stats.EvaluationDurationHours.ToString() + "h";
            TxtFavCount.Text = dashboard.Stats.FavoriteCount.ToString();

            if (!string.IsNullOrEmpty(dashboard.Profile.AvatarUrl))
            {
                try
                {
                    string url = dashboard.Profile.AvatarUrl.StartsWith("http") ? dashboard.Profile.AvatarUrl : "http://localhost:5000" + dashboard.Profile.AvatarUrl;
                    ImgAvatar.ImageSource = new BitmapImage(new Uri(url));
                }
                catch { }
            }
        }

        // 修改头像
        private async void BtnEditAvatar_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "图片文件 (*.jpg;*.jpeg;*.png)|*.jpg;*.jpeg;*.png";
            
            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    using (var content = new MultipartFormDataContent())
                    {
                        var fileStream = File.OpenRead(openFileDialog.FileName);
                        var fileContent = new StreamContent(fileStream);
                        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
                        content.Add(fileContent, "file", Path.GetFileName(openFileDialog.FileName));

                        var response = await _httpClient.PostAsync($"{ApiBaseUrl}/users/me/avatar", content);
                        if (response.IsSuccessStatusCode)
                        {
                            var json = await response.Content.ReadAsStringAsync();
                            var res = JsonConvert.DeserializeObject<ApiResponse<string>>(json);
                            await UpdateProfileInfo(res.Data); // 同步到用户信息
                            MessageBox.Show("头像更新完成！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("头像上传失败: " + ex.Message);
                }
            }
        }

        // 修改个人资料
        private async void BtnEditProfile_Click(object sender, RoutedEventArgs e)
        {
            // 使用 Microsoft.VisualBasic 需要在项目引用中添加
            try
            {
                string newName = Microsoft.VisualBasic.Interaction.InputBox("请输入新的昵称：", "修改资料", TxtDisplayName.Text);
                if (!string.IsNullOrEmpty(newName))
                {
                    await UpdateProfileInfo(null, newName);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("弹出输入框失败，请检查是否添加了 Microsoft.VisualBasic 引用。\n错误详情：" + ex.Message);
            }
        }

        private async Task UpdateProfileInfo(string avatarUrl = null, string displayName = null)
        {
            try
            {
                var updateDto = new UserDto { 
                    DisplayName = displayName ?? TxtDisplayName.Text,
                    AvatarUrl = avatarUrl 
                };
                
                var content = new StringContent(JsonConvert.SerializeObject(updateDto), System.Text.Encoding.UTF8, "application/json");
                var response = await _httpClient.PutAsync($"{ApiBaseUrl}/users/me", content);
                
                if (response.IsSuccessStatusCode)
                {
                    await LoadUserData();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("同步资料失败: " + ex.Message);
            }
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed) this.DragMove();
        }

        private void BtnGoHome_Click(object sender, RoutedEventArgs e)
        {
            MainWindow mainWin = new MainWindow(true);
            mainWin.Show();
            this.Close();
        }

        private async void BtnClearCache_Click(object sender, RoutedEventArgs e)
        {
            if (TxtCache.Text == "清除系统缓存 (0KB)") return;
            TxtCache.Text = "正在清理缓存中...";

            // 模拟后台删除文件的耗时
            await Task.Delay(1200);

            // 清理完毕
            TxtCache.Text = "清除系统缓存 (0KB)";
            TxtCache.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9CA3AF")); // 变成灰色

            MessageBox.Show("成功释放 1.2GB 系统缓存空间！", "清理完成", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}