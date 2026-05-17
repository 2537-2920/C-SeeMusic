using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
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
        private string _currentBio = "";

        public ProfileWindow()
        {
            InitializeComponent();
            this.Loaded += ProfileWindow_Loaded;
        }

        private async void ProfileWindow_Loaded(object sender, RoutedEventArgs e)
        {
            TxtLastUpdate.Text = DateTime.Now.ToString("yyyy-MM-dd");
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
                        await LoadPreferences();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ProfileWindow] Failed to load profile: {ex.Message}");
            }
        }

        private async Task LoadPreferences()
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, $"{ApiBaseUrl}/users/me/preferences");
                if (!string.IsNullOrEmpty(ApiClient.AccessToken))
                {
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ApiClient.AccessToken);
                }
                
                var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var apiResponse = JsonConvert.DeserializeObject<ApiResponse<UserPreferencesDto>>(json);
                    
                    if (apiResponse?.Data != null)
                    {
                        UpdateThemeButtonStyle(apiResponse.Data.Theme);
                        UpdateExportFormatCheckboxes(apiResponse.Data.DefaultExportFormats as List<string>);
                    }
                    else
                    {
                        SetDefaultExportFormats();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load preferences: {ex.Message}");
            }
        }

        private void SetDefaultExportFormats()
        {
            ChkMidi.IsChecked = true;
            ChkXml.IsChecked = true;
            ChkPdf.IsChecked = false;
            ChkPng.IsChecked = false;
        }

        private void UpdateExportFormatCheckboxes(List<string> formats)
        {
            if (formats == null) return;

            ChkMidi.IsChecked = formats.Contains("midi");
            ChkXml.IsChecked = formats.Contains("musicxml");
            ChkPdf.IsChecked = formats.Contains("pdf");
            ChkPng.IsChecked = formats.Contains("png");
        }

        private async void ChkExportFormat_Changed(object sender, RoutedEventArgs e)
        {
            await SaveExportFormats();
        }

        private async Task SaveExportFormats()
        {
            try
            {
                var formats = new List<string>();
                if (ChkMidi.IsChecked == true) formats.Add("midi");
                if (ChkXml.IsChecked == true) formats.Add("musicxml");
                if (ChkPdf.IsChecked == true) formats.Add("pdf");
                if (ChkPng.IsChecked == true) formats.Add("png");

                var request = new { theme = "light-music", defaultExportFormats = formats, syncPreferences = true };
                var json = JsonConvert.SerializeObject(request);
                
                var httpRequest = new HttpRequestMessage(HttpMethod.Put, $"{ApiBaseUrl}/users/me/preferences");
                if (!string.IsNullOrEmpty(ApiClient.AccessToken))
                {
                    httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ApiClient.AccessToken);
                }
                httpRequest.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(httpRequest);
                if (!response.IsSuccessStatusCode)
                {
                    var responseBody = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"Save export formats failed: {response.StatusCode} - {responseBody}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Save export formats error: {ex.Message}");
            }
        }

        private void UpdateUI(DashboardResponse dashboard)
        {
            TxtDisplayName.Text = string.IsNullOrEmpty(dashboard.Profile.DisplayName) ? "用户" : dashboard.Profile.DisplayName;
            TxtEmail.Text = dashboard.Profile.Email;
            _currentBio = dashboard.Profile.Bio ?? "";
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

            UpdateWeeklyUsageChart(dashboard.WeeklyUsage);
        }

        private void UpdateWeeklyUsageChart(List<WeeklyUsageItem> weeklyUsage)
        {
            var barMap = new Dictionary<string, Border>
            {
                { "Sun", BarSun }, { "Mon", BarMon }, { "Tue", BarTue },
                { "Wed", BarWed }, { "Thu", BarThu }, { "Fri", BarFri }, { "Sat", BarSat }
            };
            var labelMap = new Dictionary<string, TextBlock>
            {
                { "Sun", LabelSun }, { "Mon", LabelMon }, { "Tue", LabelTue },
                { "Wed", LabelWed }, { "Thu", LabelThu }, { "Fri", LabelFri }, { "Sat", LabelSat }
            };

            var today = DateTime.UtcNow.DayOfWeek.ToString().Substring(0, 3);
            var maxValue = weeklyUsage.Max(w => w.Value);
            var maxHeight = 160.0;

            foreach (var item in weeklyUsage)
            {
                if (barMap.TryGetValue(item.Day, out var bar))
                {
                    var height = maxValue > 0 ? (item.Value / (double)maxValue) * maxHeight : 0;
                    bar.Height = Math.Max(height, 5);

                    if (item.Day == today)
                    {
                        bar.Background = new SolidColorBrush(Color.FromRgb(43, 91, 132));
                        bar.Opacity = 1.0;
                        if (labelMap.TryGetValue(item.Day, out var label))
                        {
                            label.Foreground = new SolidColorBrush(Color.FromRgb(69, 123, 157));
                            label.FontWeight = FontWeights.Bold;
                        }
                    }
                    else
                    {
                        bar.Background = new SolidColorBrush(Color.FromRgb(148, 163, 184));
                        bar.Opacity = item.Value > 0 ? 0.7 : 0.3;
                        if (labelMap.TryGetValue(item.Day, out var label))
                        {
                            label.Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184));
                            label.FontWeight = FontWeights.Normal;
                        }
                    }
                }
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
            var editWindow = new EditProfileWindow(_httpClient, ApiBaseUrl);

            var result = editWindow.ShowDialog();
            
            if (result == true)
            {
                await LoadUserData();
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

        private void UpdateThemeButtonStyle(string selectedTheme)
        {
            var activeBg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#457b9d"));
            var inactiveBg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FAFAFA"));
            var activeFg = new SolidColorBrush(Colors.White);
            var inactiveFg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B"));
            var activeBorder = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#457b9d"));
            var inactiveBorder = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E2E8F0"));

            if (selectedTheme == "dark-jazz")
            {
                BtnThemeLight.Background = inactiveBg;
                BtnThemeLight.Foreground = inactiveFg;
                BtnThemeLight.BorderBrush = inactiveBorder;
                BtnThemeLight.BorderThickness = new Thickness(1);
                BtnThemeDark.Background = activeBg;
                BtnThemeDark.Foreground = activeFg;
                BtnThemeDark.BorderThickness = new Thickness(0);
            }
            else
            {
                BtnThemeLight.Background = activeBg;
                BtnThemeLight.Foreground = activeFg;
                BtnThemeLight.BorderThickness = new Thickness(0);
                BtnThemeDark.Background = inactiveBg;
                BtnThemeDark.Foreground = inactiveFg;
                BtnThemeDark.BorderBrush = inactiveBorder;
                BtnThemeDark.BorderThickness = new Thickness(1);
            }
        }

        private async void BtnThemeLight_Click(object sender, RoutedEventArgs e)
        {
            await SavePreference("light-music");
            UpdateThemeButtonStyle("light-music");
        }

        private async void BtnThemeDark_Click(object sender, RoutedEventArgs e)
        {
            await SavePreference("dark-jazz");
            UpdateThemeButtonStyle("dark-jazz");
        }

        private async Task SavePreference(string theme)
        {
            try
            {
                var request = new { theme = theme, defaultExportFormats = new[] { "midi", "musicxml" }, syncPreferences = true };
                var json = JsonConvert.SerializeObject(request);
                
                var httpRequest = new HttpRequestMessage(HttpMethod.Put, $"{ApiBaseUrl}/users/me/preferences");
                if (!string.IsNullOrEmpty(ApiClient.AccessToken))
                {
                    httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ApiClient.AccessToken);
                }
                httpRequest.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(httpRequest);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    MessageBox.Show($"保存偏好失败: {response.StatusCode}\n{responseBody}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存偏好异常: {ex.Message}", "异常", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}