using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace SeeMusicApp
{
    public partial class UploadScoreWindow : Window
    {
        private string selectedScorePath = null;
        private string selectedCoverPath = null;
        private readonly ApiClient _apiClient = new ApiClient();

        public UploadScoreWindow()
        {
            InitializeComponent();
        }

        private void Window_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
                this.DragMove();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        // 选择封面图片
        private void BtnChangeCover_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "图片文件 (*.jpg;*.jpeg;*.png)|*.jpg;*.jpeg;*.png";
            if (openFileDialog.ShowDialog() == true)
            {
                selectedCoverPath = openFileDialog.FileName;
                ImgCover.Source = new BitmapImage(new Uri(selectedCoverPath));
                PlaceholderIcon.Visibility = Visibility.Collapsed;
            }
        }

        // 选择乐谱文件
        private void BtnSelectFile_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "乐谱文件 (*.pdf;*.midi;*.xml)|*.pdf;*.midi;*.xml|所有文件 (*.*)|*.*";
            if (openFileDialog.ShowDialog() == true)
            {
                selectedScorePath = openFileDialog.FileName;
                TxtFileName.Text = Path.GetFileName(selectedScorePath);
                TxtFileName.Foreground = System.Windows.Media.Brushes.Black;
                IconFileReady.Visibility = Visibility.Visible;
            }
        }

        // 提交上传
        private async void BtnSubmit_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(selectedScorePath))
            {
                MessageBox.Show("请先选择乐谱文件", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrEmpty(TxtTitle.Text))
            {
                MessageBox.Show("请输入乐谱标题", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // 模拟上传动画或状态
                this.IsEnabled = false;

                using (var content = new MultipartFormDataContent())
                {
                    // 1. 添加乐谱文件 (必填)
                    var scoreStream = File.OpenRead(selectedScorePath);
                    var scoreFileContent = new StreamContent(scoreStream);
                    content.Add(scoreFileContent, "scoreFile", Path.GetFileName(selectedScorePath));

                    // 2. 添加封面文件 (可选)
                    Stream coverStream = null;
                    if (!string.IsNullOrEmpty(selectedCoverPath))
                    {
                        coverStream = File.OpenRead(selectedCoverPath);
                        var coverFileContent = new StreamContent(coverStream);
                        content.Add(coverFileContent, "coverFile", Path.GetFileName(selectedCoverPath));
                    }

                    // 3. 添加表单数据
                    content.Add(new StringContent(TxtTitle.Text), "Title");
                    content.Add(new StringContent(TxtArtist.Text ?? ""), "ArtistName");
                    content.Add(new StringContent(ComboCategory.Text ?? ""), "Category");
                    content.Add(new StringContent(TxtPrice.Text ?? "0"), "Price");

                    // 4. 调用真实接口
                    await _apiClient.PostMultipartAsync<object>("community/scores", content);

                    // 释放流
                    scoreStream.Close();
                    coverStream?.Close();

                    MessageBox.Show("乐谱上传成功！已发布到社区。", "SeeMusic", MessageBoxButton.OK, MessageBoxImage.Information);
                    this.DialogResult = true; // 标记成功，通知主界面刷新
                    this.Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"上传失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                this.IsEnabled = true;
            }
        }
    }
}
