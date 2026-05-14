using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace SeeMusicApp
{
    public partial class CommunityWindow : Window
    {
        private readonly ApiClient _apiClient = new ApiClient();

        public CommunityWindow()
        {
            InitializeComponent();
            this.Loaded += CommunityWindow_Loaded;
        }

        private async void CommunityWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadScores();
        }

        private async Task LoadScores(string keyword = null, string category = null)
        {
            try
            {
                var scores = await _apiClient.GetScoresAsync(keyword, category);
                
                // 清理旧的模拟数据（保留前面的样式演示也可以，但我们现在要真的数据）
                ScoresPanel.Children.Clear();

                foreach (var score in scores)
                {
                    ScoresPanel.Children.Add(CreateScoreCard(score));
                }
            }
            catch (Exception ex)
            {
                // MessageBox.Show($"加载失败: {ex.Message}");
            }
        }

        private UIElement CreateScoreCard(ScoreDto score)
        {
            // 动态创建一个符合 ScoreCardStyle 的 Border
            var border = new Border
            {
                Style = (Style)this.Resources["ScoreCardStyle"],
                Width = 210,
                Margin = new Thickness(0, 0, 20, 20),
                Tag = score.Id // 存入 ID
            };
            border.MouseLeftButtonDown += async (s, e) => await ShowScoreDetail(score.Id);

            var stack = new StackPanel();
            
            // 封面图片
            var coverBorder = new Border
            {
                Height = 200,
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(241, 245, 249)),
                CornerRadius = new CornerRadius(12),
                Margin = new Thickness(0, 0, 0, 15),
                ClipToBounds = true
            };
            
            if (!string.IsNullOrEmpty(score.CoverUrl))
            {
                var img = new Image { Stretch = System.Windows.Media.Stretch.UniformToFill };
                img.Source = new BitmapImage(new Uri("http://localhost:5000" + score.CoverUrl));
                coverBorder.Child = img;
            }
            else
            {
                coverBorder.Child = new TextBlock 
                { 
                    Text = score.Title.Substring(0,1), 
                    FontSize = 50, FontWeight = FontWeights.Bold, 
                    Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(203, 213, 225)),
                    HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
                };
            }

            stack.Children.Add(coverBorder);
            stack.Children.Add(new TextBlock { Text = score.Title, FontWeight = FontWeights.Bold, FontSize = 18, Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 41, 59)) });
            stack.Children.Add(new TextBlock { Text = score.AuthorName, FontSize = 12, Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(148, 163, 184)), Margin = new Thickness(0, 5, 0, 15) });

            var priceGrid = new Grid();
            priceGrid.Children.Add(new TextBlock { Text = score.Price == 0 ? "免费" : $"¥{score.Price / 100.0:F2}", FontWeight = FontWeights.Bold, Foreground = score.Price == 0 ? System.Windows.Media.Brushes.Green : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(69, 123, 157)) });
            
            border.Child = stack;
            return border;
        }

        private async Task ShowScoreDetail(int scoreId)
        {
            try
            {
                var detail = await _apiClient.GetScoreDetailAsync(scoreId);
                DetailTitle.Text = detail.Title;
                DetailAuthor.Text = detail.AuthorName;
                DetailCover.Text = detail.Title.Substring(0, 1);

                DetailEmptyState.Visibility = Visibility.Collapsed;
                DetailContentState.Visibility = Visibility.Visible;
            }
            catch { }
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

        // 上传按钮提示
        private void BtnUpload_Click(object sender, RoutedEventArgs e)
        {
            UploadScoreWindow uploadWin = new UploadScoreWindow();
            uploadWin.Owner = this; // 设置所有者，使其居中弹出
            uploadWin.ShowDialog();
        }

        // 乐谱卡片点击：联动右侧详情面板
        private void ScoreCard_Click(object sender, MouseButtonEventArgs e)
        {
            // 拿到被点击的卡片
            if (sender is Border clickedCard)
            {
                // 解析我们在 XAML 的 Tag 里存的简易数据: "晴天|周杰伦|¥6.00|钢琴版"
                string tagData = clickedCard.Tag?.ToString();
                if (!string.IsNullOrEmpty(tagData))
                {
                    string[] data = tagData.Split('|');

                    if (data.Length >= 2)
                    {
                        DetailTitle.Text = data[0]; // 曲名
                        DetailAuthor.Text = data[1] + (data.Length >= 4 ? " · " + data[3] : ""); // 作者 · 标签
                        DetailCover.Text = data[0].Substring(0, 1); // 封面取首字母代替
                    }
                }

                // 隐藏空白状态，显示详情内容
                DetailEmptyState.Visibility = Visibility.Collapsed;
                DetailContentState.Visibility = Visibility.Visible;
            }
        }
    }
}