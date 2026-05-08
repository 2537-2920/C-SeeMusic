using Newtonsoft.Json;
using System;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SeeMusicApp
{
    public partial class UploadScoreWindow : Window
    {
        private readonly HttpClient _httpClient = new HttpClient();
        private const string ApiBaseUrl = "http://localhost:5000/api/v1/community";

        public UploadScoreWindow()
        {
            InitializeComponent();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed) this.DragMove();
        }

        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "乐谱文件 (*.pdf;*.mid;*.xml;*.musicxml)|*.pdf;*.mid;*.xml;*.musicxml",
                Title = "选择乐谱文件"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                TxtFilePath.Text = openFileDialog.FileName;
            }
        }

        private void BtnBrowseCover_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "图片文件 (*.jpg;*.jpeg;*.png)|*.jpg;*.jpeg;*.png",
                Title = "选择封面图片"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                TxtCoverPath.Text = openFileDialog.FileName;
                try
                {
                    // 图片预览核心逻辑
                    ImgCoverPreview.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(openFileDialog.FileName));
                }
                catch { /* 忽略损坏的图像 */ }
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private async void BtnUpload_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(TxtTitle.Text) || string.IsNullOrEmpty(TxtFilePath.Text))
            {
                MessageBox.Show("请填写标题并选择乐谱文件！", "提示");
                return;
            }

            try
            {
                using (var content = new MultipartFormDataContent())
                {
                    content.Add(new StringContent(TxtTitle.Text), "Title");
                    content.Add(new StringContent(TxtArtist.Text), "ArtistName");
                    content.Add(new StringContent(TxtTag.Text), "ArrangementTag");
                    content.Add(new StringContent(TxtDesc.Text), "Description");
                    
                    // 获取选中的分类
                    if (CboCategory.SelectedItem is ComboBoxItem selectedCategory)
                    {
                        content.Add(new StringContent(selectedCategory.Content.ToString()), "Category");
                    }
                    
                    if (double.TryParse(TxtPrice.Text, out double priceYuan))
                        content.Add(new StringContent(((int)(priceYuan * 100)).ToString()), "PriceCent");

                    // 添加乐谱文件
                    var scoreStream = File.OpenRead(TxtFilePath.Text);
                    var scoreContent = new StreamContent(scoreStream);
                    content.Add(scoreContent, "file", Path.GetFileName(TxtFilePath.Text));

                    // 添加封面文件 (可选)
                    if (!string.IsNullOrEmpty(TxtCoverPath.Text))
                    {
                        var coverStream = File.OpenRead(TxtCoverPath.Text);
                        var coverContent = new StreamContent(coverStream);
                        content.Add(coverContent, "cover", Path.GetFileName(TxtCoverPath.Text));
                    }

                    var response = await _httpClient.PostAsync($"{ApiBaseUrl}/scores", content);
                    
                    if (response.IsSuccessStatusCode)
                    {
                        MessageBox.Show("分享成功！", "成功");
                        this.DialogResult = true;
                        this.Close();
                    }
                    else
                    {
                        MessageBox.Show("上传失败，请检查文件大小或后端服务。");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("错误: " + ex.Message);
            }
        }
    }
}
