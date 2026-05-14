using Microsoft.Win32;
using SeeMusicApp.Models;
using SeeMusicApp.Services;
using System;
using System.Linq;
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
        private readonly AnalysisApiClient _analysisApiClient = new AnalysisApiClient();
        private bool isRecording;
        private string _selectedAudioPath;

        public TranscriptionWindow()
        {
            InitializeComponent();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            var mainWin = new MainWindow(true);
            mainWin.Show();
            Close();
        }

        private void MainStaffCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Point clickPoint = e.GetPosition(MainStaffCanvas);

            string duration = "四分音符";
            if (CmbNoteDuration != null && CmbNoteDuration.SelectedItem is ComboBoxItem selectedItem)
            {
                duration = selectedItem.Content.ToString();
            }

            var newNoteWrapper = new Canvas();
            Canvas.SetLeft(newNoteWrapper, clickPoint.X - 7);
            Canvas.SetTop(newNoteWrapper, clickPoint.Y - 5);

            var noteHead = new Border
            {
                Width = 14,
                Height = 10,
                CornerRadius = new CornerRadius(5),
                BorderThickness = new Thickness(2),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2c3e50")),
                Background = duration == "二分音符"
                    ? Brushes.White
                    : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2c3e50"))
            };

            var stem = new Line
            {
                X1 = 13,
                Y1 = 5,
                X2 = 13,
                Y2 = -30,
                Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2c3e50")),
                StrokeThickness = 1.5
            };

            newNoteWrapper.Children.Add(stem);
            newNoteWrapper.Children.Add(noteHead);

            if (duration == "八分音符")
            {
                var tail = new Path
                {
                    Data = Geometry.Parse("M 13,-30 Q 20,-20 23,-5"),
                    Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2c3e50")),
                    StrokeThickness = 1.5
                };
                newNoteWrapper.Children.Add(tail);
            }

            MainStaffCanvas.Children.Add(newNoteWrapper);
        }

        private void BtnMic_Click(object sender, RoutedEventArgs e)
        {
            isRecording = !isRecording;

            if (isRecording)
            {
                TxtMic.Text = "停止录制...";
                TxtMic.Foreground = Brushes.Red;
                IconMic.Foreground = Brushes.Red;
                BtnMic.BorderBrush = Brushes.Red;
                BtnMic.BorderThickness = new Thickness(2);

                var pulseAnimation = new DoubleAnimation
                {
                    From = 0.4,
                    To = 1.5,
                    Duration = TimeSpan.FromSeconds(0.8),
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever
                };
                BtnMic.BeginAnimation(OpacityProperty, pulseAnimation);
            }
            else
            {
                TxtMic.Text = "麦克风录音";

                var defaultColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#666666"));
                TxtMic.Foreground = defaultColor;
                IconMic.Foreground = defaultColor;
                BtnMic.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#edf2f7"));
                BtnMic.BorderThickness = new Thickness(1);
                BtnMic.BeginAnimation(OpacityProperty, null);
                BtnMic.Opacity = 1.0;
            }
        }

        private void BtnGoHome_Click(object sender, RoutedEventArgs e)
        {
            var mainWin = new MainWindow(true);
            mainWin.Show();
            Close();
        }

        private void BtnPickAudio_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "选择用于扒谱和节拍分析的音频",
                Filter = "WAV 音频|*.wav|音频/视频文件|*.wav;*.mp3;*.m4a;*.mp4;*.mov|所有文件|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                _selectedAudioPath = dialog.FileName;
                var fileName = System.IO.Path.GetFileName(_selectedAudioPath);
                TxtSelectedAudio.Text = fileName;
                TxtTranscriptionStatus.Text = "文件已选择，点击“重新分析音频”开始调用后端。";
                TxtProjectFileName.Text = System.IO.Path.GetFileNameWithoutExtension(fileName) + ".musicxml";
                TxtTrackName.Text = "主旋律 (" + System.IO.Path.GetFileNameWithoutExtension(fileName) + ")";
            }
        }

        private async void BtnAnalyzeAudio_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_selectedAudioPath))
            {
                MessageBox.Show("请先选择一个本地音频文件。", "SeeMusic", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            BtnAnalyzeAudio.IsEnabled = false;
            BtnPickAudio.IsEnabled = false;
            TxtTranscriptionStatus.Text = string.Format("正在连接 {0} 并上传音频...", _analysisApiClient.GetBackendBaseUrl());

            try
            {
                var workflow = await _analysisApiClient.AnalyzeAudioAsync(
                    _selectedAudioPath,
                    separateMelody: ChkSeparateMelody.IsChecked == true,
                    separateAccompaniment: ChkSeparateAccompaniment.IsChecked == true);

                ApplyAnalysisResult(workflow.Analysis.BeatAnalysis, workflow.Analysis.Message);
                TxtTranscriptionStatus.Text = "分析完成，可继续编辑乐谱。";
            }
            catch (Exception exception)
            {
                TxtTranscriptionStatus.Text = string.Format("分析失败：{0}", exception.Message);
                MessageBox.Show(exception.Message, "SeeMusic", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                BtnAnalyzeAudio.IsEnabled = true;
                BtnPickAudio.IsEnabled = true;
            }
        }

        private void ApplyAnalysisResult(BeatAnalysisResult analysis, string message)
        {
            if (analysis == null || !analysis.IsAvailable || ChkRhythmAnalysis.IsChecked != true)
            {
                TxtDetectedBpm.Text = "--";
                TxtDetectedTimeSignature.Text = "--/--";
                TxtBeatAnalysisSummary.Text = analysis != null && !string.IsNullOrWhiteSpace(analysis.Summary)
                    ? analysis.Summary
                    : "未输出可用节拍结果。";
                TxtBeatTimesPreview.Text = "拍点预览: --";
                return;
            }

            TxtDetectedBpm.Text = analysis.TempoBpm.ToString("0.0");
            TxtDetectedTimeSignature.Text = string.Format("{0}/{1}", analysis.TimeSignatureNumerator, analysis.TimeSignatureDenominator);
            TxtBeatAnalysisSummary.Text = string.IsNullOrWhiteSpace(analysis.Summary) ? message : analysis.Summary;
            TxtBeatTimesPreview.Text = analysis.BeatTimes == null || analysis.BeatTimes.Count == 0
                ? "拍点预览: --"
                : "拍点预览: " + string.Join("s, ", analysis.BeatTimes.Take(8).Select(time => time.ToString("0.00"))) + "s";
        }
    }
}
