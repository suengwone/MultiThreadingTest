using System;
using System.Threading.Tasks;
using System.Diagnostics;
using UnityEngine;
using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using UnityEditor;
using Debug = UnityEngine.Debug;

[Serializable]
public struct ColorRGB
{
    [Range(0,1), SerializeField]
    public float r, g, b;
    public float[] rgb {get{return new float[]{r,g,b};}}
}

public class PixelCount : MonoBehaviour
{
    UnityEngine.UI.RawImage display;
    Texture2D[] inputTexture;
    RenderTexture outputTexture;
    public ComputeShader computeShader;
    private ComputeBuffer outputBuffer;
    int width, height, kernel, threadGroupsX, threadGroupsY;
    Color[] pixels;
    Stopwatch sw;
    public ColorRGB standard;
    public string path = "SO/";
    string tempPath;
    public int curIdx = 0;

    void Start()
    {
        inputTexture = Resources.LoadAll<Texture2D>(path);
        display = GetComponent<UnityEngine.UI.RawImage>();
        kernel = computeShader.FindKernel("CountPixel");
        sw = new Stopwatch();
        tempPath = path;
    }

    void Update()
    {
        if(tempPath != path)
        {
            Texture2D[] tempTex = Resources.LoadAll<Texture2D>(path);
            tempPath = path;
            if(tempTex.Length == 0)
            {
                return;
            }
            inputTexture = tempTex;
        }

        if(curIdx >= inputTexture.Length)
        {
            curIdx = 0;
        }
        else if(curIdx < 0)
        {
            curIdx = inputTexture.Length - 1;
        }

        if(inputTexture.Length == 0 || inputTexture[curIdx] == null)
        {
            return;
        }

        if(width != inputTexture[curIdx].width || height != inputTexture[curIdx].height || tempPath != path)
        {

            width = inputTexture[curIdx].width;
            height = inputTexture[curIdx].height;
            threadGroupsX = Mathf.CeilToInt(width / 8f);
            threadGroupsY = Mathf.CeilToInt(height / 8f);
            outputTexture = new RenderTexture(width, height, 24);
            outputTexture.enableRandomWrite = true;
            outputTexture.Create();

            display.texture = inputTexture[curIdx];
        }

        if(Input.GetKeyDown(KeyCode.Space))
        {
            pixels = inputTexture[curIdx].GetPixels();
            GetMethodTime(() => noramlTask(pixels), "Normal");
            GetMethodTime(() => Parallel.Invoke(() => noramlTask(pixels)), "NormalParallel");
            GetMethodTime(() => parallelGPU(true, inputTexture[curIdx]), "GPU");
            // GetMethodTime(() => parallelCPU(pixels, 16), "CPU");
        }
        if(Input.GetKeyDown(KeyCode.A))
        {
            pixels = inputTexture[curIdx].GetPixels();
            GetMethodTime(() => noramlTask(pixels), "Normal");
        }
        if(Input.GetKeyDown(KeyCode.S))
        {
            pixels = inputTexture[curIdx].GetPixels();
            GetMethodTime(() => Parallel.Invoke(() => noramlTask(pixels)), "NormalParallel");
        }
        if(Input.GetKeyDown(KeyCode.D))
        {
            GetMethodTime(() => parallelGPU(true, inputTexture[curIdx]), "GPU");
        }
        if(Input.GetKeyDown(KeyCode.F))
        {
            pixels = inputTexture[curIdx].GetPixels();
            GetMethodTime(() => parallelCPU(pixels, 128), "CPU");
        }

        parallelGPU(false, inputTexture[curIdx]);

        // if(Input.GetKeyDown(KeyCode.Space))
        // {
        //     pixels = inputTexture.GetPixels();
        //     GetMethodTime(() => noramlTask(pixels), "Normal");
        //     GetMethodTime(() => Parallel.Invoke(() => noramlTask(pixels)), "NormalParallel");
        //     GetMethodTime(() => parallelGPU(true), "GPU");
        //     GetMethodTime(() => parallelCPU(pixels, 16), "CPU");
        // }    
    }

    void parallelGPU(bool printTime, Texture2D inputTex)
    {
        outputBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Structured);
        outputBuffer.SetData(new int[1]{0});

