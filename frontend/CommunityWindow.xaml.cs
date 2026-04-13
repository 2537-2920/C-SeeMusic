using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SeeMusicApp
{
    public partial class CommunityWindow : Window
    {
        public CommunityWindow()
        {
            InitializeComponent();
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
            MessageBox.Show("乐谱上传弹窗将在此处弹出，支持 PDF/MIDI/MusicXML。", "上传乐谱", MessageBoxButton.OK, MessageBoxImage.Information);
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