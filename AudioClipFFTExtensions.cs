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

    public static AnimX GetFFTAnimation(this AudioClip asset, int FFTSliceLength)
    {
        double Position = 0;
        int Read = 0;
        int Samples = asset.Data.SampleCount;
        int Channels = asset.Data.ChannelCount;
        int SampleRate = asset.Data.SampleRate;
        double Duration = asset.Data.Duration;
        var fftProvider = new FftProvider(1, (FftSize)FFTSliceLength);
        int ChunkSize = FFTSliceLength * 2;
        float[] samples = new float[ChunkSize];

        AnimX anim = new AnimX((float)Duration, "FFT");
        CurveFloatAnimationTrack[] FFTFragments = new CurveFloatAnimationTrack[FFTSliceLength];

        UniLog.Log("Samples: " + Samples);
        
        for(int i = 0; i < FFTSliceLength; i++)
        {
            FFTFragments[i] = anim.AddTrack<CurveFloatAnimationTrack>();
            FFTFragments[i].Node = i.ToString();
            FFTFragments[i].Property = "Amplitude";
        }

        for(int i = 0; i < Samples; i++)
        {

            if (i % (Samples / 10) == 0)
            {
                UniLog.Log("Progress: " + Math.Round((i / (float)Samples) * 100) + "%");
            }

            if (i % FFTSliceLength == 0)
            {
                Read = asset.Data.ReadAs(samples, Position, 1, false);

                Position += Read;

                List<float[]> chans = new List<float[]>();
                for (int j = 0; j < Channels; j++)
                {
                    chans.Add(samples.Where((v,k) => k % Channels == j).ToArray());
                }

                try 
                {
                    for (int j = 0; j < Channels; j++)
                    {
                        fftProvider.Add(chans[j], Read);
                        fftProvider.GetFftData(chans[j]);
                    }
                }
                catch (Exception e)
                {
                    UniLog.Log("FFT Error: " + e.Message);
                }
                
                float[] max = new float[FFTSliceLength];
                for (int j = 0; j < FFTSliceLength; j++)
                {
                    max[j] = chans.Select(c => c[j]).Max();
                }



                for(int j = 0; j < FFTSliceLength; j++)
                {
                    FFTFragments[j].InsertKeyFrame(Math.Abs(max[j]), i / (float)Samples * (float)Duration, KeyframeInterpolation.Linear);
                }
            }
        }
        return anim;
    }
}