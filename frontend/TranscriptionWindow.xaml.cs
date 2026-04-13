using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace SeeMusicApp
{
    public partial class TranscriptionWindow : Window
    {
        private bool isRecording = false;

        public TranscriptionWindow()
        {
            InitializeComponent();
        }

        // 1. 无边框窗体拖拽支持
        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }

        // 2. 右上角关闭按钮：关闭当前窗口并返回主界面
        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            MainWindow mainWin = new MainWindow(true);
            mainWin.Show();
            this.Close();
        }

        // ====================== 3. 核心：点击画板添加音符 ======================
        private void MainStaffCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 1. 获取鼠标点击位置在 Canvas 内的相对坐标
            Point clickPoint = e.GetPosition(MainStaffCanvas);

            // 2. 获取右下角 ComboBox 选中的时值（加入 null 检查防报错）
            string duration = "四分音符";
            if (CmbNoteDuration != null && CmbNoteDuration.SelectedItem is ComboBoxItem selectedItem)
            {
                duration = selectedItem.Content.ToString();
            }

            // 3. 创建一个新的 Canvas 容器来装音符的各个部分
            Canvas newNoteWrapper = new Canvas();

            // 调整中心点，让鼠标正好点在符头中央
            Canvas.SetLeft(newNoteWrapper, clickPoint.X - 7);
            Canvas.SetTop(newNoteWrapper, clickPoint.Y - 5);

            // 4. 绘制符头 (正向摆放，去掉了倾斜)
            Border noteHead = new Border
            {
                Width = 14,
                Height = 10,
                CornerRadius = new CornerRadius(5),
                BorderThickness = new Thickness(2),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2c3e50"))
            };

            // 如果是二分音符，符头是空心的（白色），否则是实心的（黑色）
            if (duration == "二分音符")
            {
                noteHead.Background = Brushes.White;
            }
            else
            {
                noteHead.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2c3e50"));
            }

            // 5. 绘制符干 (笔直向上)
            Line stem = new Line
            {
                X1 = 13,
                Y1 = 5,
                X2 = 13,
                Y2 = -30,
                Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2c3e50")),
                StrokeThickness = 1.5
            };

            // 将符头和符干加入容器
            newNoteWrapper.Children.Add(stem);
            newNoteWrapper.Children.Add(noteHead);

            // 6. 如果是八分音符，增加一个小符尾
            if (duration == "八分音符")
            {
                Path tail = new Path
                {
                    Data = Geometry.Parse("M 13,-30 Q 20,-20 23,-5"), // 用贝塞尔曲线画出流畅的符尾弧度
                    Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2c3e50")),
                    StrokeThickness = 1.5
                };
                newNoteWrapper.Children.Add(tail);
            }

            // 7. 将完整的音符组渲染到五线谱画板上
            MainStaffCanvas.Children.Add(newNoteWrapper);
        }

        // ====================== 4. 麦克风录制按钮动画 ======================
        private void BtnMic_Click(object sender, RoutedEventArgs e)
        {
            isRecording = !isRecording;

            if (isRecording)
            {
                // 开启录制状态
                TxtMic.Text = "停止录制...";
                TxtMic.Foreground = Brushes.Red;
                IconMic.Foreground = Brushes.Red;

                BtnMic.BorderBrush = Brushes.Red;
                BtnMic.BorderThickness = new Thickness(2);

                // 创建红光呼吸动画
                DoubleAnimation pulseAnimation = new DoubleAnimation()
                {
                    From = 0.4,
                    To = 1.5,
                    Duration = TimeSpan.FromSeconds(0.8),
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever
                };
                BtnMic.BeginAnimation(UIElement.OpacityProperty, pulseAnimation);
            }
            else
            {
                // 停止录制，恢复默认样式
                TxtMic.Text = "麦克风录音";

                var defaultColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#666666"));
                TxtMic.Foreground = defaultColor;
                IconMic.Foreground = defaultColor;

                BtnMic.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#edf2f7"));
                BtnMic.BorderThickness = new Thickness(1);

                // 停止动画并恢复不透明度
                BtnMic.BeginAnimation(UIElement.OpacityProperty, null);
                BtnMic.Opacity = 1.0;
            }
        }

        // 左上角 Logo 按钮：返回主界面
        private void BtnGoHome_Click(object sender, RoutedEventArgs e)
        {
            MainWindow mainWin = new MainWindow(true);
            mainWin.Show();
            this.Close(); // 关闭当前扒谱窗口
        }
    }
}