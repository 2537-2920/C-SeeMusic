using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using backend.Models;
using backend.Services;
using Microsoft.Extensions.Options;
using Xunit;

namespace SeeMusic.Backend.Tests;

public class EvaluationServicesTests
{
    [Fact]
    public void AnonymousAccessTokenService_ShouldValidateMatchingToken()
    {
        var service = new AnonymousEvaluationAccessTokenService();

        var token = service.GenerateToken();
        var hash = service.HashToken(token);

        Assert.True(service.ValidateToken(hash, token));
        Assert.False(service.ValidateToken(hash, token + "x"));
    }

    [Fact]
    public void EvaluationExecutionPlanner_ShouldPreferAsync_WhenReferenceIsNotReady()
    {
        var planner = new EvaluationExecutionPlanner(new EvaluationProcessingOptions
        {
            ImmediateProcessingMaxDurationSeconds = 45
        });

        var performanceMedia = new MediaFile
        {
            PreparedAudioStatus = "ready",
            DurationMs = 12000
        };
        var referenceMedia = new MediaFile
        {
            PreparedAudioStatus = "pending",
            DurationMs = null
        };

        var result = planner.ShouldProcessSynchronously(
            performanceMedia,
            referenceMedia,
            new EvaluationOptionsRequest
            {
                AnalyzePitch = true,
                AnalyzeRhythm = true
            });

        Assert.False(result);
    }

    [Fact]
    public void EvaluationScoringService_ShouldFallbackToRhythmOnly_WhenPitchSkipped()
    {
        var service = new EvaluationScoringService(Options.Create(new EvaluationProcessingOptions
        {
            PitchWeight = 0.6,
            RhythmWeight = 0.4
        }));

        var result = service.Score(
            new PitchAnalysisResult
            {
                Status = "skipped",
                Summary = "skip",
                Warnings = new List<string> { "missing reference" }
            },
            new RhythmEvaluationResult
            {
                Status = "succeeded",
                Score = 78.5,
                Summary = "rhythm ok"
            },
            new EvaluationOptionsRequest
            {
                ScoringModel = "balanced",
                FeedbackLanguage = "zh-CN"
            });

        Assert.Equal("succeeded", result.Status);
        Assert.Equal("rhythm_only", result.ScoringProfile);
        Assert.Equal(78.5, result.TotalScore);
        Assert.Contains(result.Suggestions, suggestion => suggestion.SuggestionType == "pitch_setup");
    }

    [Fact]
    public void EvaluationScoringService_ShouldCombinePitchAndRhythmWeights()
    {
        var service = new EvaluationScoringService(Options.Create(new EvaluationProcessingOptions
        {
            PitchWeight = 0.6,
            RhythmWeight = 0.4
        }));

        var result = service.Score(
            new PitchAnalysisResult
            {
                Status = "succeeded",
                Score = 90,
                Summary = "pitch ok"
            },
            new RhythmEvaluationResult
            {
                Status = "succeeded",
                Score = 80,
                Summary = "rhythm ok"
            },
            new EvaluationOptionsRequest
            {
                ScoringModel = "balanced",
                FeedbackLanguage = "zh-CN"
            });

        Assert.Equal("pitch_rhythm", result.ScoringProfile);
        Assert.Equal(85.5, result.TotalScore);
        Assert.Equal("稳定出色", result.Badge);
    }

