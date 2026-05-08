using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
// Assuming NewtonSoft.Json is available as it's standard. 
// If not, the user can add it or use a different parser.
using Newtonsoft.Json; 

namespace SeeMusicApp
{
    public partial class CommunityWindow : Window
    {
        private const string ApiBaseUrl = "http://localhost:5000/api/v1/community";
        private readonly HttpClient _httpClient = new HttpClient();

        public CommunityWindow()
        {
            InitializeComponent();
            this.Loaded += CommunityWindow_Loaded;
        }

        private async void CommunityWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadScores();
        }

        private async Task LoadScores(string category = null)
        {
            try
            {
                string url = $"{ApiBaseUrl}/scores";
                if (!string.IsNullOrEmpty(category)) url += $"?category={Uri.EscapeDataString(category)}";

                var response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var result = JsonConvert.DeserializeObject<ApiResponse<List<CommunityScore>>>(json);
                    
                    if (result?.Data != null)
                    {
                        ScoresItemsControl.ItemsSource = result.Data;
                    }
                }
                else
                {
                    // Fallback to mock if API not running or DB empty
                    ShowMockData();
                }
            }
            catch (Exception)
            {
                ShowMockData();
            }
        }

        private void ShowMockData()
        {
            var mockScores = new List<CommunityScore>
            {
                new CommunityScore { Id = 1, Title = "晴天", ArtistName = "周杰伦", ArrangementTag = "钢琴版", PriceCent = 600, DownloadCount = 1200, CommentCount = 42 },
                new CommunityScore { Id = 2, Title = "G线上的咏叹调", ArtistName = "巴赫", ArrangementTag = "小提琴", PriceCent = 0, DownloadCount = 856, CommentCount = 15 },
                new CommunityScore { Id = 3, Title = "那些年", ArtistName = "胡夏", ArrangementTag = "简易钢琴", PriceCent = 500, DownloadCount = 2300, CommentCount = 88 }
            };
            ScoresItemsControl.ItemsSource = mockScores;
        }

        // 窗口无边框拖拽
        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }

        // 统一导航栏：返回主页
        private void BtnGoHome_Click(object sender, RoutedEventArgs e)
        {
            MainWindow mainWin = new MainWindow(true);
            mainWin.Show();
            this.Close();
        }

        // 上传按钮点击
        private async void BtnUpload_Click(object sender, RoutedEventArgs e)
        {
            var uploadWin = new UploadScoreWindow();
            uploadWin.Owner = this;
            if (uploadWin.ShowDialog() == true)
            {
                // 如果上传成功，刷新列表
                await LoadScores();
            }
        }

        // 乐谱卡片点击：联动右侧详情面板
        private async void ScoreCard_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border clickedCard && clickedCard.Tag is CommunityScore score)
            {
                // 设置标题、作者
                DetailTitle.Text = score.Title;
                DetailAuthor.Text = $"{score.OwnerName} · 发布于 {score.CreatedAt:yyyy-MM-dd}";

                // 实时展示统计数据 (三连看板)
                DetailStatLikes.Text = score.FavoriteCount >= 1000 ? $"{(score.FavoriteCount / 1000.0):F1}k" : score.FavoriteCount.ToString();
                DetailStatDownloads.Text = score.DownloadCountDisplay;
                DetailStatShares.Text = score.ShareCount.ToString();

                // 加载封面
                if (score.HasCover)
                {
                    DetailCoverImage.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(score.FullCoverUrl));
                    DetailCoverImage.Visibility = Visibility.Visible;
                    DetailCoverText.Visibility = Visibility.Collapsed;
                }
                else
                {
                    DetailCoverImage.Visibility = Visibility.Collapsed;
                    DetailCoverText.Visibility = Visibility.Visible;
                    DetailCoverText.Text = score.TitleAbbreviation;
                }

                // 切换显示状态
                DetailEmptyState.Visibility = Visibility.Collapsed;
                DetailContentState.Visibility = Visibility.Visible;

                // 进一步加载详细评论和最新数据
                await LoadScoreDetails(score.Id);
            }
        }

        private async Task LoadScoreDetails(int scoreId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{ApiBaseUrl}/scores/{scoreId}");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var result = JsonConvert.DeserializeObject<ApiResponse<CommunityScoreDetail>>(json);
                    
                    if (result?.Data != null)
                    {
                        var data = result.Data;
                        DetailContentState.DataContext = data;
                        
                        // 确保统计数据保持最新
                        DetailStatLikes.Text = data.FavoriteCount >= 1000 ? $"{(data.FavoriteCount / 1000.0):F1}k" : data.FavoriteCount.ToString();
                        DetailStatDownloads.Text = data.DownloadCountDisplay;
                        DetailStatShares.Text = data.ShareCount.ToString();

                        CommentsItemsControl.ItemsSource = data.RecentComments;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error loading details: " + ex.Message);
            }
        }

        // 发送评论按钮
        private async void BtnSendComment_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(CommentInput.Text)) return;
            if (DetailContentState.DataContext is CommunityScoreDetail detail)
            {
                try
                {
                    var req = new { ScoreId = detail.Id, Content = CommentInput.Text };
                    var content = new StringContent(JsonConvert.SerializeObject(req), System.Text.Encoding.UTF8, "application/json");
                    var response = await _httpClient.PostAsync($"{ApiBaseUrl}/comments", content);
                    
                    if (response.IsSuccessStatusCode)
                    {
                        CommentInput.Text = "";
                        await LoadScoreDetails(detail.Id); // 刷新评论区
                    }
                }
                catch (Exception ex) { MessageBox.Show("提交评论失败: " + ex.Message); }
            }
        }

        private void CommentInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) BtnSendComment_Click(sender, e);
        }

        // 点赞/评论联动按钮
        private async void BtnFavorite_Click(object sender, RoutedEventArgs e)
        {
            if (DetailContentState.DataContext is CommunityScoreDetail detail)
            {
                try
                {
                    var response = await _httpClient.PostAsync($"{ApiBaseUrl}/favorite/{detail.Id}", null);
                    if (response.IsSuccessStatusCode)
                    {
                        await LoadScoreDetails(detail.Id); // 刷新右侧详情数据
                        await LoadScores(); // 刷新左侧卡片列表状态
                    }
                }
                catch { }
            }
        }

        // 筛选功能
        private async void FilterButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                string category = btn.Content.ToString();
                await LoadScores(category);
            }
        }
    }
}
