using Microsoft.Win32;
using SeeMusicApp.Models;
using SeeMusicApp.Services;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace SeeMusicApp
{
    public partial class SingingEvaluationWindow : Window
    {
        private readonly AnalysisApiClient _analysisApiClient = new AnalysisApiClient();
        private string _selectedPerformanceAudioPath;
        private string _selectedReferenceAudioPath;
        private string _currentEvaluationId;
        private string _currentAnonymousAccessToken;
        private EvaluationReportResponse _currentReport;

        public SingingEvaluationWindow()
        {
            InitializeComponent();
            ResetView();
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

        private void BtnChooseReferenceAudio_Click(object sender, RoutedEventArgs e)
        {
            var filePath = PickAudioFile("选择标准音频");
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            _selectedReferenceAudioPath = filePath;
            TxtSelectedReferenceAudio.Text = Path.GetFileName(filePath);
            UpdateSelectedAudioStatus();
        }

        private void BtnChoosePerformanceAudio_Click(object sender, RoutedEventArgs e)
        {
            var filePath = PickAudioFile("选择用户演唱音频");
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            _selectedPerformanceAudioPath = filePath;
            TxtSelectedPerformanceAudio.Text = Path.GetFileName(filePath);
            UpdateSelectedAudioStatus();
        }

        private async void BtnStartEval_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_selectedReferenceAudioPath) || !File.Exists(_selectedReferenceAudioPath))
            {
                MessageBox.Show("请先上传标准音频。", "SeeMusic AI", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (string.IsNullOrWhiteSpace(_selectedPerformanceAudioPath) || !File.Exists(_selectedPerformanceAudioPath))
            {
                MessageBox.Show("请先上传用户演唱音频。", "SeeMusic AI", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var rhythmThreshold = ParseRhythmThreshold();
            SetBusyState(true);
            TxtEvalStatus.Text = string.Format("正在连接 {0} 并提交评估任务...", _analysisApiClient.GetBackendBaseUrl());

            try
            {
                var workflow = await _analysisApiClient.SubmitSingingEvaluationAsync(new SingingEvaluationRequest
                {
                    PerformanceFilePath = _selectedPerformanceAudioPath,
                    ReferenceFilePath = _selectedReferenceAudioPath,
                    UserAudioType = GetSelectedTag(ComboUserAudioType, "with_accompaniment"),
                    FeedbackLanguage = GetSelectedTag(ComboFeedbackLanguage, "zh-CN"),
                    ScoringModel = GetSelectedTag(ComboScoringModel, "balanced"),
                    RhythmThresholdMs = rhythmThreshold,
                    AnalyzePitch = true,
                    AnalyzeRhythm = true
                });

                _currentEvaluationId = workflow.Submit != null ? workflow.Submit.EvaluationId : null;
                _currentAnonymousAccessToken = workflow.Submit != null ? workflow.Submit.AnonymousAccessToken : null;
                _currentReport = workflow.Report;

                ApplyEvaluationResult(workflow.Report);
                TxtEvalStatus.Text = _currentReport != null && _currentReport.Summary != null
                    ? string.Format("评估完成：分析 ID {0}", _currentReport.Summary.AnalysisId)
                    : "评估完成。";
            }
            catch (Exception exception)
            {
                TxtEvalStatus.Text = string.Format("评估失败：{0}", exception.Message);
                MessageBox.Show(exception.Message, "歌唱评估失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                SetBusyState(false);
            }
        }

        private async void BtnGenerateTransposeSuggestion_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_currentEvaluationId))
            {
                MessageBox.Show("请先完成一次评估，再生成变调建议。", "SeeMusic AI", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            BtnGenerateTransposeSuggestion.IsEnabled = false;
            TxtTransposeSuggestionSummary.Text = "正在根据当前调性和目标声线生成变调建议...";

            try
            {
                var response = await _analysisApiClient.GetTransposeSuggestionAsync(
                    _currentEvaluationId,
                    _currentAnonymousAccessToken,
                    GetSelectedTag(ComboSourceGender, "male"),
                    GetSelectedTag(ComboTargetGender, "female"));

                ApplyTransposeSuggestion(response);
            }
            catch (Exception exception)
            {
                TxtTransposeSuggestionSummary.Text = exception.Message;
            }
            finally
            {
                BtnGenerateTransposeSuggestion.IsEnabled = true;
            }
        }

        private void BtnExportPdf_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("当前演示版前端还没有接入登录态，登录后才可导出 PDF 报告。", "SeeMusic AI", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ResetView()
        {
            TxtSummaryAnalysisId.Text = "--";
            TxtSummaryReference.Text = "--";
            TxtSummaryUserBpm.Text = "--";
            TxtSummaryReferenceBpm.Text = "--";

            TxtOverallScore.Text = "--";
            TxtEvaluationBadge.Text = "等待分析";
            TxtPitchScoreLabel.Text = "音高准确度 --";
            TxtRhythmScoreLabel.Text = "节奏准确度 --";
            TxtCoverageLabel.Text = "覆盖率 --";
            TxtConsistencyLabel.Text = "一致性 --";
            TxtMeanDeviationLabel.Text = "平均偏差 --";

            PitchScoreProgress.Value = 0;
            RhythmScoreProgress.Value = 0;
            CoverageProgress.Value = 0;
            ConsistencyProgress.Value = 0;

            TxtPitchMetricScore.Text = "--";
            TxtPitchMetricDeviation.Text = "--";
            TxtPitchMetricHit25.Text = "--";
            TxtPitchMetricHit50.Text = "--";

            TxtCorrectionSuggestion.Text = "等待后端返回分析结果。";
            TxtRhythmDetailHint.Text = "完成一次评估后，这里会展示后端返回的节奏误差分类与说明。";
            TxtDetectedKey.Text = "--";
            TxtTransposeBaseSummary.Text = "完成一次评估后，系统会自动识别标准音频的当前调性，并直接用于生成变调建议。";
            TxtTransposeSuggestionTitle.Text = "先完成一次评估";
            TxtTransposeSuggestionSummary.Text = "系统会自动识别当前调性，并生成适合目标声线的变调建议。";

            DrawPitchChart(null);
            RenderRhythmSegments(null, 0, null);
            RenderTips(TransposeTipsPanel, new string[0], "#667085");
            UpdateExportAvailability();
        }

        private void UpdateSelectedAudioStatus()
        {
            var hasReference = !string.IsNullOrWhiteSpace(_selectedReferenceAudioPath) && File.Exists(_selectedReferenceAudioPath);
            var hasPerformance = !string.IsNullOrWhiteSpace(_selectedPerformanceAudioPath) && File.Exists(_selectedPerformanceAudioPath);

            if (hasReference && hasPerformance)
            {
                TxtEvalStatus.Text = "标准音频与用户演唱音频均已选择，可以开始对比评估。";
                return;
            }

            if (hasReference)
            {
                TxtEvalStatus.Text = "标准音频已选择，请继续上传用户演唱音频。";
                return;
            }

            if (hasPerformance)
            {
                TxtEvalStatus.Text = "用户演唱音频已选择，请继续上传标准音频。";
                return;
            }

            TxtEvalStatus.Text = "请先上传标准音频与用户演唱音频，再开始对比评估。";
        }

        private void SetBusyState(bool isBusy)
        {
            BtnStartEval.IsEnabled = !isBusy;
            BtnChooseReferenceAudio.IsEnabled = !isBusy;
            BtnChoosePerformanceAudio.IsEnabled = !isBusy;
            ComboUserAudioType.IsEnabled = !isBusy;
            ComboFeedbackLanguage.IsEnabled = !isBusy;
            ComboScoringModel.IsEnabled = !isBusy;
            TxtRhythmThreshold.IsEnabled = !isBusy;
        }

        private void ApplyEvaluationResult(EvaluationReportResponse report)
        {
            if (report == null || report.Summary == null)
            {
                ResetView();
                TxtEvalStatus.Text = "当前评估结果不可用。";
                return;
            }

            var summary = report.Summary;
            var pitchAnalysis = report.PitchAnalysis ?? new PitchAnalysis();
            var rhythmAnalysis = report.RhythmAnalysis ?? new RhythmAnalysis();
            var overallScore = ToDisplayScore(summary.TotalScore);
            var pitchScore = ToDisplayScore(summary.PitchScore);
            var rhythmScore = ToDisplayScore(summary.RhythmScore);
            var coverage = ToDisplayScore(summary.Coverage);
            var consistency = ToDisplayScore(summary.Consistency);

            TxtSummaryAnalysisId.Text = string.IsNullOrWhiteSpace(summary.AnalysisId) ? "--" : summary.AnalysisId;
            TxtSummaryReference.Text = string.IsNullOrWhiteSpace(summary.ReferenceFileName) ? "参考音频信息缺失" : summary.ReferenceFileName;
            TxtSummaryUserBpm.Text = FormatValue(summary.PerformanceTempoBpm, "0.0");
            TxtSummaryReferenceBpm.Text = FormatValue(summary.ReferenceTempoBpm, "0.0");

            TxtOverallScore.Text = overallScore >= 0 ? overallScore.ToString(CultureInfo.InvariantCulture) : "--";
            TxtEvaluationBadge.Text = string.IsNullOrWhiteSpace(summary.Badge) ? "等待分析" : summary.Badge;
            TxtPitchScoreLabel.Text = string.Format("音高准确度 {0}", FormatPercent(summary.PitchScore));
            TxtRhythmScoreLabel.Text = string.Format("节奏准确度 {0}", FormatPercent(summary.RhythmScore));
            TxtCoverageLabel.Text = string.Format("覆盖率 {0}", FormatPercent(summary.Coverage));
            TxtConsistencyLabel.Text = string.Format("一致性 {0}", FormatPercent(summary.Consistency));
            TxtMeanDeviationLabel.Text = string.Format(
                "平均偏差 {0}",
                summary.MeanPitchDeviationCents.HasValue
                    ? string.Format(CultureInfo.InvariantCulture, "{0:0.0} cents", summary.MeanPitchDeviationCents.Value)
                    : "--");

            PitchScoreProgress.Value = ClampDisplay(summary.PitchScore);
            RhythmScoreProgress.Value = ClampDisplay(summary.RhythmScore);
            CoverageProgress.Value = ClampDisplay(summary.Coverage);
            ConsistencyProgress.Value = ClampDisplay(summary.Consistency);

            TxtPitchMetricScore.Text = FormatPercent(summary.PitchScore);
            TxtPitchMetricDeviation.Text = summary.MeanPitchDeviationCents.HasValue
                ? string.Format(CultureInfo.InvariantCulture, "{0:0.0}", summary.MeanPitchDeviationCents.Value)
                : "--";
            TxtPitchMetricHit25.Text = pitchAnalysis.HitRate25.HasValue
                ? string.Format(CultureInfo.InvariantCulture, "{0:0.0}%", pitchAnalysis.HitRate25.Value)
                : "--";
            TxtPitchMetricHit50.Text = pitchAnalysis.HitRate50.HasValue
                ? string.Format(CultureInfo.InvariantCulture, "{0:0.0}%", pitchAnalysis.HitRate50.Value)
                : "--";

            TxtCorrectionSuggestion.Text = BuildSuggestion(report);
            TxtDetectedKey.Text = BuildDetectedKeyLabel(report.TransposeBase);
            TxtTransposeBaseSummary.Text = report.TransposeBase != null && !string.IsNullOrWhiteSpace(report.TransposeBase.Summary)
                ? report.TransposeBase.Summary
                : "当前还没有可用的调性识别结果。";
            TxtTransposeSuggestionTitle.Text = "先完成一次评估";
            TxtTransposeSuggestionSummary.Text = "系统会自动识别当前调性，并生成适合目标声线的变调建议。";
            RenderTips(TransposeTipsPanel, new string[0], "#667085");

            DrawPitchChart(pitchAnalysis);
            RenderRhythmSegments(rhythmAnalysis.Segments, rhythmAnalysis.ThresholdMs, rhythmAnalysis.SeverityCounts);
            UpdateExportAvailability();
        }

        private void ApplyTransposeSuggestion(TransposeSuggestionResponse suggestion)
        {
            if (suggestion == null)
            {
                return;
            }

            TxtTransposeSuggestionTitle.Text = string.IsNullOrWhiteSpace(suggestion.Title)
                ? "推荐变调建议"
                : suggestion.Title;

            var summary = suggestion.Summary;
            if (!string.IsNullOrWhiteSpace(suggestion.RecommendedKey) && suggestion.RecommendedSemitone.HasValue)
            {
                summary = string.Format(
                    "{0}\n推荐结果：{1:+#;-#;0} 半音 -> {2}",
                    summary,
                    suggestion.RecommendedSemitone.Value,
                    suggestion.RecommendedKey);
            }

            TxtTransposeSuggestionSummary.Text = summary;
            RenderTips(TransposeTipsPanel, suggestion.Tips ?? new List<string>(), "#667085");
        }

        private void DrawPitchChart(PitchAnalysis analysis)
        {
            PitchChartCanvas.Children.Clear();
            var width = PitchChartCanvas.Width > 0 ? PitchChartCanvas.Width : 1300;
            var height = PitchChartCanvas.Height > 0 ? PitchChartCanvas.Height : 330;

            DrawChartFrame(width, height);

            if (analysis == null
                || analysis.ReferencePoints == null
                || analysis.ReferencePoints.Count == 0
                || analysis.PerformancePoints == null
                || analysis.PerformancePoints.Count == 0)
            {
                TxtPitchChartEmpty.Visibility = Visibility.Visible;
                return;
            }

            TxtPitchChartEmpty.Visibility = Visibility.Collapsed;

            var pitchValues = new List<double>();
            pitchValues.AddRange(analysis.ReferencePoints.Select(point => point.Value));
            pitchValues.AddRange(analysis.PerformancePoints.Select(point => point.Value));

            var minPitch = pitchValues.Min();
            var maxPitch = pitchValues.Max();
            if (Math.Abs(maxPitch - minPitch) < 0.1)
            {
                maxPitch += 1.0;
                minPitch -= 1.0;
            }

            DrawSeries(
                analysis.ReferencePoints,
                width,
                40,
                height - 170,
                minPitch,
                maxPitch,
                ColorFromHex("#4F84A7"),
                3.0);

            DrawSeries(
                analysis.PerformancePoints,
                width,
                40,
                height - 170,
                minPitch,
                maxPitch,
                ColorFromHex("#F97316"),
                3.0);

            if (analysis.DeviationPoints != null && analysis.DeviationPoints.Count > 0)
            {
                var deviationMax = analysis.DeviationPoints.Max(point => Math.Abs(point.Value));
                if (deviationMax < 25)
                {
                    deviationMax = 25;
                }

                DrawDeviationSeries(
                    analysis.DeviationPoints,
                    width,
                    height - 118,
                    58,
                    deviationMax,
                    ColorFromHex("#82C6B0"));
            }
        }

        private void DrawChartFrame(double width, double height)
        {
            var border = new Rectangle
            {
                Width = width,
                Height = height - 14,
                RadiusX = 22,
                RadiusY = 22,
                Stroke = new SolidColorBrush(ColorFromHex("#E9EEF5")),
                StrokeThickness = 1.2,
                Fill = Brushes.White
            };
            PitchChartCanvas.Children.Add(border);

            for (var index = 1; index <= 4; index++)
            {
                var y = 40 + index * 46;
                var line = new Line
                {
                    X1 = 36,
                    X2 = width - 36,
                    Y1 = y,
                    Y2 = y,
                    Stroke = new SolidColorBrush(ColorFromHex("#EEF2F7")),
                    StrokeThickness = 1
                };
                PitchChartCanvas.Children.Add(line);
            }

            var deviationAxis = new Line
            {
                X1 = 36,
                X2 = width - 36,
                Y1 = height - 90,
                Y2 = height - 90,
                Stroke = new SolidColorBrush(ColorFromHex("#E9EEF5")),
                StrokeThickness = 1
            };
            PitchChartCanvas.Children.Add(deviationAxis);
        }

        private void DrawSeries(
            IList<PitchCurvePoint> points,
            double width,
            double top,
            double height,
            double minValue,
            double maxValue,
            Color color,
            double thickness)
        {
            if (points == null || points.Count == 0)
            {
                return;
            }

            var maxTime = points.Max(point => point.TimeSeconds);
            if (maxTime <= 0)
            {
                maxTime = 1;
            }

            var polyline = new Polyline
            {
                Stroke = new SolidColorBrush(color),
                StrokeThickness = thickness,
                StrokeLineJoin = PenLineJoin.Round
            };

            foreach (var point in points)
            {
                var x = 36 + point.TimeSeconds / maxTime * (width - 72);
                var ratio = (point.Value - minValue) / (maxValue - minValue);
                var y = top + (1 - ratio) * height;
                polyline.Points.Add(new Point(x, y));
            }

            PitchChartCanvas.Children.Add(polyline);
        }

        private void DrawDeviationSeries(
            IList<PitchCurvePoint> points,
            double width,
            double centerY,
            double amplitude,
            double maxAbsValue,
            Color color)
        {
            if (points == null || points.Count == 0)
            {
                return;
            }

            var maxTime = points.Max(point => point.TimeSeconds);
            if (maxTime <= 0)
            {
                maxTime = 1;
            }

            var polyline = new Polyline
            {
                Stroke = new SolidColorBrush(color),
                StrokeThickness = 2.4,
                StrokeLineJoin = PenLineJoin.Round
            };

            foreach (var point in points)
            {
                var x = 36 + point.TimeSeconds / maxTime * (width - 72);
                var normalized = point.Value / maxAbsValue;
                var y = centerY - normalized * amplitude;
                polyline.Points.Add(new Point(x, y));
            }

            PitchChartCanvas.Children.Add(polyline);
        }

        private void RenderRhythmSegments(
            IList<EvaluationSegment> segments,
            int thresholdMs,
            SeverityCount counts)
        {
            RhythmSegmentsPanel.Children.Clear();

            if (segments == null || segments.Count == 0)
            {
                TxtRhythmDetailHint.Text = "完成一次评估后，这里会展示后端返回的节奏误差分类与说明。";
                return;
            }

            TxtRhythmDetailHint.Text = counts != null
                ? string.Format(
                    "当前阈值 {0}ms，正常 {1} 段，提醒 {2} 段，重点修正 {3} 段。",
                    thresholdMs,
                    counts.Normal,
                    counts.Warning,
                    counts.Critical)
                : string.Format("当前阈值 {0}ms，以下为主要节奏偏差片段。", thresholdMs);

            foreach (var segment in segments.Take(12))
            {
                var border = new Border
                {
                    Margin = new Thickness(0, 0, 0, 12),
                    Padding = new Thickness(18, 16, 18, 16),
                    CornerRadius = new CornerRadius(18),
                    BorderThickness = new Thickness(1),
                    BorderBrush = new SolidColorBrush(ColorFromHex("#E9EEF5")),
                    Background = new SolidColorBrush(GetRhythmCardColor(segment.Severity))
                };

                var stack = new StackPanel();
                stack.Children.Add(new TextBlock
                {
                    Text = string.Format(
                        CultureInfo.InvariantCulture,
                        "{0:0.00}s - {1:0.00}s   |   {2}   |   偏差 {3}",
                        segment.StartMs / 1000.0,
                        segment.EndMs / 1000.0,
                        GetSeverityLabel(segment.Severity),
                        segment.DeviationValue.HasValue
                            ? string.Format(CultureInfo.InvariantCulture, "{0:0.0} ms", segment.DeviationValue.Value)
                            : "--"),
                    FontSize = 15,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(ColorFromHex("#344054"))
                });
                stack.Children.Add(new TextBlock
                {
                    Margin = new Thickness(0, 8, 0, 0),
                    Text = string.IsNullOrWhiteSpace(segment.NoteText) ? "后端未返回说明。" : segment.NoteText,
                    FontSize = 14,
                    Foreground = new SolidColorBrush(ColorFromHex("#667085")),
                    TextWrapping = TextWrapping.Wrap
                });

                border.Child = stack;
                RhythmSegmentsPanel.Children.Add(border);
            }
        }

        private void RenderTips(Panel container, IEnumerable<string> tips, string foregroundHex)
        {
            container.Children.Clear();
            foreach (var tip in tips)
            {
                container.Children.Add(new TextBlock
                {
                    Margin = new Thickness(0, 0, 0, 8),
                    Text = "• " + tip,
                    FontSize = 15,
                    Foreground = new SolidColorBrush(ColorFromHex(foregroundHex)),
                    TextWrapping = TextWrapping.Wrap
                });
            }
        }

        private void UpdateExportAvailability()
        {
            BtnExportPdf.IsEnabled = false;
            BtnExportPdf.ToolTip = "当前演示版前端未接入登录态，登录后可导出 PDF 报告。";
        }

        private int ParseRhythmThreshold()
        {
            int value;
            if (!int.TryParse(TxtRhythmThreshold.Text, out value))
            {
                value = 50;
            }

            value = Math.Max(20, Math.Min(200, value));
            TxtRhythmThreshold.Text = value.ToString(CultureInfo.InvariantCulture);
            return value;
        }

        private static string GetSelectedTag(ComboBox comboBox, string fallback)
        {
            var item = comboBox.SelectedItem as ComboBoxItem;
            if (item != null && item.Tag != null)
            {
                return item.Tag.ToString();
            }

            return fallback;
        }

        private static string FormatPercent(double? value)
        {
            return value.HasValue
                ? string.Format(CultureInfo.InvariantCulture, "{0:0.0}%", value.Value)
                : "--";
        }

        private static string FormatValue(double? value, string format)
        {
            return value.HasValue
                ? value.Value.ToString(format, CultureInfo.InvariantCulture)
                : "--";
        }

        private static int ClampDisplay(double? value)
        {
            if (!value.HasValue)
            {
                return 0;
            }

            var rounded = (int)Math.Round(value.Value);
            return Math.Max(0, Math.Min(100, rounded));
        }

        private static int ToDisplayScore(double? value)
        {
            return value.HasValue ? (int)Math.Round(value.Value) : -1;
        }

        private static string BuildSuggestion(EvaluationReportResponse report)
        {
            if (report.Suggestions != null && report.Suggestions.Count > 0)
            {
                return report.Suggestions[0].Content;
            }

            if (report.Warnings != null && report.Warnings.Count > 0)
            {
                return report.Warnings[0];
            }

            return report.Summary != null && !string.IsNullOrWhiteSpace(report.Summary.SummaryText)
                ? report.Summary.SummaryText
                : "可以优先查看音高对比和节奏偏差区，针对波动较大的片段做针对性练习。";
        }

        private static string BuildDetectedKeyLabel(TransposeBase transposeBase)
        {
            if (transposeBase == null || string.IsNullOrWhiteSpace(transposeBase.DetectedKey) || transposeBase.DetectedKey == "--")
            {
                return "--";
            }

            if (string.IsNullOrWhiteSpace(transposeBase.DetectedMode) || transposeBase.DetectedMode == "--")
            {
                return transposeBase.DetectedKey;
            }

            return string.Format(
                "{0} {1}",
                transposeBase.DetectedKey,
                string.Equals(transposeBase.DetectedMode, "minor", StringComparison.OrdinalIgnoreCase) ? "minor" : "major");
        }

        private static string GetSeverityLabel(string severity)
        {
            if (string.Equals(severity, "critical", StringComparison.OrdinalIgnoreCase))
            {
                return "重点修正";
            }

            if (string.Equals(severity, "warning", StringComparison.OrdinalIgnoreCase))
            {
                return "轻微偏差";
            }

            return "基本稳定";
        }

        private static Color GetRhythmCardColor(string severity)
        {
            if (string.Equals(severity, "critical", StringComparison.OrdinalIgnoreCase))
            {
                return ColorFromHex("#FEF3F2");
            }

            if (string.Equals(severity, "warning", StringComparison.OrdinalIgnoreCase))
            {
                return ColorFromHex("#FFFAEB");
            }

            return ColorFromHex("#F8FAFC");
        }

        private static Color ColorFromHex(string hex)
        {
            return (Color)ColorConverter.ConvertFromString(hex);
        }

        private static string PickAudioFile(string title)
        {
            var dialog = new OpenFileDialog
            {
                Title = title,
                Filter = "音频 / 视频文件|*.wav;*.mp3;*.m4a;*.mp4;*.mov|WAV 音频|*.wav|所有文件|*.*"
            };

            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }
    }
}
