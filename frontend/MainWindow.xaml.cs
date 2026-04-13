using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
namespace SeeMusicApp
{
    public partial class MainWindow : Window
    {
        // 1. 真正的无参构造函数（WPF App.xaml 启动程序时必须依赖它！）
        public MainWindow()
        {
            InitializeComponent();

            // 窗体加载完毕后，生成漂浮的音符动画
            this.Loaded += (s, e) => CreateDecorations();
        }

        // 2. 带参数的重载构造函数（供我们从登录界面跳回来时调用）
        // 注意这里的 : this()，它表示在执行下面代码前，会先去执行上面那个无参构造函数
        public MainWindow(bool isLoggedIn) : this()
        {
            // 如果传入 true，则直接展示已登录的四大金刚卡片
            if (isLoggedIn)
            {
                LoggedOutView.Visibility = Visibility.Collapsed;
                LoggedInView.Visibility = Visibility.Visible;

                // 同步演示面板的按钮状态
                BtnMockIn.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#457b9d"));
                BtnMockIn.Foreground = Brushes.White;
                BtnMockOut.Background = Brushes.White;
                BtnMockOut.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#444444"));
            }
        }
        /// <summary>
        /// 动态生成背景的呼吸音符与力度记号
        /// </summary>
        private void CreateDecorations()
        {
            // 使用标准的 Unicode 转义字符，确保在任何代码文件编码下都不会乱码
            string[] notes = { "\u266A", "\u266B", "\u266C" }; // ♪, ♫, ♬
            string[] markings = { "p", "f", "ff", "mf", "pp", "mp" };
            Random rand = new Random();

            for (int i = 0; i < 12; i++)
            {
                TextBlock el = new TextBlock();
                el.Opacity = 0.3;
                el.RenderTransformOrigin = new Point(0.5, 0.5);

                TransformGroup transformGroup = new TransformGroup();
                TranslateTransform translate = new TranslateTransform();
                ScaleTransform scale = new ScaleTransform();
                transformGroup.Children.Add(scale);
                transformGroup.Children.Add(translate);
                el.RenderTransform = transformGroup;

                if (rand.NextDouble() > 0.4)
                {
                    // 生成音符
                    el.Text = notes[rand.Next(notes.Length)];
                    el.FontSize = rand.NextDouble() * 25 + 25;
                    el.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#333333"));
                }
                else
                {
                    // 生成强弱记号
                    el.Text = markings[rand.Next(markings.Length)];
                    el.FontFamily = new FontFamily("Georgia"); // 英文字母继续用优雅的衬线体
                    el.FontSize = rand.NextDouble() * 20 + 15;
                    el.FontWeight = FontWeights.Bold;
                    el.FontStyle = FontStyles.Italic;
                    el.Foreground = Brushes.Black;
                }
                // 算法：全屏幕均匀且带有随机性的分布
                double leftPercent = (i % 4 * 25 + rand.NextDouble() * 15);
                double topPercent = (Math.Floor(i / 4.0) * 33 + rand.NextDouble() * 20);

                // 中心避让逻辑，防止音符遮挡 Logo 和按钮
                // 定义中间的核心敏感区 (X轴 20%~80% 之间，Y轴 25%~75% 之间)
                if (leftPercent > 20 && leftPercent < 80 && topPercent > 25 && topPercent < 75)
                {
                    // 如果音符刚好掉进了中心敏感区，就把它们推到两边的空白地带
                    if (leftPercent < 50)
                        leftPercent = rand.NextDouble() * 20;       // 强行推到屏幕最左侧 0~20%
                    else
                        leftPercent = 80 + rand.NextDouble() * 20;  // 强行推到屏幕最右侧 80~100%
                }

                Canvas.SetLeft(el, this.Width * leftPercent / 100);
                Canvas.SetTop(el, this.Height * topPercent / 100);

                // 将元素添加到 XAML 中定义的 Canvas 层
                DecorationCanvas.Children.Add(el);

                // --- 绑定核心动画逻辑 ---
                double delaySeconds = rand.NextDouble() * 5;
                TimeSpan duration = TimeSpan.FromSeconds(4); // 呼吸周期
                var easing = new SineEase() { EasingMode = EasingMode.EaseInOut }; // 平滑缓动

                // Y轴漂浮
                DoubleAnimation animY = new DoubleAnimation()
                {
                    By = -20,
                    Duration = duration,
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                    BeginTime = TimeSpan.FromSeconds(delaySeconds),
                    EasingFunction = easing
                };

                // 缩放呼吸
                DoubleAnimation animScale = new DoubleAnimation()
                {
                    To = 1.1,
                    Duration = duration,
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                    BeginTime = TimeSpan.FromSeconds(delaySeconds),
                    EasingFunction = easing
                };

                // 透明度渐变
                DoubleAnimation animOpacity = new DoubleAnimation()
                {
                    To = 0.6,
                    Duration = duration,
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                    BeginTime = TimeSpan.FromSeconds(delaySeconds),
                    EasingFunction = easing
                };

                // 启动动画
                translate.BeginAnimation(TranslateTransform.YProperty, animY);
                scale.BeginAnimation(ScaleTransform.ScaleXProperty, animScale);
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, animScale);
                el.BeginAnimation(UIElement.OpacityProperty, animOpacity);
            }
        }

