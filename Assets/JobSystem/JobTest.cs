using System;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using Debug = UnityEngine.Debug;

public class JobTest : MonoBehaviour
{
    Stopwatch sw = new Stopwatch();

    void NoramlJob(Color[] pixels)
    {
        int count = 0;

        for(int i=0; i<pixels.Length; i++)
        {
            if(pixels[i].r < 0.6f && pixels[i].g < 0.6f)
            {
                count += 1;
            }
        }

        // Debug.Log($"Count_Normal : {count}");
    }

    void parallelJob(Color[] pixels)
    {
        int count = 0;

        Parallel.ForEach(pixels, pixel =>
        {
            if(pixel.r < 0.6f && pixel.g < 0.6f)
            {
                Interlocked.Increment(ref count);
            }
        });

        // if(res.IsCompleted == true)
        // {
        //     Debug.Log($"Count_Parallel : {count}");
        // }
    }

    void GetMethodTime(Action _event, string name)
    {
        sw.Reset();
        sw.Start();

        _event();

        sw.Stop();
        Debug.Log($"time_{name} : {sw.ElapsedMilliseconds}");

        if(min > sw.ElapsedMilliseconds)
        {
            min = sw.ElapsedMilliseconds;
            minJob = name;
        }
    }

    [BurstCompile(FloatPrecision.Standard, FloatMode.Default, CompileSynchronously = true)]
    public struct PixelCounterJob : IJobParallelFor
    {
        [ReadOnly]
        public NativeArray<Color> pixels;

        public NativeIntPtr.Parallel count;

        public void Execute(int i)
        {
            if (pixels[i].r < 0.6f && pixels[i].g < 0.6f)
            {
                count.Increment();
            }
        }
    }

    public void CountPixels(Color[] texture, int batch)
    {
        NativeArray<Color> pixels = new NativeArray<Color>(texture, Allocator.TempJob);

        NativeIntPtr sum = new NativeIntPtr(Allocator.TempJob);

        PixelCounterJob job = new PixelCounterJob()
        {
            pixels = pixels,
            count = sum.GetParallel()
        };

        JobHandle handle = job.Schedule(texture.Length, batch);

        handle.Complete();

        // Debug.Log($"Found {sum.Value} pixels");

        pixels.Dispose();
        sum.Dispose();
    }


    public Texture2D tempTex;

    Color[] pixels;
    long min = long.MaxValue;
    string minJob;
    void Start()
    {
        pixels = tempTex.GetPixels();
        GetMethodTime(() => NoramlJob(pixels), "Normal");
        // GetMethodTime(() => parallelJob(pixels), "Parallel");

        for(int i=2; i<=8192; i*=2)
        {
            GetMethodTime(() => CountPixels(pixels, i), $"DivideJob_{i}");
        }

        Debug.Log($"{minJob} : {min}");
    }
}