        // Bind the input texture and output buffer to the compute shader
        computeShader.SetTexture(kernel, "inputTexture", inputTex);
        computeShader.SetTexture(kernel, "outputTexture", outputTexture);
        computeShader.SetBuffer(kernel, "outputBuffer", outputBuffer);
        computeShader.SetFloats("rgb_std", standard.rgb);

        // Dispatch the compute shader with the appropriate number of thread groups
        computeShader.Dispatch(kernel, threadGroupsX, threadGroupsY, 1);

        if(printTime == true)
        {
            int[] arr = new int[1];
            outputBuffer.GetData(arr);
            Debug.Log($"GPU_Parallel : {arr[0]}px");
        }

        outputBuffer.Release();
        display.texture = outputTexture;
    }

    void noramlTask(Color[] pixels)
    {
        int count = 0;

        for(int i=0; i<pixels.Length; i++)
        {
            if(pixels[i] != Color.black && pixels[i].r <= standard.r && pixels[i].g <= standard.g && pixels[i].b <= standard.b)
            {
                count += 1;
            }
        }

        Debug.Log($"Normal : {count}px");
    }

    [BurstCompile(FloatPrecision.Standard, FloatMode.Default, CompileSynchronously = true)]
    public struct PixelCounterJob : IJobParallelFor
    {
        [ReadOnly]
        public NativeArray<Color> pixels;

        public NativeIntPtr.Parallel count;

        [ReadOnly]
        public float rStd;
        public float gStd;
        public float bStd;

        public void Execute(int i)
        {
            if (pixels[i] != Color.black && pixels[i].r <= rStd && pixels[i].g <= gStd && pixels[i].b <= bStd)
            {
                count.Increment();
            }
        }
    }

    public void parallelCPU(Color[] texture, int batch)
    {
        NativeArray<Color> pixels = new NativeArray<Color>(texture.Length, Allocator.TempJob);
        pixels.CopyFrom(texture);

        NativeIntPtr sum = new NativeIntPtr(Allocator.TempJob);

        PixelCounterJob job = new PixelCounterJob()
        {
            pixels = pixels,
            count = sum.GetParallel(),
            rStd = standard.r,
            gStd = standard.g,
            bStd = standard.b,
        };

        JobHandle handle = job.Schedule(texture.Length, batch);

        handle.Complete();

        Debug.Log($"CPU_Parallel : {sum.Value}px");

        pixels.Dispose();
        sum.Dispose();
    }

    void GetMethodTime(Action _event, string name)
    {
        sw.Reset();
        sw.Start();

        _event();

        sw.Stop();
        Debug.Log($"{name} : {sw.ElapsedMilliseconds}ms");
    }

    void OnGUI()
    {
        GUIStyle fontSize = new GUIStyle(GUI.skin.GetStyle("label"));
        fontSize.fontSize = 20;

        GUI.contentColor = Color.white;
        standard.r = GUI.HorizontalSlider(new Rect(200, 20, 100, 30), standard.r, 0f, +1.0f);
        standard.g = GUI.HorizontalSlider(new Rect(200, 55, 100, 30), standard.g, 0f, +1.0f);
        standard.b = GUI.HorizontalSlider(new Rect(200, 90, 100, 30), standard.b, 0f, +1.0f);
        curIdx = Mathf.RoundToInt(GUI.HorizontalSlider(new Rect(200, 125, 200, 30), curIdx, 0, inputTexture.Length));
        path = GUI.TextArea(new Rect(100, 150, 100, 30), path, fontSize);


        GUI.Label(new Rect(25, 10, 200, 30), "R Standard : " + standard.r.ToString("f2"), fontSize);
        GUI.Label(new Rect(25, 45, 200, 30), "G Standard : " + standard.g.ToString("f2"), fontSize);
        GUI.Label(new Rect(25, 80, 200, 30), "B Standard : " + standard.b.ToString("f2"), fontSize);
        GUI.Label(new Rect(25, 115, 200, 30), "Image Index : " + curIdx.ToString(), fontSize);
        GUI.Label(new Rect(25, 150, 200, 30), "Path : ", fontSize);
    }
}
