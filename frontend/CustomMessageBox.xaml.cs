using System;
using System.Windows;
using System.Windows.Media;

namespace SeeMusicApp
{
    public enum MessageBoxType
    {
        Success,
        Info,
        Warning,
        Error
    }

    public partial class CustomMessageBox : Window
    {
        public CustomMessageBox(string message, string title, MessageBoxType type)
        {
            InitializeComponent();
            TxtMessage.Text = message;
            TxtTitle.Text = title;

            // 根据类型更新图标和主题色
            switch (type)
            {
                case MessageBoxType.Success:
                    IconText.Text = "\uE73E"; // Checkmark
                    IconText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#22C55E"));
                    IconBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DCFCE7"));
                    break;
                case MessageBoxType.Warning:
                    IconText.Text = "\uE7BA"; // Warning
                    IconText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EAB308"));
                    IconBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FEF9C3"));
                    break;
                case MessageBoxType.Error:
                    IconText.Text = "\uEA39"; // Error / Red cross
                    IconText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444"));
                    IconBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FEE2E2"));
                    break;
                case MessageBoxType.Info:
                default:
                    IconText.Text = "\uE946"; // Info icon
                    IconText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3B82F6"));
                    IconBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EFF6FF"));
                    break;
            }
        }

        private void Window_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
                this.DragMove();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }

        // 静态 Show 方法，方便直接调用
        public static bool? Show(string message, string title = "提示", MessageBoxType type = MessageBoxType.Info, Window owner = null)
        {
            var box = new CustomMessageBox(message, title, type);
            if (owner != null)
            {
                box.Owner = owner;
            }
            else
            {
                // 尝试自动寻找当前活动窗口作为 Owner
                if (Application.Current != null && Application.Current.MainWindow != null)
                {
                    box.Owner = Application.Current.MainWindow;
                }
            }
            return box.ShowDialog();
        }
    }
}