    [Fact]
    public void EvaluationScoringService_ShouldUsePitchFocusedWeights_AndEnglishSuggestions()
    {
        var service = new EvaluationScoringService(Options.Create(new EvaluationProcessingOptions()));

        var result = service.Score(
            new PitchAnalysisResult
            {
                Status = "succeeded",
                Score = 92,
                Summary = "pitch ok"
            },
            new RhythmEvaluationResult
            {
                Status = "succeeded",
                Score = 60,
                Summary = "rhythm needs work"
            },
            new EvaluationOptionsRequest
            {
                ScoringModel = "pitch_focus",
                FeedbackLanguage = "en-US"
            });

        Assert.Equal(84.0, result.TotalScore);
        Assert.Equal("Keep Building", result.Badge);
        Assert.Contains(result.Suggestions, suggestion => suggestion.Title.Contains("Slow", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PitchAnalysisService_ShouldScoreMatchingReferenceMelody()
    {
        var performancePath = CreateWaveFile(CreateMelody(new[] { 440.0, 493.88, 523.25, 587.33, 659.25 }, 0.45, 44100));
        var referencePath = CreateWaveFile(CreateMelody(new[] { 440.0, 493.88, 523.25, 587.33, 659.25 }, 0.45, 44100));

        try
        {
            var service = new PitchAnalysisService();
            var result = service.Analyze(
                performancePath,
                referencePath,
                new EvaluationOptionsRequest
                {
                    UserAudioType = "clean_vocal",
                    FeedbackLanguage = "zh-CN"
                });

            Assert.Equal("succeeded", result.Status);
            Assert.NotNull(result.Score);
            Assert.True(result.Score >= 35, "Pitch score was " + result.Score);
            Assert.NotNull(result.MeanDeviationCents);
            Assert.True(result.MeanDeviationCents <= 120, "Pitch mean deviation was " + result.MeanDeviationCents);
            Assert.NotEmpty(result.PerformancePoints);
            Assert.NotEmpty(result.ReferencePoints);
            Assert.NotNull(result.DetectedKey);
        }
        finally
        {
            File.Delete(performancePath);
            File.Delete(referencePath);
        }
    }

    [Fact]
    public void PitchAnalysisService_ShouldSkip_WhenReferenceHasNoStableMelody()
    {
        var performancePath = CreateWaveFile(CreateMelody(new[] { 440.0, 493.88, 523.25, 587.33, 659.25 }, 0.45, 44100));
        var referencePath = CreateWaveFile(CreateClickTrack(120, 3.0, 44100));

        try
        {
            var service = new PitchAnalysisService();
            var result = service.Analyze(
                performancePath,
                referencePath,
                new EvaluationOptionsRequest
                {
                    UserAudioType = "with_accompaniment",
                    FeedbackLanguage = "zh-CN"
                });

            Assert.Equal("skipped", result.Status);
            Assert.Contains(result.Warnings, warning => warning.Contains("主旋律"));
        }
        finally
        {
            File.Delete(performancePath);
            File.Delete(referencePath);
        }
    }

    [Fact]
    public void RhythmEvaluationService_ShouldScoreAlignedReferenceClickTrack()
    {
        var performancePath = CreateWaveFile(CreateClickTrack(120, 8.0, 44100));
        var referencePath = CreateWaveFile(CreateClickTrack(120, 8.0, 44100));

        try
        {
            var service = new RhythmEvaluationService(new BeatAnalysisService());
            var result = service.Analyze(
                performancePath,
                referencePath,
                new EvaluationOptionsRequest
                {
                    RhythmThresholdMs = 50,
                    FeedbackLanguage = "zh-CN"
                });

            Assert.Equal("succeeded", result.Status);
            Assert.NotNull(result.Score);
            Assert.True(result.Score >= 75);
            Assert.NotEmpty(result.Segments);
        }
        finally
        {
            File.Delete(performancePath);
            File.Delete(referencePath);
        }
    }

    [Fact]
    public void RhythmEvaluationService_ShouldRespectThresholdOption()
    {
        var referencePath = CreateWaveFile(CreateClickTrack(120, 8.0, 44100));
        var performancePath = CreateWaveFile(CreateJitteredClickTrack(120, 8.0, 44100, 0.045));

        try
        {
            var service = new RhythmEvaluationService(new BeatAnalysisService());
            var strictResult = service.Analyze(
                performancePath,
                referencePath,
                new EvaluationOptionsRequest
                {
                    RhythmThresholdMs = 30,
                    FeedbackLanguage = "zh-CN"
                });
            var looseResult = service.Analyze(
                performancePath,
                referencePath,
                new EvaluationOptionsRequest
                {
                    RhythmThresholdMs = 100,
                    FeedbackLanguage = "zh-CN"
                });

            Assert.True(strictResult.Score <= looseResult.Score);
            Assert.True(strictResult.SeverityCounts.Warning + strictResult.SeverityCounts.Critical
                        >= looseResult.SeverityCounts.Warning + looseResult.SeverityCounts.Critical);
        }
        finally
        {
            File.Delete(referencePath);
            File.Delete(performancePath);
        }
    }

    private static float[] CreateMelody(IReadOnlyList<double> frequencies, double noteDurationSeconds, int sampleRate)
    {
        var totalSamples = (int)(frequencies.Count * noteDurationSeconds * sampleRate);
        var samples = new float[totalSamples];

        for (var noteIndex = 0; noteIndex < frequencies.Count; noteIndex++)
        {
            var frequency = frequencies[noteIndex];
            var startSample = (int)(noteIndex * noteDurationSeconds * sampleRate);
            var noteSamples = (int)(noteDurationSeconds * sampleRate);

            for (var sampleIndex = 0; sampleIndex < noteSamples && startSample + sampleIndex < samples.Length; sampleIndex++)
            {
                var time = sampleIndex / (double)sampleRate;
                var globalPosition = sampleIndex / (double)noteSamples;
                var envelope = Math.Sin(Math.PI * Math.Min(1.0, globalPosition));
                var value = Math.Sin(2 * Math.PI * frequency * time) * envelope;
                samples[startSample + sampleIndex] += (float)(value * 0.85);
            }
        }

        return samples;
    }

    private static float[] CreateClickTrack(int bpm, double durationSeconds, int sampleRate)
    {
        var totalSamples = (int)(durationSeconds * sampleRate);
        var samples = new float[totalSamples];
        var intervalSeconds = 60.0 / bpm;
        var clickLength = (int)(sampleRate * 0.03);
        var clickFrequency = 1800.0;

        for (var beatTime = 0.0; beatTime < durationSeconds; beatTime += intervalSeconds)
        {
            var start = (int)(beatTime * sampleRate);
            for (var index = 0; index < clickLength && start + index < samples.Length; index++)
            {
                var time = index / (double)sampleRate;
                var envelope = Math.Exp(-35 * time);
                var sample = Math.Sin(2 * Math.PI * clickFrequency * time) * envelope;
                samples[start + index] += (float)(sample * 0.95);
            }
        }

        return samples;
    }

    private static float[] CreateJitteredClickTrack(int bpm, double durationSeconds, int sampleRate, double jitterSeconds)
    {
        var totalSamples = (int)(durationSeconds * sampleRate);
        var samples = new float[totalSamples];
        var intervalSeconds = 60.0 / bpm;
        var clickLength = (int)(sampleRate * 0.03);
        var clickFrequency = 1800.0;

        var beatIndex = 0;
        for (var beatTime = 0.0; beatTime < durationSeconds; beatTime += intervalSeconds)
        {
            var adjustedTime = beatTime + (beatIndex % 2 == 0 ? jitterSeconds : -jitterSeconds);
            if (adjustedTime < 0)
            {
                adjustedTime = 0;
            }

            var start = (int)(adjustedTime * sampleRate);
            for (var index = 0; index < clickLength && start + index < samples.Length; index++)
            {
                var time = index / (double)sampleRate;
                var envelope = Math.Exp(-35 * time);
                var sample = Math.Sin(2 * Math.PI * clickFrequency * time) * envelope;
                samples[start + index] += (float)(sample * 0.95);
            }

            beatIndex++;
        }

        return samples;
    }

    private static string CreateWaveFile(float[] samples)
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.wav");
        using var stream = File.Create(filePath);
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: false);
        WriteWave(writer, samples, 44100);
        return filePath;
    }

    private static void WriteWave(BinaryWriter writer, float[] samples, int sampleRate)
    {
        const short channelCount = 1;
        const short bitsPerSample = 16;
        var blockAlign = (short)(channelCount * bitsPerSample / 8);
        var byteRate = sampleRate * blockAlign;
        var dataSize = samples.Length * blockAlign;

        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + dataSize);
        writer.Write(Encoding.ASCII.GetBytes("WAVE"));
        writer.Write(Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16);
        writer.Write((short)1);
        writer.Write(channelCount);
        writer.Write(sampleRate);
        writer.Write(byteRate);
        writer.Write(blockAlign);
        writer.Write(bitsPerSample);
        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(dataSize);

        foreach (var sample in samples)
        {
            var clamped = Math.Clamp(sample, -1.0f, 1.0f);
            writer.Write((short)Math.Round(clamped * short.MaxValue));
        }
    }
}
