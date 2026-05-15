using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using backend.Models;
using backend.Services;
using Microsoft.Extensions.Options;
using Xunit;

namespace SeeMusic.Backend.Tests;

public class TranscriptionServicesTests
{
    [Fact]
    public void TranscriptionExecutionPlanner_ShouldPreferSync_WhenPreparedMediaIsShort()
    {
        var planner = new TranscriptionExecutionPlanner(new TranscriptionProcessingOptions
        {
            ImmediateProcessingMaxDurationSeconds = 45
        });

        var media = new MediaFile
        {
            PreparedAudioStatus = "ready",
            DurationMs = 12000
        };

        Assert.True(planner.ShouldProcessSynchronously(media));
    }

    [Fact]
    public void PianoTranscriptionService_ShouldGenerateMusicXmlAndDualTracks_FromMelodyWave()
    {
        var wavePath = CreateWaveFile(CreateMelody(new[] { 440.0, 493.88, 523.25, 587.33, 659.25, 698.46 }, 0.4, 44100));

        try
        {
            var service = new PianoTranscriptionService(
                new BeatAnalysisService(),
                Options.Create(new TranscriptionProcessingOptions()));

            var result = service.Transcribe(wavePath, "识谱测试", new TranscriptionOptionsRequest
            {
                Mode = "piano",
                SeparateMelody = true,
                SeparateAccompaniment = true,
                AnalyzeRhythm = true
            });

            Assert.Equal("succeeded", result.Status);
            Assert.NotEmpty(result.MusicXmlContent);
            Assert.Contains("<score-partwise", result.MusicXmlContent, StringComparison.Ordinal);
            Assert.Equal(2, result.Tracks.Count);
            Assert.True(result.Tracks[0].Notes.Count >= 4);
            Assert.True(result.Tracks[1].Notes.Count >= 4);
            Assert.True(result.MeasureCount >= 1);
            Assert.True(result.EstimatedPageCount >= 1);
            Assert.False(string.IsNullOrWhiteSpace(result.AnalysisSummary.MelodySummary));
        }
        finally
        {
            File.Delete(wavePath);
        }
    }

    [Fact]
    public void PianoTranscriptionService_ShouldFail_WhenAudioHasNoStablePitchContour()
    {
        var wavePath = CreateWaveFile(CreateSilence(3.0, 44100));

        try
        {
            var service = new PianoTranscriptionService(
                new BeatAnalysisService(),
                Options.Create(new TranscriptionProcessingOptions()));

            var result = service.Transcribe(wavePath, "无旋律测试", new TranscriptionOptionsRequest
            {
                Mode = "piano",
                SeparateMelody = true,
                SeparateAccompaniment = true,
                AnalyzeRhythm = true
            });

            Assert.Equal("failed", result.Status);
            Assert.Contains("旋律", result.ErrorMessage);
        }
        finally
        {
            File.Delete(wavePath);
        }
    }

    private static byte[] CreateMelody(IReadOnlyList<double> frequencies, double noteDurationSeconds, int sampleRate)
    {
        var samplesPerNote = (int)Math.Round(noteDurationSeconds * sampleRate);
        var totalSamples = samplesPerNote * frequencies.Count;
        var samples = new float[totalSamples];
        var cursor = 0;

        foreach (var frequency in frequencies)
        {
            for (var index = 0; index < samplesPerNote; index++)
            {
                var t = index / (double)sampleRate;
                var envelope = Math.Min(1.0, index / (sampleRate * 0.03));
                envelope *= Math.Min(1.0, (samplesPerNote - index) / (sampleRate * 0.03));
                samples[cursor + index] = (float)(Math.Sin(2.0 * Math.PI * frequency * t) * 0.7 * envelope);
            }

            cursor += samplesPerNote;
        }

        return BuildWave(samples, sampleRate);
    }

    private static byte[] CreateClickTrack(double bpm, double durationSeconds, int sampleRate)
    {
        var totalSamples = (int)Math.Round(durationSeconds * sampleRate);
        var samples = new float[totalSamples];
        var beatIntervalSamples = (int)Math.Round(sampleRate * 60.0 / bpm);

        for (var beatStart = 0; beatStart < totalSamples; beatStart += beatIntervalSamples)
        {
            var clickLength = Math.Min((int)(sampleRate * 0.03), totalSamples - beatStart);
            for (var index = 0; index < clickLength; index++)
            {
                var t = index / (double)sampleRate;
                var envelope = Math.Exp(-28.0 * t);
                samples[beatStart + index] += (float)(Math.Sin(2.0 * Math.PI * 1500.0 * t) * envelope * 0.9);
            }
        }

        return BuildWave(samples, sampleRate);
    }

    private static byte[] CreateSilence(double durationSeconds, int sampleRate)
    {
        var totalSamples = (int)Math.Round(durationSeconds * sampleRate);
        return BuildWave(new float[totalSamples], sampleRate);
    }

    private static byte[] BuildWave(float[] samples, int sampleRate)
    {
        using (var stream = new MemoryStream())
        using (var writer = new BinaryWriter(stream))
        {
            var bytesPerSample = 2;
            var dataSize = samples.Length * bytesPerSample;
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + dataSize);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
            writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)1);
            writer.Write(sampleRate);
            writer.Write(sampleRate * bytesPerSample);
            writer.Write((short)bytesPerSample);
            writer.Write((short)(bytesPerSample * 8));
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            writer.Write(dataSize);

            foreach (var sample in samples)
            {
                var clamped = Math.Max(-1.0f, Math.Min(1.0f, sample));
                writer.Write((short)Math.Round(clamped * short.MaxValue));
            }

            writer.Flush();
            return stream.ToArray();
        }
    }

    private static string CreateWaveFile(byte[] contents)
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".wav");
        File.WriteAllBytes(path, contents);
        return path;
    }
}
