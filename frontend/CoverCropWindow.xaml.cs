using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace SeeMusicApp
{
    public partial class CoverCropWindow : Window
    {
        private Point _startPoint;
        private bool _isDragging = false;
        private string _originalImagePath;
        public string CroppedImagePath { get; private set; }

        public CoverCropWindow(string imagePath)
        {
            InitializeComponent();
            _originalImagePath = imagePath;
            LoadImage();
        }

        private void LoadImage()
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(System.IO.Path.GetFullPath(_originalImagePath), UriKind.Absolute);
                bitmap.EndInit();
                
                SourceImage.Source = bitmap;
                SourceImage.Width = bitmap.Width;
                SourceImage.Height = bitmap.Height;
                CropCanvas.Width = bitmap.Width;
                CropCanvas.Height = bitmap.Height;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载图片失败: {ex.Message}");
                this.DialogResult = false;
            }
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is Border || e.OriginalSource is Grid)
                this.DragMove();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }

        private void CropCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isDragging = true;
            _startPoint = e.GetPosition(CropCanvas);
            SelectionBox.Visibility = Visibility.Visible;
            Canvas.SetLeft(SelectionBox, _startPoint.X);
            Canvas.SetTop(SelectionBox, _startPoint.Y);
            SelectionBox.Width = 0;
            SelectionBox.Height = 0;
            CropCanvas.CaptureMouse();
        }

        private void CropCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging)
            {
                var currentPoint = e.GetPosition(CropCanvas);

                var x = Math.Min(currentPoint.X, _startPoint.X);
                var y = Math.Min(currentPoint.Y, _startPoint.Y);
                var width = Math.Abs(currentPoint.X - _startPoint.X);
                var height = Math.Abs(currentPoint.Y - _startPoint.Y);

                // 限制在画布内
                x = Math.Max(0, x);
                y = Math.Max(0, y);
                width = Math.Min(width, CropCanvas.Width - x);
                height = Math.Min(height, CropCanvas.Height - y);

                Canvas.SetLeft(SelectionBox, x);
                Canvas.SetTop(SelectionBox, y);
                SelectionBox.Width = width;
                SelectionBox.Height = height;
            }
        }

        private void CropCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isDragging = false;
            CropCanvas.ReleaseMouseCapture();
        }

        private void BtnConfirm_Click(object sender, RoutedEventArgs e)
        {
            if (SelectionBox.Width <= 10 || SelectionBox.Height <= 10)
            {
                MessageBox.Show("裁剪区域太小，请重新框选。");
                return;
            }

            try
            {
                var x = Canvas.GetLeft(SelectionBox);
                var y = Canvas.GetTop(SelectionBox);
                var width = SelectionBox.Width;
                var height = SelectionBox.Height;

                var sourceBitmap = (BitmapSource)SourceImage.Source;
                
                // 转换 WPF 的 DIP 坐标为图片的真实像素坐标
                double scaleX = sourceBitmap.PixelWidth / sourceBitmap.Width;
                double scaleY = sourceBitmap.PixelHeight / sourceBitmap.Height;

                int pixelX = (int)(x * scaleX);
                int pixelY = (int)(y * scaleY);
                int pixelWidth = (int)(width * scaleX);
                int pixelHeight = (int)(height * scaleY);

                var rect = new Int32Rect(pixelX, pixelY, pixelWidth, pixelHeight);
                var croppedBitmap = new CroppedBitmap(sourceBitmap, rect);

                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(croppedBitmap));

                string tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"cover_cropped_{Guid.NewGuid()}.png");
                using (var stream = new FileStream(tempPath, FileMode.Create))
                {
                    encoder.Save(stream);
                }

                CroppedImagePath = tempPath;
                this.DialogResult = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"裁剪失败: {ex.Message}");
            }
        }
    }
}
