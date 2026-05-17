using System;
using System.IO;
using System.Text;
using backend.Services;
using Xunit;

namespace SeeMusic.Backend.Tests;

public class BeatAnalysisServiceTests
{
    private readonly BeatAnalysisService _service = new();

    [Fact]
    public void AnalyzeFile_ShouldDetectTempoFromClickTrack()
    {
        var filePath = CreateWaveFile(CreateClickTrack(120, 8.0, 44100));

        try
        {
            var result = _service.AnalyzeFile(filePath);

            Assert.True(result.IsAvailable);
            Assert.InRange(result.TempoBpm, 118.0, 122.0);
            Assert.InRange(result.Stability, 0.85, 1.0);
            Assert.InRange(result.Confidence, 0.45, 1.0);
            Assert.Equal("detected", result.GridSource);
            Assert.InRange(result.TimeSignatureNumerator, 2, 4);
            Assert.InRange(result.TimeSignatureConfidence, 0.0, 1.0);
            Assert.True(result.BeatTimes.Count >= 12);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void AnalyzeFile_ShouldRejectTooShortAudio()
    {
        var filePath = CreateWaveFile(CreateClickTrack(120, 1.0, 44100));

        try
        {
            var result = _service.AnalyzeFile(filePath);

            Assert.False(result.IsAvailable);
            Assert.Contains("时长过短", result.Summary);
        }
        finally
        {
            File.Delete(filePath);
        }
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
