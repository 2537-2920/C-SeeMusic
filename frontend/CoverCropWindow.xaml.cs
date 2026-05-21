using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SeeMusicApp
{
    public partial class CoverCropWindow : Window
    {
        private string _originalImagePath;
        public string CroppedImagePath { get; private set; }

        private Point _lastMousePosition;
        private bool _isDragging = false;

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
                bitmap.UriSource = new Uri(Path.GetFullPath(_originalImagePath), UriKind.Absolute);
                bitmap.EndInit();
                
                SourceImage.Source = bitmap;
                
                // 等比缩放计算：以填充 180x200 的实例视口为基准
                double viewW = 180.0;
                double viewH = 200.0;
                double imgW = bitmap.PixelWidth;
                double imgH = bitmap.PixelHeight;

                // 取得覆盖视口所需的最小缩放比
                double scale = Math.Max(viewW / imgW, viewH / imgH);

                // 设置图片控件在拖动画布中的大小（放大的原始尺寸）
                SourceImage.Width = imgW * scale;
                SourceImage.Height = imgH * scale;

                // 居中初始位置
                ImageTranslate.X = (viewW - SourceImage.Width) / 2.0;
                ImageTranslate.Y = (viewH - SourceImage.Height) / 2.0;
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"加载图片失败: {ex.Message}", "加载错误", MessageBoxType.Error, this);
                this.DialogResult = false;
            }
        }

        private void SourceImage_Loaded(object sender, RoutedEventArgs e)
        {
            // 初始化位置（在画布左上角，通过 ImageTranslate 来精确控制位置）
            Canvas.SetLeft(SourceImage, 0);
            Canvas.SetTop(SourceImage, 0);
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

        // ====================== 拖动位置逻辑 ======================

        private void DragCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isDragging = true;
            _lastMousePosition = e.GetPosition(DragCanvas);
            DragCanvas.CaptureMouse();
        }

        private void DragCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging)
            {
                Point currentPos = e.GetPosition(DragCanvas);
                double deltaX = currentPos.X - _lastMousePosition.X;
                double deltaY = currentPos.Y - _lastMousePosition.Y;

                double newX = ImageTranslate.X + deltaX;
                double newY = ImageTranslate.Y + deltaY;

                // 限制滑动边界，防止拖出空白区域
                // X 轴限制在 [180 - Width, 0] 范围内，Y 轴限制在 [200 - Height, 0] 范围内
                double minX = 180.0 - SourceImage.Width;
                double minY = 200.0 - SourceImage.Height;

                if (minX < 0)
                {
                    newX = Math.Max(minX, Math.Min(0, newX));
                }
                else
                {
                    newX = 0; // 宽度刚好契合视口，禁止左右移动
                }

                if (minY < 0)
                {
                    newY = Math.Max(minY, Math.Min(0, newY));
                }
                else
                {
                    newY = 0; // 高度刚好契合视口，禁止上下移动
                }

                ImageTranslate.X = newX;
                ImageTranslate.Y = newY;

                _lastMousePosition = currentPos;
            }
        }

        private void DragCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isDragging = false;
            DragCanvas.ReleaseMouseCapture();
        }

        // ====================== 确认生成裁剪图片 ======================

        private void BtnConfirm_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 获取视口 Canvas 的真实尺寸，消除父布局带来的位置偏移！
                int width = (int)DragCanvas.ActualWidth;
                int height = (int)DragCanvas.ActualHeight;

                if (width <= 0) width = 180;
                if (height <= 0) height = 200;

                // 强制刷新排版
                DragCanvas.UpdateLayout();

                // 使用 VisualBrush 把 DragCanvas 渲染到 DrawingVisual 的 (0, 0) 起点
                DrawingVisual drawingVisual = new DrawingVisual();
                using (DrawingContext drawingContext = drawingVisual.RenderOpen())
                {
                    VisualBrush visualBrush = new VisualBrush(DragCanvas);
                    drawingContext.DrawRectangle(visualBrush, null, new Rect(0, 0, width, height));
                }

                // 渲染 DrawingVisual，得到精准且不带外框的裁剪图片
                RenderTargetBitmap rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
                rtb.Render(drawingVisual);

                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(rtb));

                string tempPath = Path.Combine(Path.GetTempPath(), $"cover_cropped_{Guid.NewGuid()}.png");
                using (var stream = new FileStream(tempPath, FileMode.Create))
                {
                    encoder.Save(stream);
                }

                CroppedImagePath = tempPath;
                this.DialogResult = true;
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"保存封面失败: {ex.Message}", "错误", MessageBoxType.Error, this);
            }
        }
    }
}
