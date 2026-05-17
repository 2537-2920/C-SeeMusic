using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using backend.Models;
using backend.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Xunit;

namespace SeeMusic.Backend.Tests;

public class InstantSingingEvaluationServiceTests
{
    [Fact]
    public async Task EvaluateAsync_ShouldReturnReport_ForMatchingWaveFiles()
    {
        var service = CreateInstantEvaluationService(new TemporaryAudioPreparationService());
        var performanceSamples = CreateMelody(new[] { 440.0, 493.88, 523.25, 587.33, 659.25 }, 0.45, 44100);
        var referenceSamples = CreateMelody(new[] { 440.0, 493.88, 523.25, 587.33, 659.25 }, 0.45, 44100);
        await using var performanceStream = new MemoryStream(CreateWaveBytes(performanceSamples));
        await using var referenceStream = new MemoryStream(CreateWaveBytes(referenceSamples));
        var performanceFile = new FormFile(performanceStream, 0, performanceStream.Length, "performanceFile", "performance.wav");
        var referenceFile = new FormFile(referenceStream, 0, referenceStream.Length, "referenceFile", "reference.wav");

        var report = await service.EvaluateAsync(
            performanceFile,
            referenceFile,
            new EvaluationOptionsRequest
            {
                UserAudioType = "clean_vocal",
                FeedbackLanguage = "zh-CN",
                ScoringModel = "balanced",
                RhythmThresholdMs = 50,
            });

        Assert.NotNull(report);
        Assert.NotNull(report.Summary);
        Assert.Equal("pitch_rhythm", report.Summary.ScoringProfile);
        Assert.True(report.Summary.TotalScore >= 35);
        Assert.Equal("reference.wav", report.Summary.ReferenceFileName);
        Assert.NotEmpty(report.PitchAnalysis.ReferencePoints);
        Assert.NotEmpty(report.RhythmAnalysis.Segments);
        Assert.False(string.IsNullOrWhiteSpace(report.TransposeBase.Summary));
    }

    [Fact]
    public async Task EvaluateAsync_ShouldDeleteWorkingDirectories_AfterCompletion()
    {
        var performancePrepared = CreatePreparedAudioResult(CreateMelody(new[] { 440.0, 493.88, 523.25, 587.33, 659.25 }, 0.45, 44100));
        var referencePrepared = CreatePreparedAudioResult(CreateMelody(new[] { 440.0, 493.88, 523.25, 587.33, 659.25 }, 0.45, 44100));
        var tempService = new StubTemporaryAudioPreparationService(performancePrepared, referencePrepared);
        var service = CreateInstantEvaluationService(tempService);
        await using var performanceStream = new MemoryStream(new byte[] { 1, 2, 3, 4 });
        await using var referenceStream = new MemoryStream(new byte[] { 1, 2, 3, 4 });
        var performanceFile = new FormFile(performanceStream, 0, performanceStream.Length, "performanceFile", "performance.wav");
        var referenceFile = new FormFile(referenceStream, 0, referenceStream.Length, "referenceFile", "reference.wav");

        await service.EvaluateAsync(
            performanceFile,
            referenceFile,
            new EvaluationOptionsRequest
            {
                UserAudioType = "clean_vocal",
                FeedbackLanguage = "zh-CN",
            });

        Assert.False(Directory.Exists(performancePrepared.WorkingDirectory));
        Assert.False(Directory.Exists(referencePrepared.WorkingDirectory));
    }

    [Fact]
    public void Build_ShouldReturnTransposeSuggestion_FromCurrentReportState()
    {
        var service = new InstantTransposeSuggestionService();

        var result = service.Build(new TransposeSuggestionRequest
        {
            SourceGender = "male",
            TargetGender = "female",
            FeedbackLanguage = "zh-CN",
            TransposeBase = new TransposeBaseDto
            {
                DetectedKey = "C",
                DetectedMode = "major",
                ReferenceMedianMidi = 57,
                Summary = "系统识别当前标准音频的调性为 C 大调。"
            }
        });

        Assert.NotNull(result);
        Assert.Equal("C", result.DetectedKey);
        Assert.NotNull(result.RecommendedSemitone);
        Assert.False(string.IsNullOrWhiteSpace(result.Summary));
        Assert.NotEmpty(result.Tips);
    }

    [Fact]
    public void Export_ShouldReturnPdfBytes_FromCurrentReport()
    {
        var service = new PdfExportService();

        var bytes = service.Export(new EvaluationReportResponse
        {
            Summary = new EvaluationSummaryDto
            {
                AnalysisId = "abc123",
                GeneratedAt = System.DateTime.UtcNow,
                Badge = "稳定出色",
                ScoringProfile = "pitch_rhythm",
                SummaryText = "测试报告"
            },
            PitchAnalysis = new PitchAnalysisDto(),
            RhythmAnalysis = new RhythmAnalysisDto(),
            TransposeBase = new TransposeBaseDto
            {
                DetectedKey = "C",
                DetectedMode = "major",
                Summary = "系统识别当前标准音频的调性为 C 大调。"
            },
            Suggestions = new List<EvaluationSuggestionDto>(),
            Warnings = new List<string>()
        });

        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 32);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
    }

    private static InstantSingingEvaluationService CreateInstantEvaluationService(ITemporaryAudioPreparationService tempService)
    {
        return new InstantSingingEvaluationService(
            tempService,
            new PitchAnalysisService(),
            new RhythmEvaluationService(new BeatAnalysisService()),
            new EvaluationScoringService(Options.Create(new EvaluationProcessingOptions())));
    }

    private static TemporaryPreparedAudioResult CreatePreparedAudioResult(float[] samples)
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), "seemusic-singing-test", System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workingDirectory);
        var audioPath = Path.Combine(workingDirectory, "prepared.wav");
        File.WriteAllBytes(audioPath, CreateWaveBytes(samples));
        return new TemporaryPreparedAudioResult
        {
            Status = "ready",
            AbsolutePath = audioPath,
            WorkingDirectory = workingDirectory,
        };
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
                var envelope = System.Math.Sin(System.Math.PI * System.Math.Min(1.0, globalPosition));
                var value = System.Math.Sin(2 * System.Math.PI * frequency * time) * envelope;
                samples[startSample + sampleIndex] += (float)(value * 0.85);
            }
        }

        return samples;
    }

    private static byte[] CreateWaveBytes(float[] samples)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);
        WriteWave(writer, samples, 44100);
        writer.Flush();
        return stream.ToArray();
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
            var clamped = System.Math.Clamp(sample, -1.0f, 1.0f);
            writer.Write((short)System.Math.Round(clamped * short.MaxValue));
        }
    }

    private sealed class StubTemporaryAudioPreparationService : ITemporaryAudioPreparationService
    {
        private readonly Queue<TemporaryPreparedAudioResult> _results;

        public StubTemporaryAudioPreparationService(params TemporaryPreparedAudioResult[] results)
        {
            _results = new Queue<TemporaryPreparedAudioResult>(results);
        }

        public Task<TemporaryPreparedAudioResult> PrepareAsync(IFormFile file, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_results.Dequeue());
        }
    }
}