        // ====================== UI 交互事件 ======================

        // 1. 无边框窗体拖拽支持
        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }

        // 2. 右上角关闭按钮
        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        // 3. 切换至未登录状态
        private void BtnMockOut_Click(object sender, RoutedEventArgs e)
        {
            LoggedOutView.Visibility = Visibility.Visible;
            LoggedInView.Visibility = Visibility.Collapsed;

            BtnMockOut.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#457b9d"));
            BtnMockOut.Foreground = Brushes.White;
            BtnMockIn.Background = Brushes.White;
            BtnMockIn.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#444444"));
        }

        // 4. 切换至已登录状态
        private void BtnMockIn_Click(object sender, RoutedEventArgs e)
        {
            LoggedOutView.Visibility = Visibility.Collapsed;
            LoggedInView.Visibility = Visibility.Visible;

            BtnMockIn.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#457b9d"));
            BtnMockIn.Foreground = Brushes.White;
            BtnMockOut.Background = Brushes.White;
            BtnMockOut.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#444444"));
        }

        // 跳转到登录界面 (传参 true 表示登录模式)
        private void BtnGoLogin_Click(object sender, RoutedEventArgs e)
        {
            LoginWindow loginWin = new LoginWindow(true);
            loginWin.Show();
            this.Close(); // 关闭当前主窗口
        }

        // 跳转到注册界面 (传参 false 表示注册模式)
        private void BtnGoRegister_Click(object sender, RoutedEventArgs e)
        {
            LoginWindow loginWin = new LoginWindow(false);
            loginWin.Show();
            this.Close(); // 关闭当前主窗口
        }

        //跳转到曲谱转录界面
        private void BtnGoTranscription_Click(object sender, MouseButtonEventArgs e)
        {
            TranscriptionWindow transWin = new TranscriptionWindow();
            transWin.Show();
            this.Close();
        }

        private void BtnGoEvaluation_Click(object sender, MouseButtonEventArgs e)
        {
            SingingEvaluationWindow evalWin = new SingingEvaluationWindow();
            evalWin.Show();
            this.Close();
        }

        private void BtnGoCommunity_Click(object sender, MouseButtonEventArgs e)
        {
            CommunityWindow commWin = new CommunityWindow();
            commWin.Show();
            this.Close();
        }

        private void BtnGoProfile_Click(object sender, MouseButtonEventArgs e)
        {
            ProfileWindow profileWin = new ProfileWindow();
            profileWin.Show();
            this.Close();
        }
    }
}