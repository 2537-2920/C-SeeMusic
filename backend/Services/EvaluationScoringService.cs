using backend.Models;
using Microsoft.Extensions.Options;

namespace backend.Services;

public sealed class EvaluationScoringService : IEvaluationScoringService
{
    public EvaluationScoringService(IOptions<EvaluationProcessingOptions> options)
    {
        _ = options.Value;
    }

    public EvaluationAggregateResult Score(
        PitchAnalysisResult pitchResult,
        RhythmEvaluationResult rhythmResult,
        EvaluationOptionsRequest options)
    {
        var normalizedOptions = (options ?? new EvaluationOptionsRequest()).Normalize();
        var isEnglish = string.Equals(normalizedOptions.FeedbackLanguage, "en-US", StringComparison.OrdinalIgnoreCase);
        var warnings = new List<string>();
        warnings.AddRange(pitchResult.Warnings);
        warnings.AddRange(rhythmResult.Warnings);

        var pitchSucceeded = string.Equals(pitchResult.Status, "succeeded", StringComparison.OrdinalIgnoreCase)
            && pitchResult.Score.HasValue;
        var rhythmSucceeded = string.Equals(rhythmResult.Status, "succeeded", StringComparison.OrdinalIgnoreCase)
            && rhythmResult.Score.HasValue;

        if (!pitchSucceeded && !rhythmSucceeded)
        {
            return new EvaluationAggregateResult
            {
                Status = "failed",
                ScoringProfile = "unavailable",
                PitchStatus = pitchResult.Status,
                RhythmStatus = rhythmResult.Status,
                ErrorMessage = isEnglish
                    ? "Neither pitch nor rhythm results were available, so no report could be generated."
                    : "音准和节奏结果都不可用，无法生成评估报告。",
                SummaryText = CombineSummary(pitchResult.Summary, rhythmResult.Summary, isEnglish),
                Warnings = warnings,
                Suggestions = BuildSuggestions(pitchResult, rhythmResult, null, warnings, isEnglish),
            };
        }

        double totalScore;
        string profile;
        if (pitchSucceeded && rhythmSucceeded)
        {
            var (pitchWeight, rhythmWeight) = ResolveWeights(normalizedOptions.ScoringModel);
            totalScore = pitchResult.Score!.Value * pitchWeight
                + rhythmResult.Score!.Value * rhythmWeight;
            profile = "pitch_rhythm";
        }
        else if (pitchSucceeded)
        {
            totalScore = pitchResult.Score!.Value;
            profile = "pitch_only";
        }
        else
        {
            totalScore = rhythmResult.Score!.Value;
            profile = "rhythm_only";
        }

        totalScore = Math.Round(totalScore, 1);

        return new EvaluationAggregateResult
        {
            Status = "succeeded",
            ScoringProfile = profile,
            PitchStatus = pitchResult.Status,
            RhythmStatus = rhythmResult.Status,
            TotalScore = totalScore,
            Badge = GetBadge(totalScore, isEnglish),
            SummaryText = BuildCompositeSummary(pitchResult, rhythmResult, profile, isEnglish),
            Warnings = warnings,
            Suggestions = BuildSuggestions(pitchResult, rhythmResult, totalScore, warnings, isEnglish),
        };
    }

    private static (double PitchWeight, double RhythmWeight) ResolveWeights(string scoringModel)
    {
        return scoringModel switch
        {
            "pitch_focus" => (0.75, 0.25),
            "rhythm_focus" => (0.30, 0.70),
            _ => (0.55, 0.45),
        };
    }

    private static string BuildCompositeSummary(
        PitchAnalysisResult pitchResult,
        RhythmEvaluationResult rhythmResult,
        string profile,
        bool isEnglish)
    {
        if (profile == "pitch_rhythm")
        {
            return isEnglish
                ? $"Pitch and rhythm were both scored. {pitchResult.Summary} {rhythmResult.Summary}"
                : $"音准与节奏均已完成评分。{pitchResult.Summary} {rhythmResult.Summary}";
        }

        if (profile == "rhythm_only")
        {
            return isEnglish
                ? $"This report is rhythm-led. {rhythmResult.Summary}"
                : $"本次报告以节奏维度为主。{rhythmResult.Summary}";
        }

        return isEnglish
            ? $"This report is pitch-led. {pitchResult.Summary}"
            : $"本次报告以音准维度为主。{pitchResult.Summary}";
    }

