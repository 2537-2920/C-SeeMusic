using Microsoft.Win32;
using SeeMusicApp.Models;
using SeeMusicApp.Services;
using System;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SeeMusicApp
{
    public partial class TranscriptionWindow : Window
    {
        private readonly AnalysisApiClient _analysisApiClient = new AnalysisApiClient();
        private string _selectedAudioPath;
        private ScoreDetailResponse _currentScore;
        private int _currentPageIndex = 1;
        private bool _isServiceAvailable = true;

        public TranscriptionWindow()
        {
            InitializeComponent();
            Loaded += Window_Loaded;
            ResetUiState();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await RefreshHealthAsync();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void BtnGoHome_Click(object sender, RoutedEventArgs e)
        {
            var mainWin = new MainWindow(true);
            mainWin.Show();
            Close();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void BtnChooseAudio_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "选择用于识谱的音频文件",
                Filter = "音频文件|*.wav;*.mp3;*.m4a;*.ogg|所有文件|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                _selectedAudioPath = dialog.FileName;
                TxtSelectedAudio.Text = System.IO.Path.GetFileName(_selectedAudioPath);
                TxtTranscriptionStatus.Text = "文件已选择，可以开始生成双手钢琴谱。";
                TxtFooterStatus.Text = "音频准备完成，点击“生成双手钢琴谱”开始识谱。";

                if (string.IsNullOrWhiteSpace(TxtProjectTitle.Text)
                    || string.Equals(TxtProjectTitle.Text, "我的智能识谱项目", StringComparison.Ordinal))
                {
                    TxtProjectTitle.Text = System.IO.Path.GetFileNameWithoutExtension(_selectedAudioPath);
                }
            }
        }

        private async void BtnGenerateScore_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_selectedAudioPath))
            {
                MessageBox.Show("请先选择一个可用的本地音频文件。", "SeeMusic", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!await RefreshHealthAsync())
            {
                MessageBox.Show("当前后端服务不可用，请确认服务已启动后重试。", "SeeMusic", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SetBusy(true);
            ClearScoreOnly();
            TranscriptionProgressBar.Value = 8;
            TxtTranscriptionStatus.Text = "正在上传音频并创建识谱任务...";
            TxtFooterStatus.Text = "识谱任务已开始，正在与后端同步处理进度。";

            try
            {
                var upload = await _analysisApiClient.UploadAudioAsync(_selectedAudioPath);
                TranscriptionProgressBar.Value = 18;

                var title = string.IsNullOrWhiteSpace(TxtProjectTitle.Text)
                    ? "我的智能识谱项目"
                    : TxtProjectTitle.Text.Trim();
                var createResponse = await _analysisApiClient.CreatePianoTranscriptionAsync(upload.MediaId, title);
                var status = new TranscriptionStatusResponse
                {
                    JobId = createResponse.JobId,
                    Status = createResponse.Status,
                    Progress = createResponse.Progress,
                    ScoreId = createResponse.ScoreId,
                    Warnings = new System.Collections.Generic.List<string>()
                };

                UpdateStatusFromJob(status, createResponse.Message);

                for (var attempt = 0; attempt < 40; attempt++)
                {
                    if (string.Equals(status.Status, "succeeded", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(status.Status, "failed", StringComparison.OrdinalIgnoreCase))
                    {
                        break;
                    }

                    await Task.Delay(TimeSpan.FromSeconds(1.5));
                    status = await _analysisApiClient.GetTranscriptionStatusAsync(createResponse.JobId);
                    UpdateStatusFromJob(status, "识谱任务正在处理中...");
                }

                if (string.Equals(status.Status, "failed", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(string.IsNullOrWhiteSpace(status.ErrorMessage)
                        ? "识谱失败，请尝试更清晰的旋律音频。"
                        : status.ErrorMessage);
                }

                if (!string.Equals(status.Status, "succeeded", StringComparison.OrdinalIgnoreCase)
                    || string.IsNullOrWhiteSpace(status.ScoreId))
                {
                    throw new InvalidOperationException("识谱任务仍在处理中，请稍后重试。");
                }

                var score = await _analysisApiClient.GetScoreAsync(status.ScoreId);
                ApplyScore(score);
                TxtTranscriptionStatus.Text = "识谱完成，当前正在展示双手钢琴谱预览。";
                TxtFooterStatus.Text = "识谱完成，现在可以翻页预览并导出 PDF。";
                TranscriptionProgressBar.Value = 100;
            }
            catch (Exception exception)
            {
                TxtTranscriptionStatus.Text = "识谱失败：" + exception.Message;
                TxtFooterStatus.Text = "识谱失败，请检查文件格式或服务状态后重试。";
                MessageBox.Show(exception.Message, "SeeMusic", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void BtnPrevPage_Click(object sender, RoutedEventArgs e)
        {
            if (_currentScore == null || _currentScore.PreviewPages == null || _currentScore.PreviewPages.Count == 0)
            {
                return;
            }

            _currentPageIndex = Math.Max(1, _currentPageIndex - 1);
            RenderCurrentPage();
        }

        private void BtnNextPage_Click(object sender, RoutedEventArgs e)
        {
            if (_currentScore == null || _currentScore.PreviewPages == null || _currentScore.PreviewPages.Count == 0)
            {
                return;
            }

            _currentPageIndex = Math.Min(_currentScore.PreviewPages.Count, _currentPageIndex + 1);
            RenderCurrentPage();
        }

        private async void BtnRefreshClear_Click(object sender, RoutedEventArgs e)
        {
            _selectedAudioPath = null;
            TxtProjectTitle.Text = "我的智能识谱项目";
            TxtSelectedAudio.Text = "未选择任何文件";
            ResetUiState();
            await RefreshHealthAsync();
        }

        private void BtnExportPdf_Click(object sender, RoutedEventArgs e)
        {
            if (_currentScore == null || _currentScore.PreviewPages == null || _currentScore.PreviewPages.Count == 0)
            {
                MessageBox.Show("请先完成一次识谱，生成可预览的钢琴谱后再导出 PDF。", "SeeMusic", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            PrintScoreDocument();
        }

        private async Task<bool> RefreshHealthAsync()
        {
            try
            {
                var health = await _analysisApiClient.GetHealthAsync();
                var isAvailable = health != null && health.ServiceAvailable;
                SetHealthState(isAvailable);
                return isAvailable;
            }
            catch
            {
                SetHealthState(false);
                return false;
            }
        }

        private void SetBusy(bool isBusy)
        {
            BtnGenerateScore.IsEnabled = !isBusy && _isServiceAvailable;
            BtnChooseAudio.IsEnabled = !isBusy;
            BtnRefreshClear.IsEnabled = !isBusy;
            BtnPrevPage.IsEnabled = !isBusy && _currentScore != null && _currentPageIndex > 1;
            BtnNextPage.IsEnabled = !isBusy
                && _currentScore != null
                && _currentScore.PreviewPages != null
                && _currentPageIndex < _currentScore.PreviewPages.Count;
            BtnExportPdf.IsEnabled = !isBusy && _currentScore != null && _currentScore.PreviewPages != null && _currentScore.PreviewPages.Count > 0;
        }

        private void SetHealthState(bool isAvailable)
        {
            _isServiceAvailable = isAvailable;
            HealthStatusDot.Fill = isAvailable
                ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#67B67B"))
                : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E17373"));
            TxtHealthStatus.Text = isAvailable ? "服务可用" : "服务不可用";
            TxtModeStatus.Text = isAvailable ? " 已连接" : " 未连接";

            if (!isAvailable)
            {
                BtnGenerateScore.IsEnabled = false;
                TxtFooterStatus.Text = "后端服务当前不可用，请先启动服务后再开始识谱。";
            }
            else if (_currentScore == null)
            {
                BtnGenerateScore.IsEnabled = true;
                TxtFooterStatus.Text = "服务连接正常，现在可以开始识谱和预览。";
            }
        }

        private void UpdateStatusFromJob(TranscriptionStatusResponse status, string fallbackMessage)
        {
            if (status == null)
            {
                return;
            }

            TranscriptionProgressBar.Value = Math.Max(0, Math.Min(100, status.Progress));
            TxtTempo.Text = status.DetectedTempoBpm.HasValue ? status.DetectedTempoBpm.Value.ToString("0.0") : "--";
            TxtMeter.Text = string.IsNullOrWhiteSpace(status.DetectedTimeSignature) ? "--/--" : status.DetectedTimeSignature;
            TxtMeasures.Text = status.MeasureCount.HasValue ? status.MeasureCount.Value.ToString() : "--";
            TxtTranscriptionStatus.Text = string.IsNullOrWhiteSpace(status.ErrorMessage)
                ? fallbackMessage
                : status.ErrorMessage;
        }

        private void ApplyScore(ScoreDetailResponse score)
        {
            _currentScore = score;
            _currentPageIndex = 1;

            TxtScoreTitle.Text = string.IsNullOrWhiteSpace(score.Title) ? "双手钢琴谱预览" : score.Title;
            TxtScoreSubtitle.Text = "当前页面固定展示只读钢琴谱，使用后端返回的 MusicXML 与 SVG 预览同步浏览双手结果。";
            TxtTempo.Text = score.TempoBpm.HasValue ? score.TempoBpm.Value.ToString("0.0") : "--";
            TxtMeter.Text = string.IsNullOrWhiteSpace(score.TimeSignature) ? "--/--" : score.TimeSignature;
            TxtKey.Text = string.IsNullOrWhiteSpace(score.KeySignature) ? "--" : score.KeySignature;
            TxtMeasures.Text = score.MeasureCount > 0 ? score.MeasureCount.ToString() : "--";
            TxtMelodySummary.Text = score.AnalysisSummary != null && !string.IsNullOrWhiteSpace(score.AnalysisSummary.MelodySummary)
                ? score.AnalysisSummary.MelodySummary
                : "暂无旋律摘要。";
            TxtAccompanimentSummary.Text = score.AnalysisSummary != null && !string.IsNullOrWhiteSpace(score.AnalysisSummary.AccompanimentSummary)
                ? score.AnalysisSummary.AccompanimentSummary
                : "暂无伴奏摘要。";
            TxtAssignmentSummary.Text = score.AnalysisSummary != null && !string.IsNullOrWhiteSpace(score.AnalysisSummary.AssignmentSummary)
                ? score.AnalysisSummary.AssignmentSummary
                : "暂无双手分配说明。";

            WarningsPanel.Children.Clear();
            var warnings = score.Warnings ?? new System.Collections.Generic.List<string>();
            if (warnings.Count == 0)
            {
                WarningsPanel.Children.Add(CreateWarningText("当前结果没有额外警告。"));
            }
            else
            {
                foreach (var warning in warnings.Take(3))
                {
                    WarningsPanel.Children.Add(CreateWarningText("提示：" + warning));
                }
            }

            RenderCurrentPage();
            SetBusy(false);
        }

        private TextBlock CreateWarningText(string text)
        {
            return new TextBlock
            {
                Text = text,
                Margin = new Thickness(0, 0, 0, 6),
                FontSize = 13,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#7B8CA5")),
                TextWrapping = TextWrapping.Wrap
            };
        }

        private void RenderCurrentPage()
        {
            if (_currentScore == null || _currentScore.PreviewPages == null || _currentScore.PreviewPages.Count == 0)
            {
                ScorePreviewBrowser.NavigateToString(BuildEmptyPreviewHtml());
                TxtPageIndicator.Text = "第 1 / 1 页";
                BtnPrevPage.IsEnabled = false;
                BtnNextPage.IsEnabled = false;
                BtnExportPdf.IsEnabled = false;
                return;
            }

            _currentPageIndex = Math.Max(1, Math.Min(_currentPageIndex, _currentScore.PreviewPages.Count));
            var page = _currentScore.PreviewPages[_currentPageIndex - 1];
            ScorePreviewBrowser.NavigateToString(BuildPreviewHtml(page.SvgContent, _currentScore.Title, _currentPageIndex, _currentScore.PreviewPages.Count));
            TxtPageIndicator.Text = string.Format("第 {0} / {1} 页", _currentPageIndex, _currentScore.PreviewPages.Count);
            BtnPrevPage.IsEnabled = _currentPageIndex > 1;
            BtnNextPage.IsEnabled = _currentPageIndex < _currentScore.PreviewPages.Count;
            BtnExportPdf.IsEnabled = true;
        }

        private void PrintScoreDocument()
        {
            var printWindow = new Window
            {
                Width = 1,
                Height = 1,
                Left = -10000,
                Top = -10000,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent
            };

            var browser = new WebBrowser();
            System.Windows.Navigation.LoadCompletedEventHandler handler = null;
            handler = (sender, args) =>
            {
                browser.LoadCompleted -= handler;
                try
                {
                    InvokeBrowserPrint(browser);
                }
                finally
                {
                    printWindow.Close();
                }
            };

            browser.LoadCompleted += handler;
            printWindow.Content = browser;
            printWindow.Show();
            browser.NavigateToString(BuildPrintHtml());
        }

        private static void InvokeBrowserPrint(WebBrowser browser)
        {
            try
            {
                var activeX = browser.GetType().InvokeMember(
                    "ActiveXInstance",
                    BindingFlags.GetProperty | BindingFlags.Instance | BindingFlags.NonPublic,
                    null,
                    browser,
                    new object[] { });
                activeX.GetType().InvokeMember("ExecWB", BindingFlags.InvokeMethod, null, activeX, new object[] { 6, 1 });
            }
            catch
            {
            }
        }

        private string BuildPrintHtml()
        {
            var builder = new StringBuilder();
            builder.AppendLine("<html><head><meta http-equiv=\"X-UA-Compatible\" content=\"IE=11\"/>");
            builder.AppendLine("<style>");
            builder.AppendLine("body{margin:0;padding:24px;background:#f4f7fb;font-family:'Segoe UI';}");
            builder.AppendLine(".page{width:1040px;margin:0 auto 26px auto;padding:18px;background:white;page-break-after:always;}");
            builder.AppendLine(".page:last-child{page-break-after:auto;}");
            builder.AppendLine("</style></head><body>");

            foreach (var page in _currentScore.PreviewPages)
            {
                builder.AppendLine("<div class=\"page\">");
                builder.AppendLine(page.SvgContent);
                builder.AppendLine("</div>");
            }

            builder.AppendLine("</body></html>");
            return builder.ToString();
        }

        private static string BuildPreviewHtml(string svgContent, string title, int pageIndex, int totalPages)
        {
            return string.Format(
                "<html><head><meta http-equiv=\"X-UA-Compatible\" content=\"IE=11\"/><style>body{{margin:0;background:#F7FBFF;font-family:'Segoe UI';}}.wrap{{padding:12px 10px 20px 10px;}}.meta{{padding:6px 10px 16px 18px;color:#71809B;font-size:13px;}}svg{{display:block;margin:0 auto;box-shadow:0 22px 60px rgba(44,72,104,0.08);background:white;}}</style></head><body><div class='wrap'><div class='meta'>{0} · 第 {1} / {2} 页</div>{3}</div></body></html>",
                EscapeHtml(string.IsNullOrWhiteSpace(title) ? "钢琴双手谱预览" : title),
                pageIndex,
                totalPages,
                svgContent ?? string.Empty);
        }

        private static string BuildEmptyPreviewHtml()
        {
            return "<html><head><meta http-equiv=\"X-UA-Compatible\" content=\"IE=11\"/><style>body{margin:0;background:#F7FBFF;font-family:'Segoe UI';display:flex;align-items:center;justify-content:center;height:100%;color:#8A98AE;} .box{padding:24px 32px;border:1px dashed #D3DFEB;border-radius:20px;background:white;}</style></head><body><div class='box'>完成一次识谱后，这里会展示只读钢琴谱预览。</div></body></html>";
        }

        private void ResetUiState()
        {
            ClearScoreOnly();
            TranscriptionProgressBar.Value = 0;
            TxtTempo.Text = "--";
            TxtMeter.Text = "--/--";
            TxtKey.Text = "--";
            TxtMeasures.Text = "--";
            TxtScoreTitle.Text = "双手钢琴谱预览";
            TxtScoreSubtitle.Text = "当前页面会固定展示只读钢琴谱，并同步展示拍号、速度与双手分配摘要。";
            TxtMelodySummary.Text = "等待识谱结果。";
            TxtAccompanimentSummary.Text = "等待识谱结果。";
            TxtAssignmentSummary.Text = "生成后会在这里展示双手分配与处理说明。";
            WarningsPanel.Children.Clear();
            WarningsPanel.Children.Add(CreateWarningText("当前还没有识谱结果。"));
            TxtTranscriptionStatus.Text = "等待选择音频文件。";
            TxtFooterStatus.Text = "服务连接正常，现在可以开始识谱和预览。";
            RenderCurrentPage();
        }

        private void ClearScoreOnly()
        {
            _currentScore = null;
            _currentPageIndex = 1;
            ScorePreviewBrowser.NavigateToString(BuildEmptyPreviewHtml());
            TxtPageIndicator.Text = "第 1 / 1 页";
            BtnPrevPage.IsEnabled = false;
            BtnNextPage.IsEnabled = false;
            BtnExportPdf.IsEnabled = false;
        }

        private static string EscapeHtml(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return value
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;");
        }
    }
}
