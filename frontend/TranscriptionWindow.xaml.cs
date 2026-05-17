using Microsoft.Win32;
using SeeMusicApp.Models;
using SeeMusicApp.Services;
using System;
using System.Collections.Generic;
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
        private bool _isBusy;

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

                ApplySelectionState();
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
            TxtFooterStatus.Text = "识谱任务已开始，工作台会沿着连续卡片同步刷新处理进度。";
            ApplyProcessingState(TxtTranscriptionStatus.Text);

            try
            {
                var upload = await _analysisApiClient.UploadAudioAsync(_selectedAudioPath);
                TranscriptionProgressBar.Value = 18;

                var title = GetProjectDisplayTitle();
                var createResponse = await _analysisApiClient.CreatePianoTranscriptionAsync(upload.MediaId, title);
                var status = new TranscriptionStatusResponse
                {
                    JobId = createResponse.JobId,
                    Status = createResponse.Status,
                    Progress = createResponse.Progress,
                    ScoreId = createResponse.ScoreId,
                    Warnings = new List<string>()
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
                ApplyFailureState(exception.Message);
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
            _isBusy = isBusy;
            UpdateActionStates();
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
                TxtFooterStatus.Text = "后端服务当前不可用，请先启动服务后再开始识谱。";
            }
            else if (_currentScore == null && !_isBusy)
            {
                TxtFooterStatus.Text = "服务连接正常，现在可以开始识谱和预览。";
            }

            UpdateActionStates();
        }

        private void UpdateActionStates()
        {
            BtnGenerateScore.IsEnabled = !_isBusy && _isServiceAvailable;
            BtnChooseAudio.IsEnabled = !_isBusy;
            BtnRefreshClear.IsEnabled = !_isBusy;

            var hasPreview = _currentScore != null
                && _currentScore.PreviewPages != null
                && _currentScore.PreviewPages.Count > 0;

            BtnPrevPage.IsEnabled = !_isBusy && hasPreview && _currentPageIndex > 1;
            BtnNextPage.IsEnabled = !_isBusy && hasPreview && _currentPageIndex < _currentScore.PreviewPages.Count;
            BtnExportPdf.IsEnabled = !_isBusy && hasPreview;
        }

        private void UpdateStatusFromJob(TranscriptionStatusResponse status, string fallbackMessage)
        {
            if (status == null)
            {
                return;
            }

            TranscriptionProgressBar.Value = Math.Max(0, Math.Min(100, status.Progress));
            var displayMessage = string.IsNullOrWhiteSpace(status.ErrorMessage)
                ? fallbackMessage
                : status.ErrorMessage;
            TxtTranscriptionStatus.Text = string.IsNullOrWhiteSpace(displayMessage)
                ? "识谱任务正在处理中..."
                : displayMessage;

            ApplyProcessingState(TxtTranscriptionStatus.Text, status);
        }

        private void ApplyScore(ScoreDetailResponse score)
        {
            _currentScore = score;
            _currentPageIndex = 1;

            TxtScoreTitle.Text = "双手钢琴谱预览";
            TxtScoreSubtitle.Text = "当前页面固定展示只读双手钢琴谱，并沿着连续卡片继续展开轨道构建、结构和双手分配结果。";
            TxtHeroProjectTitle.Text = string.IsNullOrWhiteSpace(score.Title) ? GetProjectDisplayTitle() : score.Title;
            TxtHeroDescription.Text = BuildHeroDescription(score);
            TxtHeroMeasuresChip.Text = FormatMeasureChip(score.MeasureCount);
            TxtHeroPagesChip.Text = FormatPageChip(GetEstimatedPageCount(score));
            TxtHeroViewerChip.Text = BuildViewerChip(score.PreviewRenderMode);
            TxtPreviewCardHint.Text = BuildPreviewHint(score);

            TxtTempo.Text = FormatTempo(score.TempoBpm);
            TxtMeter.Text = FormatMeter(score.TimeSignature);
            TxtKey.Text = FormatKey(score.KeySignature);
            TxtMeasures.Text = FormatCount(score.MeasureCount);

            TxtStructureTempo.Text = FormatTempo(score.TempoBpm);
            TxtStructureMeter.Text = FormatMeter(score.TimeSignature);
            TxtStructureKey.Text = FormatKey(score.KeySignature);
            TxtStructureMeasures.Text = FormatCount(score.MeasureCount);
            TxtStructurePages.Text = FormatCount(GetEstimatedPageCount(score));

            TxtMelodySummary.Text = score.AnalysisSummary != null && !string.IsNullOrWhiteSpace(score.AnalysisSummary.MelodySummary)
                ? score.AnalysisSummary.MelodySummary
                : "暂无旋律摘要。";
            TxtAccompanimentSummary.Text = score.AnalysisSummary != null && !string.IsNullOrWhiteSpace(score.AnalysisSummary.AccompanimentSummary)
                ? score.AnalysisSummary.AccompanimentSummary
                : "暂无伴奏摘要。";
            TxtAssignmentSummary.Text = score.AnalysisSummary != null && !string.IsNullOrWhiteSpace(score.AnalysisSummary.AssignmentSummary)
                ? score.AnalysisSummary.AssignmentSummary
                : "暂无双手分配说明。";

            PopulateTrackCards(score.Tracks, "当前结果暂时没有可展示的轨道构建摘要。");
            PopulateWarnings(score.Warnings, "当前结果没有额外提示。", BuildResultInsights(score));

            RenderCurrentPage();
        }

        private void ApplySelectionState()
        {
            TxtScoreTitle.Text = "双手钢琴谱预览";
            TxtScoreSubtitle.Text = "右侧结果会按连续卡片工作台展开，先看谱面，再继续查看轨道构建、结构与双手分配。";
            TxtHeroProjectTitle.Text = GetProjectDisplayTitle();
            TxtHeroDescription.Text = "音频已经准备完成，点击“生成双手钢琴谱”后，工作台会从谱面预览一路串联到轨道构建、结构和双手分配结果。";
            TxtHeroViewerChip.Text = "支持分页与 PDF 导出";
            TxtPreviewCardHint.Text = "当前预览区正在等待本次识谱结果，生成完成后会自动切换到 SVG 纸面谱面浏览。";
        }

        private void ApplyProcessingState(string message, TranscriptionStatusResponse status = null)
        {
            TxtScoreTitle.Text = "双手钢琴谱预览";
            TxtScoreSubtitle.Text = "系统正在沿着连续卡片工作台刷新当前项目的识谱进度、结构摘要和谱面预览。";
            TxtHeroProjectTitle.Text = GetProjectDisplayTitle();
            TxtHeroDescription.Text = string.IsNullOrWhiteSpace(message) ? "识谱任务正在处理中..." : message;
            TxtHeroMeasuresChip.Text = status != null && status.MeasureCount.HasValue
                ? FormatMeasureChip(status.MeasureCount.Value)
                : "共 -- 小节";
            TxtHeroPagesChip.Text = status != null && status.EstimatedPageCount.HasValue
                ? FormatPageChip(status.EstimatedPageCount.Value)
                : "预计 -- 页";
            TxtHeroViewerChip.Text = "结果返回后支持分页预览";
            TxtPreviewCardHint.Text = "系统正在生成 MusicXML 与 SVG 预览，成功后会直接在这里显示当前谱面。";

            TxtTempo.Text = status != null && status.DetectedTempoBpm.HasValue ? FormatTempo(status.DetectedTempoBpm) : "--";
            TxtMeter.Text = status != null ? FormatMeter(status.DetectedTimeSignature) : "--/--";
            TxtKey.Text = "--";
            TxtMeasures.Text = status != null && status.MeasureCount.HasValue ? FormatCount(status.MeasureCount.Value) : "--";

            TxtStructureTempo.Text = status != null && status.DetectedTempoBpm.HasValue ? FormatTempo(status.DetectedTempoBpm) : "--";
            TxtStructureMeter.Text = status != null ? FormatMeter(status.DetectedTimeSignature) : "--/--";
            TxtStructureKey.Text = "--";
            TxtStructureMeasures.Text = status != null && status.MeasureCount.HasValue ? FormatCount(status.MeasureCount.Value) : "--";
            TxtStructurePages.Text = status != null && status.EstimatedPageCount.HasValue ? FormatCount(status.EstimatedPageCount.Value) : "--";

            TxtMelodySummary.Text = "系统正在提取主旋律与右手线条，请稍候。";
            TxtAccompanimentSummary.Text = "系统正在整理左手织体和伴奏骨架。";
            TxtAssignmentSummary.Text = "系统正在生成左右手分配与处理说明。";

            PopulateTrackCards(status != null ? status.TrackSummaries : null, "运行轨道构建后，这里会展示左右手轨道、音域和处理摘要。");
            PopulateWarnings(status != null ? status.Warnings : null, "任务处理中，当前还没有额外提示。", BuildStatusInsights(status));
        }

        private void ApplyFailureState(string errorMessage)
        {
            TxtScoreTitle.Text = "双手钢琴谱预览";
            TxtScoreSubtitle.Text = "本次识谱未成功完成，你可以修正音频或服务状态后重新发起识谱。";
            TxtHeroProjectTitle.Text = GetProjectDisplayTitle();
            TxtHeroDescription.Text = "当前流程未能生成完整乐谱：" + errorMessage;
            TxtHeroViewerChip.Text = "修正后可重新生成预览";
            TxtPreviewCardHint.Text = "当前没有可展示的谱面预览，请检查音频质量和服务状态后重试。";
            TxtAssignmentSummary.Text = "当前没有可用的双手分配说明。";

            PopulateTrackCards(null, "这次识谱没有成功生成轨道构建结果。");
            PopulateWarnings(new[] { "请检查音频清晰度、时长与后端服务状态后重试。" }, null);
        }

        private void PopulateTrackCards(IEnumerable<ScoreTrackResponse> tracks, string emptyMessage)
        {
            TracksPanel.Children.Clear();
            var trackList = tracks == null
                ? new List<ScoreTrackResponse>()
                : tracks.Where(track => track != null).Take(6).ToList();

            if (trackList.Count == 0)
            {
                TracksPanel.Children.Add(CreatePlaceholderCard(emptyMessage));
                return;
            }

            foreach (var track in trackList)
            {
                TracksPanel.Children.Add(CreateTrackCard(track));
            }
        }

        private void PopulateWarnings(IEnumerable<string> warnings, string emptyMessage, IEnumerable<string> insights = null)
        {
            WarningsPanel.Children.Clear();
            var insightList = insights == null
                ? new List<string>()
                : insights.Where(text => !string.IsNullOrWhiteSpace(text)).Take(4).ToList();
            var warningList = warnings == null
                ? new List<string>()
                : warnings.Where(warning => !string.IsNullOrWhiteSpace(warning)).Take(4).ToList();

            foreach (var insight in insightList)
            {
                WarningsPanel.Children.Add(CreateWarningPill(insight, true));
            }

            if (warningList.Count == 0)
            {
                if (insightList.Count == 0 && !string.IsNullOrWhiteSpace(emptyMessage))
                {
                    WarningsPanel.Children.Add(CreateWarningPill(emptyMessage, true));
                }

                return;
            }

            foreach (var warning in warningList)
            {
                WarningsPanel.Children.Add(CreateWarningPill("提示：" + warning, false));
            }
        }

        private Border CreatePlaceholderCard(string message)
        {
            return new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FAFCFF")),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DCE8F4")),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(20),
                Padding = new Thickness(18),
                Margin = new Thickness(0, 0, 14, 14),
                Width = 760,
                Child = new TextBlock
                {
                    Text = string.IsNullOrWhiteSpace(message)
                        ? "当前还没有可展示的识谱结果。"
                        : message,
                    FontSize = 14,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#7B8CA5")),
                    TextWrapping = TextWrapping.Wrap
                }
            };
        }

        private Border CreateTrackCard(ScoreTrackResponse track)
        {
            var container = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FAFCFF")),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DCE8F4")),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(20),
                Padding = new Thickness(18),
                Margin = new Thickness(0, 0, 14, 14),
                Width = 330
            };

            var content = new StackPanel();
            var badges = new WrapPanel();
            badges.Children.Add(CreateMetaPill(FormatHandRole(track.HandRole), "#EEF6FF", "#315A86", "#D2E4F5"));

            var trackOriginLabel = FormatTrackOrigin(track.Origin, track.IsGenerated);
            if (!string.IsNullOrWhiteSpace(trackOriginLabel))
            {
                badges.Children.Add(CreateMetaPill(trackOriginLabel, "#F8F1FF", "#745B97", "#E4D8F4"));
            }

            content.Children.Add(badges);
            content.Children.Add(new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(track.Name) ? "未命名音轨" : track.Name,
                Margin = new Thickness(0, 14, 0, 0),
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1F3F63")),
                TextWrapping = TextWrapping.Wrap
            });
            content.Children.Add(new TextBlock
            {
                Text = "乐器：" + (string.IsNullOrWhiteSpace(track.Instrument) ? "双手钢琴" : track.Instrument),
                Margin = new Thickness(0, 10, 0, 0),
                FontSize = 13,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#7B8CA5")),
                TextWrapping = TextWrapping.Wrap
            });
            content.Children.Add(new TextBlock
            {
                Text = string.Format("音符 {0} · 音域 {1}", track.NoteCount, FormatMidiRange(track.RangeLowMidi, track.RangeHighMidi)),
                Margin = new Thickness(0, 8, 0, 0),
                FontSize = 13,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#7B8CA5")),
                TextWrapping = TextWrapping.Wrap
            });
            content.Children.Add(new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(track.SummaryText) ? "当前音轨还没有额外摘要。" : track.SummaryText,
                Margin = new Thickness(0, 14, 0, 0),
                FontSize = 14,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#526782")),
                TextWrapping = TextWrapping.Wrap
            });

            container.Child = content;
            return container;
        }

        private Border CreateMetaPill(string text, string backgroundHex, string foregroundHex, string borderHex)
        {
            return new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(backgroundHex)),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(borderHex)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(12, 5, 12, 5),
                Margin = new Thickness(0, 0, 10, 8),
                Child = new TextBlock
                {
                    Text = text,
                    FontSize = 12,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(foregroundHex))
                }
            };
        }

        private Border CreateWarningPill(string text, bool isNeutral)
        {
            return new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(isNeutral ? "#F7FBFF" : "#FFF7ED")),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(isNeutral ? "#DCE8F4" : "#F3D4AA")),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(18),
                Padding = new Thickness(16, 10, 16, 10),
                Margin = new Thickness(0, 0, 12, 12),
                Child = new TextBlock
                {
                    Text = text,
                    FontSize = 13,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(isNeutral ? "#6D7D93" : "#91663D")),
                    TextWrapping = TextWrapping.Wrap
                }
            };
        }

        private void RenderCurrentPage()
        {
            if (_currentScore == null || _currentScore.PreviewPages == null || _currentScore.PreviewPages.Count == 0)
            {
                ScorePreviewBrowser.NavigateToString(BuildEmptyPreviewHtml());
                TxtPageIndicator.Text = "第 1 / 1 页";
                UpdateActionStates();
                return;
            }

            _currentPageIndex = Math.Max(1, Math.Min(_currentPageIndex, _currentScore.PreviewPages.Count));
            var page = _currentScore.PreviewPages[_currentPageIndex - 1];
            ScorePreviewBrowser.NavigateToString(BuildPreviewHtml(page.SvgContent, _currentScore.Title, _currentPageIndex, _currentScore.PreviewPages.Count));
            TxtPageIndicator.Text = string.Format("第 {0} / {1} 页", _currentPageIndex, _currentScore.PreviewPages.Count);
            UpdateActionStates();
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
            ApplyIdleWorkspaceState();
            TxtTranscriptionStatus.Text = "等待选择音频文件。";
            TxtFooterStatus.Text = _isServiceAvailable
                ? "服务连接正常，现在可以开始识谱和预览。"
                : "后端服务当前不可用，请先启动服务后再开始识谱。";
            UpdateActionStates();
        }

        private void ClearScoreOnly()
        {
            _currentScore = null;
            _currentPageIndex = 1;
            RenderCurrentPage();
        }

        private void ApplyIdleWorkspaceState()
        {
            TxtTempo.Text = "--";
            TxtMeter.Text = "--/--";
            TxtKey.Text = "--";
            TxtMeasures.Text = "--";

            TxtScoreTitle.Text = "双手钢琴谱预览";
            TxtScoreSubtitle.Text = "右侧结果会按连续卡片工作台展开，先看谱面，再继续查看轨道构建、结构与双手分配。";
            TxtHeroProjectTitle.Text = GetProjectDisplayTitle();
            TxtHeroDescription.Text = "选择音频并开始识谱后，这里会展示 MusicXML 结果、分页说明与 SVG 纸面预览。";
            TxtHeroMeasuresChip.Text = "共 -- 小节";
            TxtHeroPagesChip.Text = "预计 -- 页";
            TxtHeroViewerChip.Text = "支持分页与 PDF 导出";
            TxtPreviewCardHint.Text = "完成一次识谱后，这里会显示只读钢琴谱预览，并和当前页码导航、导出操作保持同步。";

            TxtStructureTempo.Text = "--";
            TxtStructureMeter.Text = "--/--";
            TxtStructureKey.Text = "--";
            TxtStructureMeasures.Text = "--";
            TxtStructurePages.Text = "--";

            TxtMelodySummary.Text = "等待识谱结果。";
            TxtAccompanimentSummary.Text = "等待识谱结果。";
            TxtAssignmentSummary.Text = "生成后会在这里展示双手分配与处理说明。";

            PopulateTrackCards(null, "运行轨道构建后，这里会展示左右手轨道、音域和处理摘要。");
            PopulateWarnings(null, "当前还没有分配提示。");
        }

        private string GetProjectDisplayTitle()
        {
            var title = string.IsNullOrWhiteSpace(TxtProjectTitle.Text)
                ? string.Empty
                : TxtProjectTitle.Text.Trim();
            return string.IsNullOrWhiteSpace(title) ? "我的智能识谱项目" : title;
        }

        private static int GetEstimatedPageCount(ScoreDetailResponse score)
        {
            if (score == null)
            {
                return 0;
            }

            if (score.EstimatedPageCount > 0)
            {
                return score.EstimatedPageCount;
            }

            return score.PreviewPages == null ? 0 : score.PreviewPages.Count;
        }

        private static string FormatTempo(double? tempo)
        {
            return tempo.HasValue ? tempo.Value.ToString("0.0") : "--";
        }

        private static string FormatMeter(string meter)
        {
            return string.IsNullOrWhiteSpace(meter) ? "--/--" : meter;
        }

        private static string FormatKey(string key)
        {
            return string.IsNullOrWhiteSpace(key) ? "--" : key;
        }

        private static string FormatCount(int value)
        {
            return value > 0 ? value.ToString() : "--";
        }

        private static string FormatMeasureChip(int measureCount)
        {
            return measureCount > 0 ? string.Format("共 {0} 小节", measureCount) : "共 -- 小节";
        }

        private static string FormatPageChip(int pageCount)
        {
            return pageCount > 0 ? string.Format("预计 {0} 页", pageCount) : "预计 -- 页";
        }

        private static string BuildHeroDescription(ScoreDetailResponse score)
        {
            var previewText = string.Equals(score.PreviewRenderMode, "generated_svg_projection", StringComparison.OrdinalIgnoreCase)
                ? "当前谱面预览采用后端 SVG 纸面投影"
                : "当前谱面预览已经生成";
            var keyConfidenceText = score.KeyConfidence.HasValue
                ? string.Format("，调性识别置信度约 {0}", FormatPercentage(score.KeyConfidence.Value))
                : string.Empty;
            return previewText + "，并已同步返回 MusicXML 结果、分页信息与双手轨道摘要" + keyConfidenceText + "。";
        }

        private static string BuildPreviewHint(ScoreDetailResponse score)
        {
            if (string.Equals(score.PreviewRenderMode, "generated_svg_projection", StringComparison.OrdinalIgnoreCase))
            {
                return "当前预览采用后端 SVG 纸面投影；MusicXML 结果、分页摘要与 PDF 导出会围绕同一份识谱结果同步更新。";
            }

            return "当前页面固定展示带左手伴奏的双手钢琴谱，翻页、预览和 PDF 导出会使用同一份结果。";
        }

        private static string BuildViewerChip(string previewRenderMode)
        {
            return string.Equals(previewRenderMode, "generated_svg_projection", StringComparison.OrdinalIgnoreCase)
                ? "当前预览采用 SVG 纸面投影"
                : "支持分页与 PDF 导出";
        }

        private static IEnumerable<string> BuildResultInsights(ScoreDetailResponse score)
        {
            var insights = new List<string>();
            if (!string.IsNullOrWhiteSpace(score.TrackBuildMode))
            {
                insights.Add("当前轨道模式：" + FormatTrackBuildMode(score.TrackBuildMode));
            }

            if (score.AnalysisSummary != null)
            {
                if (!string.IsNullOrWhiteSpace(score.AnalysisSummary.TrackBuildSummary))
                {
                    insights.Add(score.AnalysisSummary.TrackBuildSummary);
                }

                if (!string.IsNullOrWhiteSpace(score.AnalysisSummary.RhythmSummary))
                {
                    insights.Add(score.AnalysisSummary.RhythmSummary);
                }
            }

            if (score.KeyConfidence.HasValue)
            {
                insights.Add("调性识别置信度：" + FormatPercentage(score.KeyConfidence.Value));
            }

            if (score.TimeSignatureConfidence.HasValue)
            {
                insights.Add("拍号识别置信度：" + FormatPercentage(score.TimeSignatureConfidence.Value));
            }

            if (!string.IsNullOrWhiteSpace(score.RhythmGridSource))
            {
                insights.Add("节拍网格来源：" + FormatRhythmGridSource(score.RhythmGridSource));
            }

            return insights;
        }

        private static IEnumerable<string> BuildStatusInsights(TranscriptionStatusResponse status)
        {
            if (status == null)
            {
                return Array.Empty<string>();
            }

            var insights = new List<string>();
            if (!string.IsNullOrWhiteSpace(status.TrackBuildMode))
            {
                insights.Add("当前轨道模式：" + FormatTrackBuildMode(status.TrackBuildMode));
            }

            if (!string.IsNullOrWhiteSpace(status.RhythmGridSource))
            {
                insights.Add("当前节拍网格：" + FormatRhythmGridSource(status.RhythmGridSource));
            }

            if (status.DetectedTimeSignatureConfidence.HasValue)
            {
                insights.Add("拍号识别置信度：" + FormatPercentage(status.DetectedTimeSignatureConfidence.Value));
            }

            return insights;
        }

        private static string FormatTrackOrigin(string origin, bool isGenerated)
        {
            if (string.Equals(origin, "extracted_melody", StringComparison.OrdinalIgnoreCase))
            {
                return "旋律提取";
            }

            if (string.Equals(origin, "arranged_accompaniment", StringComparison.OrdinalIgnoreCase))
            {
                return "规则编配";
            }

            return isGenerated ? "自动生成" : string.Empty;
        }

        private static string FormatTrackBuildMode(string trackBuildMode)
        {
            return string.Equals(trackBuildMode, "melody_extraction_only", StringComparison.OrdinalIgnoreCase)
                ? "仅旋律提取"
                : string.Equals(trackBuildMode, "melody_extraction_with_arranged_accompaniment", StringComparison.OrdinalIgnoreCase)
                    ? "旋律提取 + 左手编配"
                    : trackBuildMode;
        }

        private static string FormatRhythmGridSource(string rhythmGridSource)
        {
            if (string.Equals(rhythmGridSource, "detected", StringComparison.OrdinalIgnoreCase))
            {
                return "检测拍点";
            }

            if (string.Equals(rhythmGridSource, "pause_estimated", StringComparison.OrdinalIgnoreCase))
            {
                return "按旋律停顿估算";
            }

            if (string.Equals(rhythmGridSource, "default_fallback", StringComparison.OrdinalIgnoreCase))
            {
                return "默认 96 BPM / 4/4";
            }

            if (string.Equals(rhythmGridSource, "disabled", StringComparison.OrdinalIgnoreCase))
            {
                return "已关闭节拍识别";
            }

            return rhythmGridSource;
        }

        private static string FormatPercentage(double value)
        {
            return string.Format("{0:0}%", Clamp01(value) * 100.0);
        }

        private static double Clamp01(double value)
        {
            if (value < 0.0)
            {
                return 0.0;
            }

            if (value > 1.0)
            {
                return 1.0;
            }

            return value;
        }

        private static string FormatHandRole(string handRole)
        {
            switch ((handRole ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "left":
                case "lh":
                    return "左手";
                case "right":
                case "rh":
                    return "右手";
                case "both":
                    return "双手";
                default:
                    return string.IsNullOrWhiteSpace(handRole) ? "待分配" : handRole;
            }
        }

        private static string FormatMidiRange(int? lowMidi, int? highMidi)
        {
            if (!lowMidi.HasValue && !highMidi.HasValue)
            {
                return "待分析";
            }

            if (lowMidi.HasValue && highMidi.HasValue)
            {
                return string.Format("{0} - {1}", FormatMidiNote(lowMidi.Value), FormatMidiNote(highMidi.Value));
            }

            return FormatMidiNote(lowMidi ?? highMidi ?? 60);
        }

        private static string FormatMidiNote(int midi)
        {
            var names = new[] { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
            var noteIndex = ((midi % 12) + 12) % 12;
            var octave = (midi / 12) - 1;
            return names[noteIndex] + octave;
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
