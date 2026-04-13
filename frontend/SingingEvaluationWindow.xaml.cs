using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace SeeMusicApp
{
    public partial class SingingEvaluationWindow : Window
    {
        public SingingEvaluationWindow()
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

        // 返回主界面
        private void BtnGoHome_Click(object sender, RoutedEventArgs e)
        {
            MainWindow mainWin = new MainWindow(true);
            mainWin.Show();
            this.Close(); // 关闭当前歌唱评估窗口
        }

        // 模拟“开始智能检测”按钮的异步加载逻辑
        private async void BtnStartEval_Click(object sender, RoutedEventArgs e)
        {
            // 防止重复点击
            BtnStartEval.IsEnabled = false;

            // 保存原本的文字，修改按钮状态为加载中
            string originalText = BtnStartEval.Content.ToString();
            BtnStartEval.Content = "正在分析音准与节奏...";
            BtnStartEval.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9CA3AF")); // 变成灰色

            // 模拟 AI 分析的耗时过程 (1.5秒)
            await Task.Delay(1500);

            // 恢复按钮状态
            BtnStartEval.Content = originalText;
            BtnStartEval.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#457b9d"));
            BtnStartEval.IsEnabled = true;

            // 弹窗提示
            MessageBox.Show("演唱作品分析完毕！(评估数据已更新)", "SeeMusic AI", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}