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
            UpdateCacheSizeDisplay();
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
                System.Diagnostics.Debug.WriteLine($"[LoadPreferences] AccessToken: {(string.IsNullOrEmpty(ApiClient.AccessToken) ? "NULL" : "Set")}");
                
                var request = new HttpRequestMessage(HttpMethod.Get, $"{ApiBaseUrl}/users/me/preferences");
                if (!string.IsNullOrEmpty(ApiClient.AccessToken))
                {
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ApiClient.AccessToken);
                }
                
                var response = await _httpClient.SendAsync(request);
                System.Diagnostics.Debug.WriteLine($"[LoadPreferences] Response Status: {response.StatusCode}");
                
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"[LoadPreferences] Response Body: {json}");
                    
                    var apiResponse = JsonConvert.DeserializeObject<ApiResponse<UserPreferencesDto>>(json);
                    
                    if (apiResponse?.Data != null)
                    {
                        UpdateThemeButtonStyle(apiResponse.Data.Theme);
                        UpdateExportFormatCheckboxes(apiResponse.Data.DefaultExportFormats as List<string>);
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("[LoadPreferences] Data is null");
                        SetDefaultExportFormats();
                    }
                }
                else
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"[LoadPreferences] Failed: {response.StatusCode} - {errorBody}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LoadPreferences] Exception: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[LoadPreferences] StackTrace: {ex.StackTrace}");
            }
        }

        private void SetDefaultExportFormats()
        {
            RdoMidi.IsChecked = true;
        }

        private void UpdateExportFormatCheckboxes(List<string> formats)
        {
            if (formats == null || formats.Count == 0)
            {
                SetDefaultExportFormats();
                return;
            }

            var format = formats.FirstOrDefault()?.ToLower();
            
            switch (format)
            {
                case "midi":
                    RdoMidi.IsChecked = true;
                    break;
                case "musicxml":
                    RdoXml.IsChecked = true;
                    break;
                case "pdf":
                    RdoPdf.IsChecked = true;
                    break;
                case "png":
                    RdoPng.IsChecked = true;
                    break;
                default:
                    RdoMidi.IsChecked = true;
                    break;
            }
        }

        private async void ChkExportFormat_Changed(object sender, RoutedEventArgs e)
        {
            await SaveExportFormats();
        }

        private async Task SaveExportFormats()
        {
            if (string.IsNullOrEmpty(ApiClient.AccessToken))
            {
                System.Diagnostics.Debug.WriteLine("[SaveExportFormats] AccessToken is null or empty");
                return;
            }

            try
            {
                // 先获取当前的偏好设置
                var getRequest = new HttpRequestMessage(HttpMethod.Get, $"{ApiBaseUrl}/users/me/preferences");
                getRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ApiClient.AccessToken);
                var getResponse = await _httpClient.SendAsync(getRequest);
                
                string currentTheme = "light-music"; // 默认主题
                
                if (getResponse.IsSuccessStatusCode)
                {
                    var getJson = await getResponse.Content.ReadAsStringAsync();
                    var getApiResponse = JsonConvert.DeserializeObject<ApiResponse<UserPreferencesDto>>(getJson);
                    if (getApiResponse?.Data != null)
                    {
                        currentTheme = getApiResponse.Data.Theme ?? "light-music";
                        System.Diagnostics.Debug.WriteLine($"[SaveExportFormats] Current theme from server: {currentTheme}");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[SaveExportFormats] Failed to get current preferences: {getResponse.StatusCode}");
                }

                // 构建要保存的导出格式列表（单选）
                var formats = new List<string>();
                if (RdoMidi.IsChecked == true) formats.Add("midi");
                else if (RdoXml.IsChecked == true) formats.Add("musicxml");
                else if (RdoPdf.IsChecked == true) formats.Add("pdf");
                else if (RdoPng.IsChecked == true) formats.Add("png");

                // 使用当前主题和新的导出格式保存
                var request = new { theme = currentTheme, defaultExportFormats = formats, syncPreferences = true };
                var json = JsonConvert.SerializeObject(request);
                
                System.Diagnostics.Debug.WriteLine($"[SaveExportFormats] Saving - Theme: {currentTheme}, Formats: {string.Join(",", formats)}");
                
                var httpRequest = new HttpRequestMessage(HttpMethod.Put, $"{ApiBaseUrl}/users/me/preferences");
                httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ApiClient.AccessToken);
                httpRequest.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(httpRequest);
                var responseBody = await response.Content.ReadAsStringAsync();
                
                System.Diagnostics.Debug.WriteLine($"[SaveExportFormats] Response Status: {response.StatusCode}");
                
                if (!response.IsSuccessStatusCode)
                {
                    System.Diagnostics.Debug.WriteLine($"[SaveExportFormats] Save failed: {response.StatusCode} - {responseBody}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[SaveExportFormats] Save successful");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SaveExportFormats] Exception: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[SaveExportFormats] StackTrace: {ex.StackTrace}");
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
                        bar.Background = new SolidColorBrush(Color.FromRgb(0x45, 0x7b, 0x9d));
                    }
                }
            }
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }

        private void BtnGoHome_Click(object sender, RoutedEventArgs e)
        {
            var mainWin = new MainWindow(true, new LoginResponse
            {
                AccessToken = ApiClient.AccessToken,
                User = new UserDto
                {
                    Username = TxtDisplayName.Text,
                    Email = TxtEmail.Text
                }
            });
            mainWin.Show();
            this.Close();
        }

        private void BtnEditAvatar_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Image Files|*.jpg;*.jpeg;*.png;*.gif;*.webp",
                Title = "选择头像图片"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                var filePath = openFileDialog.FileName;
                UploadAvatar(filePath);
            }
        }

        private async void UploadAvatar(string filePath)
        {
            try
            {
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    var ms = new MemoryStream();
                    await fs.CopyToAsync(ms);
                    ms.Position = 0;

                    var content = new MultipartFormDataContent();
                    var fileContent = new ByteArrayContent(ms.ToArray());
                    fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
                    content.Add(fileContent, "file", Path.GetFileName(filePath));

                    var request = new HttpRequestMessage(HttpMethod.Post, $"{ApiBaseUrl}/users/me/avatar")
                    {
                        Content = content
                    };

                    if (!string.IsNullOrEmpty(ApiClient.AccessToken))
                    {
                        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ApiClient.AccessToken);
                    }

                    var response = await _httpClient.SendAsync(request);
                    if (response.IsSuccessStatusCode)
                    {
                        var json = await response.Content.ReadAsStringAsync();
                        var apiResponse = JsonConvert.DeserializeObject<ApiResponse<string>>(json);
                        if (apiResponse?.Data != null)
                        {
                            ImgAvatar.ImageSource = new BitmapImage(new Uri("http://localhost:5000" + apiResponse.Data));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Upload avatar failed: {ex.Message}");
            }
        }

        private void BtnEditProfile_Click(object sender, RoutedEventArgs e)
        {
            var editWindow = new EditProfileWindow(_httpClient, ApiBaseUrl);
            editWindow.ShowDialog();
        }

        private void BtnChangePassword_Click(object sender, RoutedEventArgs e)
        {
            var changePasswordWindow = new ChangePasswordWindow(_httpClient, ApiBaseUrl);
            changePasswordWindow.ShowDialog();
        }

        private async void BtnLogout_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, $"{ApiBaseUrl}/auth/logout");
                if (!string.IsNullOrEmpty(ApiClient.AccessToken))
                {
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ApiClient.AccessToken);
                }

                var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    ApiClient.AccessToken = null;
                    var loginWindow = new LoginWindow();
                    loginWindow.Show();
                    this.Close();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Logout failed: {ex.Message}");
            }
        }

        private void BtnClearCache_Click(object sender, RoutedEventArgs e)
        {
            var cacheSize = GetCacheSize();
            var sizeText = cacheSize > 0 ? $"当前缓存大小：{FormatFileSize(cacheSize)}" : "当前没有缓存文件";
            var result = MessageBox.Show($"确定要清除系统缓存吗？\n{sizeText}", "确认", MessageBoxButton.YesNo, MessageBoxImage.Question);
            
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    var cacheDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SeeMusic", "Cache");
                    if (Directory.Exists(cacheDir))
                    {
                        Directory.Delete(cacheDir, true);
                        MessageBox.Show("缓存已清除", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                        UpdateCacheSizeDisplay();
                    }
                    else
                    {
                        MessageBox.Show("没有找到缓存文件", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"清除缓存失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private string GetCacheDirectory()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SeeMusic", "Cache");
        }

        private long GetCacheSize()
        {
            var cacheDir = GetCacheDirectory();
            if (!Directory.Exists(cacheDir))
            {
                return 0;
            }

            try
            {
                var dirInfo = new DirectoryInfo(cacheDir);
                return dirInfo.EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length);
            }
            catch
            {
                return 0;
            }
        }

        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            int order = 0;
            double size = bytes;
            
            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }
            
            return $"{size:0.##} {sizes[order]}";
        }

        private void UpdateCacheSizeDisplay()
        {
            try
            {
                var cacheSize = GetCacheSize();
                if (TxtCache != null)
                {
                    if (cacheSize > 0)
                    {
                        TxtCache.Text = $"清除系统缓存 ({FormatFileSize(cacheSize)})";
                        TxtCache.Foreground = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44));
                    }
                    else
                    {
                        TxtCache.Text = "清除系统缓存 (无缓存)";
                        TxtCache.Foreground = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));
                    }
                }
            }
            catch
            {
                if (TxtCache != null)
                {
                    TxtCache.Text = "清除系统缓存";
                }
            }
        }

        private void UpdateThemeButtonStyle(string theme)
        {
            var activeBg = new SolidColorBrush(Color.FromRgb(0x45, 0x7b, 0x9d));
            var activeFg = new SolidColorBrush(Colors.White);
            var inactiveBg = new SolidColorBrush(Color.FromRgb(0xFA, 0xFA, 0xFA));
            var inactiveFg = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B));
            var inactiveBorder = new SolidColorBrush(Color.FromRgb(0xE2, 0xE8, 0xF0));

            if (theme == "dark-jazz")
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
            System.Diagnostics.Debug.WriteLine($"[SavePreference] Start - Theme: {theme}");
            System.Diagnostics.Debug.WriteLine($"[SavePreference] AccessToken: {(string.IsNullOrEmpty(ApiClient.AccessToken) ? "NULL" : "Set")}");
            
            if (string.IsNullOrEmpty(ApiClient.AccessToken))
            {
                MessageBox.Show("请先登录后再保存偏好设置", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var request = new { theme = theme, defaultExportFormats = new[] { "midi", "musicxml" }, syncPreferences = true };
                var json = JsonConvert.SerializeObject(request);
                
                System.Diagnostics.Debug.WriteLine($"[SavePreference] Request URL: {ApiBaseUrl}/users/me/preferences");
                System.Diagnostics.Debug.WriteLine($"[SavePreference] Request Body: {json}");
                
                var httpRequest = new HttpRequestMessage(HttpMethod.Put, $"{ApiBaseUrl}/users/me/preferences");
                httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ApiClient.AccessToken);
                httpRequest.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(httpRequest);
                var responseBody = await response.Content.ReadAsStringAsync();

                System.Diagnostics.Debug.WriteLine($"[SavePreference] Response Status: {response.StatusCode}");
                System.Diagnostics.Debug.WriteLine($"[SavePreference] Response Body: {responseBody}");

                if (!response.IsSuccessStatusCode)
                {
                    MessageBox.Show($"保存偏好失败：{response.StatusCode}\n{responseBody}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (HttpRequestException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SavePreference] HttpRequestException: {ex.Message}");
                MessageBox.Show($"网络请求失败，请检查后端服务是否正常运行\n{ex.Message}", "网络错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SavePreference] Exception: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[SavePreference] StackTrace: {ex.StackTrace}");
                MessageBox.Show($"保存偏好异常：{ex.Message}", "异常", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