    private static string CombineSummary(string left, string right, bool isEnglish)
    {
        if (string.IsNullOrWhiteSpace(left))
        {
            return string.IsNullOrWhiteSpace(right)
                ? (isEnglish ? "No analysis summary is available yet." : "当前没有可用的分析摘要。")
                : right;
        }

        if (string.IsNullOrWhiteSpace(right))
        {
            return left;
        }

        return $"{left} {right}";
    }

    private static string GetBadge(double totalScore, bool isEnglish)
    {
        if (totalScore >= 85)
        {
            return isEnglish ? "Strong and Stable" : "稳定出色";
        }

        if (totalScore >= 70)
        {
            return isEnglish ? "Keep Building" : "继续提升";
        }

        if (totalScore >= 55)
        {
            return isEnglish ? "Needs Refinement" : "需要打磨";
        }

        return isEnglish ? "Consider Re-recording" : "建议重录";
    }

    private static List<EvaluationSuggestionDto> BuildSuggestions(
        PitchAnalysisResult pitchResult,
        RhythmEvaluationResult rhythmResult,
        double? totalScore,
        IReadOnlyList<string> warnings,
        bool isEnglish)
    {
        var suggestions = new List<EvaluationSuggestionDto>();

        if (pitchResult.Status == "skipped")
        {
            suggestions.Add(new EvaluationSuggestionDto
            {
                SuggestionType = "pitch_setup",
                Title = isEnglish ? "Add a Reference Track" : "补充参考音频",
                Content = isEnglish
                    ? "To get a more trustworthy pitch score, upload a reference track with a clear melody and evaluate again."
                    : "想拿到更可信的音准准确率，建议补充带主旋律的参考音频再重新评估。"
            });
        }
        else if (pitchResult.Score is <= 70)
        {
            suggestions.Add(new EvaluationSuggestionDto
            {
                SuggestionType = "pitch_fix",
                Title = isEnglish ? "Practice Line by Line" : "拆句校正音高",
                Content = isEnglish
                    ? "Pull out the lines with the largest pitch drift, tune them in isolation, then stitch them back into full phrases."
                    : "先把偏差较大的句子单独拿出来练习，再回到整段连唱，通常会更容易稳定准度。"
            });
        }

        if (rhythmResult.Score is <= 70)
        {
            suggestions.Add(new EvaluationSuggestionDto
            {
                SuggestionType = "rhythm_fix",
                Title = isEnglish ? "Slow Metronome Drills" : "节拍器分拍练习",
                Content = isEnglish
                    ? "Slow the weak section down to 70%-80% speed and lock in the entrances and phrase endings beat by beat."
                    : "建议把问题段落降速到 70%-80%，按拍器逐拍对齐起拍和句尾收拍。"
            });
        }

        if (warnings.Count > 0)
        {
            suggestions.Add(new EvaluationSuggestionDto
            {
                SuggestionType = "recording",
                Title = isEnglish ? "Improve the Source Audio" : "优化录音素材",
                Content = isEnglish
                    ? "If the recording is noisy or the reference melody is unclear, try a cleaner WAV take and re-run the evaluation."
                    : "若素材噪声较多或参考旋律不够清晰，建议改用更干净的 WAV 录音重新评估。"
            });
        }

        if (suggestions.Count == 0 && totalScore is >= 85)
        {
            suggestions.Add(new EvaluationSuggestionDto
            {
                SuggestionType = "maintain",
                Title = isEnglish ? "Maintain the Foundation" : "保持当前状态",
                Content = isEnglish
                    ? "The technical baseline is already stable, so you can start focusing more on expression and phrasing details."
                    : "当前整体完成度已经比较稳定，可以开始关注情感表达和细节处理。"
            });
        }

        return suggestions;
    }
}
