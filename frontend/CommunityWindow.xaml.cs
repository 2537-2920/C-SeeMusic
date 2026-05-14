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
        private int _currentScoreId = -1;

        public CommunityWindow()
        {
            InitializeComponent();
            this.Loaded += CommunityWindow_Loaded;
        }

        private async void BtnSendComment_Click(object sender, RoutedEventArgs e)
        {
            if (_currentScoreId == -1 || string.IsNullOrWhiteSpace(CommentInput.Text))
                return;

            try
            {
                var success = await _apiClient.AddCommentAsync(_currentScoreId, CommentInput.Text);
                if (success)
                {
                    CommentInput.Clear();
                    // 刷新详情以显示新评论
                    await ShowScoreDetail(_currentScoreId);
                }
                else
                {
                    MessageBox.Show("评论失败，请检查登录状态");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"评论出错: {ex.Message}");
            }
        }

        private async void CommunityWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadScores();
        }

        private async void FilterCategory_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button clickedButton)
            {
                // 更新样式：让所有分类按钮变回普通样式
                foreach (var child in CategoryPanel.Children)
                {
                    if (child is Button btn)
                    {
                        btn.Style = (Style)this.Resources["FilterBtnStyle"];
                    }
                }

                // 让当前点击的按钮高亮
                clickedButton.Style = (Style)this.Resources["ActiveFilterBtnStyle"];

                // 获取分类名称进行查询 (如果是“精选”则传 null 表示显示全部)
                string category = clickedButton.Content.ToString();
                if (category == "精选") category = null;

                await LoadScores(null, category);
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            SearchPlaceholder.Visibility = string.IsNullOrEmpty(SearchBox.Text) ? Visibility.Visible : Visibility.Collapsed;
        }

        private async void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                await LoadScores(SearchBox.Text);
            }
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
            
            var statsStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            
            // 收藏数
            statsStack.Children.Add(new TextBlock { Text = "", FontFamily = new System.Windows.Media.FontFamily("Segoe MDL2 Assets"), FontSize = 12, Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(148, 163, 184)), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,4,0) });
            statsStack.Children.Add(new TextBlock { Text = FormatCount(score.FavoriteCount), FontSize = 12, Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(148, 163, 184)), VerticalAlignment = VerticalAlignment.Center });
            
            priceGrid.Children.Add(statsStack);
            stack.Children.Add(priceGrid);
            border.Child = stack;
            return border;
        }

        private async Task ShowScoreDetail(int scoreId)
        {
            _currentScoreId = scoreId;
            try
            {
                var detail = await _apiClient.GetScoreDetailAsync(scoreId);
                DetailTitle.Text = detail.Title;
                DetailAuthor.Text = detail.AuthorName;
                DetailCover.Text = detail.Title.Substring(0, 1);
                
                DetailFavoriteCount.Text = FormatCount(detail.FavoriteCount);
                DetailCommentHeader.Text = $"社区评论 ({detail.CommentCount})";

                // 填充评论
                DetailCommentsPanel.Children.Clear();
                foreach (var comment in detail.RecentComments)
                {
                    DetailCommentsPanel.Children.Add(CreateCommentItem(comment));
                }

                DetailEmptyState.Visibility = Visibility.Collapsed;
                DetailContentState.Visibility = Visibility.Visible;
            }
            catch { }
        }

        private UIElement CreateCommentItem(CommentDto comment)
        {
            var grid = new Grid { Margin = new Thickness(0, 0, 0, 15) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var avatar = new System.Windows.Shapes.Ellipse { Width = 32, Height = 32, Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(226, 232, 240)), VerticalAlignment = VerticalAlignment.Top };
            grid.Children.Add(avatar);

            var stack = new StackPanel { Margin = new Thickness(10, 0, 0, 0) };
            Grid.SetColumn(stack, 1);

            var header = new StackPanel { Orientation = Orientation.Horizontal };
            header.Children.Add(new TextBlock { Text = comment.UserName, FontWeight = FontWeights.Bold, FontSize = 12, Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(51, 51, 51)) });
            header.Children.Add(new TextBlock { Text = comment.CreatedAt.ToString("MM-dd HH:mm"), FontSize = 10, Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(203, 211, 225)), Margin = new Thickness(10, 2, 0, 0) });
            
            stack.Children.Add(header);
            stack.Children.Add(new TextBlock { Text = comment.Content, FontSize = 12, Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(100, 116, 139)), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 5, 0, 0) });

            grid.Children.Add(stack);
            return grid;
        }

        private string FormatCount(int count)
        {
            if (count >= 1000) return (count / 1000.0).ToString("F1") + "k";
            return count.ToString();
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
        private async void BtnUpload_Click(object sender, RoutedEventArgs e)
        {
            UploadScoreWindow uploadWin = new UploadScoreWindow();
            uploadWin.Owner = this; // 设置所有者，使其居中弹出
            if (uploadWin.ShowDialog() == true)
            {
                await LoadScores();
            }
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