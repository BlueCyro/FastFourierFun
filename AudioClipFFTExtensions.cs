using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Reflection;
using FrooxEngine;
using FrooxEngine.LogiX;
using BaseX;
using CodeX;
using NeosModLoader;
using HarmonyLib;
using CSCore;
using CSCore.DSP;

public static class AudioClipFFTExtensions
{
    private static int ReadAs(this AudioX data, in float[] samples, double Position, double rate, bool loop)
    {
        switch (data.Channels)
        {
            case ChannelConfiguration.Mono:
                return data.Read(samples.AsMonoBuffer(), Position, 1.0, false);
            case ChannelConfiguration.Stereo:
                return data.Read(samples.AsStereoBuffer(), Position, 1.0, false);
            case ChannelConfiguration.Quad:
                return data.Read(samples.AsQuadBuffer(), Position, 1.0, false);
            case ChannelConfiguration.Surround51: 
                return data.Read(samples.AsSurround51Buffer(), Position, 1.0, false);
            default:
                return -1;
        }
    }


    /// <param name="FFTSliceLength">The sample size of the FFT. Also will read the clip every <paramref name="FFTSliceLength"/> samples.</param>
    /// <param name="ReadMultiplier">Incrementing this beyond 1 will increase the frequency with which the clip is read. (1 = every FFTSliceLength samples. 2 = every FFTSliceLength / 2 samples, etc.)</param>
    /// <param name="SliceTo">If greater than zero, will slice the first X amount of samples from the resulting FFT. This helps when you want resolution, but don't need the entire result. E.g. For a visualizer.</param> 
    /// <summary>This method computes the FFT of the audioclip and returns the result as an animation.</summary>
    /// <returns>AnimX</returns>
    public static AnimX GetFFTAnimation(this AudioClip asset, Sync<string> progress, int FFTSliceLength = 2048, int ReadMultiplier = 1, int SliceTo = 0)
    {
        UniLog.Log("Getting FFT Animation");
        SliceTo = SliceTo == 0 || SliceTo >= FFTSliceLength ? FFTSliceLength : SliceTo;
        double Position = 0;
        int Read = 0;
        int Samples = asset.Data.SampleCount;
        int Channels = asset.Data.ChannelCount;
        int SampleRate = asset.Data.SampleRate;
        double Duration = asset.Data.Duration;
        World world = Engine.Current.WorldManager.FocusedWorld;
        // Given the information about the clip, the FFTSliceLength, and the ReadMultiplier, we can calulate the interval in milliseconds between each keyframe of the animation.
        float AnimationInterval = (float)(FFTSliceLength / (double)SampleRate) / (float)ReadMultiplier;
        UniLog.Log("Animation Interval: " + AnimationInterval + " or " + 1 / AnimationInterval + " times per second");

        // var fftProvider = new FftProvider(1, (FftSize)FFTSliceLength);
        var fftProvider = new FftProvider(Channels, (FftSize)FFTSliceLength);
        int ChunkSize = FFTSliceLength * Channels;

        float[] samples = new float[ChunkSize];
        float[] resultBuffer = new float[FFTSliceLength];

        AnimX anim = new AnimX((float)Duration, "FFT");
        List<RawFloatAnimationTrack> FFTFragments = new List<RawFloatAnimationTrack>();

        UniLog.Log("Samples: " + Samples);
        
        for(int i = 0; i < SliceTo; i++)
        {
            FFTFragments.Add(anim.AddTrack<RawFloatAnimationTrack>());
            FFTFragments[i].Interval = AnimationInterval;
            FFTFragments[i].Node = i.ToString();
            FFTFragments[i].Property = "Amplitude";
        }

        try
        {
            for(int i = 0; i < Samples; i++)
            {
                string numval = ((i / (float)Samples) * 100f).ToString("0.00");
                if (i % (Samples / 10000) == 0 && progress != null)
                {
                    world.RunSynchronously(() => {
                        progress.Value = String.Format("Importing... {0}%", numval);
                    });   
                }

                if (i % (Samples / 10) == 0)
                {
                    UniLog.Log(String.Format("Calculating FFT: {0}%", numval));
                }

                if (i % (FFTSliceLength / ReadMultiplier ) == 0)
                {
                    Read = asset.Data.ReadAs(samples, Position, 1, false);

                    Position += Read / ReadMultiplier;

                    /*
                    List<float[]> chans = new List<float[]>();
                    for (int j = 0; j < Channels; j++)
                    {
                        chans.Add(samples.Where((v,k) => k % Channels == j).ToArray());
                    }
                    */

                    fftProvider.Add(samples, Read);
                    fftProvider.GetFftData(resultBuffer);
                    /*
                    for (int j = 0; j < Channels; j++)
                    {
                        fftProvider.Add(chans[j], Read);
                        fftProvider.GetFftData(chans[j]);
                    }
                    */
                    float[] result = new float[SliceTo];

                    /*
                    // Average all results together
                    for (int j = 0; j < Channels; j++)
                    {
                        for (int k = 0; k < SliceTo; k++)
                        {
                            result[k] += chans[j][k];
                        }
                    }
                    */
                    for (int j = 0; j < SliceTo; j++)
                    {
                        var track = FFTFragments[j];
                        track.AppendFrame(Math.Abs(result[j]) / Channels);
                    }
                }
            }
        }
        // Catch and print stack trace
        catch (Exception e)
        {
            UniLog.Log(e.Message);
            UniLog.Log(e.StackTrace);
        }
        return anim;
    }
}