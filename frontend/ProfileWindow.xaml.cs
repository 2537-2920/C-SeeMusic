using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace SeeMusicApp
{
    public partial class ProfileWindow : Window
    {
        public ProfileWindow()
        {
            InitializeComponent();
        }

        // 支持窗口无边框拖拽
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
            this.Close(); // 关闭当前的个人中心窗口
        }

        // 模拟清除系统缓存的动态逻辑
        private async void BtnClearCache_Click(object sender, RoutedEventArgs e)
        {
            // 如果缓存已经是 0KB，就不再执行了
            if (TxtCache.Text == "清除系统缓存 (0KB)")
            {
                MessageBox.Show("系统缓存已经很干净啦！", "SeeMusic", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 修改文本状态
            TxtCache.Text = "正在清理缓存中...";
            TxtCache.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B")); // 变成橙色

            // 模拟后台删除文件的耗时
            await Task.Delay(1200);

            // 清理完毕
            TxtCache.Text = "清除系统缓存 (0KB)";
            TxtCache.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9CA3AF")); // 变成灰色

            MessageBox.Show("成功释放 1.2GB 系统缓存空间！", "清理完成", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}