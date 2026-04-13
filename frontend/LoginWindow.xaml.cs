using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace SeeMusicApp
{
    public partial class LoginWindow : Window
    {
        // 状态标记：当前是否为登录模式
        private bool isLoginMode = true;

        // 修改构造函数，接收传过来的参数（默认 true 为登录）
        public LoginWindow(bool startAsLogin = true)
        {
            InitializeComponent();

            // 窗体加载完成后生成背景的漂浮音符
            this.Loaded += (s, e) => CreateDecorations();

            // 根据主界面的点击，初始化显示“登录”还是“注册”
            isLoginMode = !startAsLogin; // 故意取反，为了复用下面的 SwitchModeLogic 逻辑
            SwitchModeLogic();
        }

        private void CreateDecorations()
        {
            string[] notes = { "\u266A", "\u266B", "\u266C" };
            string[] markings = { "p", "f", "ff", "mf", "pp", "mp" };
            Random rand = new Random();

            for (int i = 0; i < 15; i++)
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
                    el.Text = notes[rand.Next(notes.Length)];
                    el.FontFamily = new FontFamily("Arial");
                    el.FontSize = rand.NextDouble() * 25 + 25;
                    el.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#457b9d"));
                }
                else
                {
                    el.Text = markings[rand.Next(markings.Length)];
                    el.FontFamily = new FontFamily("Georgia");
                    el.FontSize = rand.NextDouble() * 20 + 15;
                    el.FontWeight = FontWeights.Bold;
                    el.FontStyle = FontStyles.Italic;
                    el.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1d3557"));
                }

                double leftPercent = rand.NextDouble() * 100;
                double topPercent = rand.NextDouble() * 100;

                Canvas.SetLeft(el, this.Width * leftPercent / 100);
                Canvas.SetTop(el, this.Height * topPercent / 100);

                DecorationCanvas.Children.Add(el);

                double delaySeconds = rand.NextDouble() * 5;
                TimeSpan duration = TimeSpan.FromSeconds(3);
                var easing = new SineEase() { EasingMode = EasingMode.EaseInOut };

                DoubleAnimation animY = new DoubleAnimation()
                {
                    By = -15,
                    Duration = duration,
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                    BeginTime = TimeSpan.FromSeconds(delaySeconds),
                    EasingFunction = easing
                };

                DoubleAnimation animScale = new DoubleAnimation()
                {
                    To = 1.05,
                    Duration = duration,
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                    BeginTime = TimeSpan.FromSeconds(delaySeconds),
                    EasingFunction = easing
                };

                translate.BeginAnimation(TranslateTransform.YProperty, animY);
                scale.BeginAnimation(ScaleTransform.ScaleXProperty, animScale);
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, animScale);
            }
        }

        // ====================== UI 交互事件 ======================

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }

        // 正确实现返回主页逻辑
        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            // 取消登录，重新实例化主界面并保持未登录状态
            MainWindow mainWin = new MainWindow(); // 默认参数就是 false
            mainWin.Show();
            // 关闭当前的登录界面
            this.Close();
        }

        private void BtnSubmit_Click(object sender, RoutedEventArgs e)
        {
            if (isLoginMode)
            {
                // 模拟登录成功逻辑
                MessageBox.Show("登录成功！欢迎回来。", "SeeMusic", MessageBoxButton.OK, MessageBoxImage.Information);

                // 核心修改：实例化主界面，并传入 true 告诉主界面显示已登录状态！
                MainWindow mainWin = new MainWindow(true);
                mainWin.Show();
                this.Close();
            }
            else
            {
                MessageBox.Show("注册成功！请使用新账号登录。", "SeeMusic", MessageBoxButton.OK, MessageBoxImage.Information);
                // 注册成功后，自动帮你切换回登录模式
                SwitchModeLogic();
            }
        }

        // 将切换逻辑提取为独立方法，供构造函数和点击事件复用
        private void SwitchModeLogic()
        {
            isLoginMode = !isLoginMode;

            if (isLoginMode)
            {
                TxtTitle.Text = "欢迎回来";
                TxtSubtitle.Text = "让灵感在五线谱上自由流淌";
                ConfirmPwdPanel.Visibility = Visibility.Collapsed;

                TxtSubmit.Text = "立即登录";
                IconSubmit.Text = "\xE8FA";

                TxtSwitch.Text = "创建新账号";
                IconSwitch.Text = "\xE8D4";
            }
            else
            {
                TxtTitle.Text = "加入 SeeMusic";
                TxtSubtitle.Text = "开启您的智能音乐创作之旅";
                ConfirmPwdPanel.Visibility = Visibility.Visible;

                TxtSubmit.Text = "注册账号";
                IconSubmit.Text = "\xE8D4";

                TxtSwitch.Text = "已有账号？登录";
                IconSwitch.Text = "\xE8FA";
            }
        }

        // 按钮点击时调用切换逻辑
        private void BtnSwitchMode_Click(object sender, RoutedEventArgs e)
        {
            SwitchModeLogic();
        }
    }
}