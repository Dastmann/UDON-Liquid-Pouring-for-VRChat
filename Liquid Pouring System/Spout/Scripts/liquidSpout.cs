/*
    liquidSpout U# Script by Dastmann
    Part of Dastmann's liquid pouring system for VRChat

    Configures the liquid spout with flowrate and color
    The values can be easily modified via other scripts

    This object can be simply toggled on or off with another script or with the included leverSync script
    
    This project's GitHub: https://github.com/Dastmann/UDON-Liquid-Pouring-for-VRChat
*/

using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class liquidSpout : UdonSharpBehaviour
{
    private ParticleSystem _particleSystem;
    [Range(0.00005f, 0.00015f), Tooltip("This is in (m^3)/s")] public float flowRate = 0.000071428f; // This seems to be a nice slow number. You can experiment and tweak it yourself
    public Color outColor = new Color(1, 0.7882353f, 0.05490196f, 1);
    private bool LOG = false;

    [Tooltip("How many frames should be stored in memory")] public int outflowArraySize = 50;
    private float[] liquidAmountsOut;
    private float[] timesOut;
    private Color[] colorsOut;
    private Vector2[] flowratesAndTimes = new Vector2[10];
    private int position;
    private ParticleSystem.MainModule main;
    private int outflowArrayLimit;


    void Start()
    {
        _particleSystem = GetComponent<ParticleSystem>();
        //var customData = _particleSystem.customData;
        //customData.enabled = true;

        liquidAmountsOut = new float[outflowArraySize];
        timesOut = new float[outflowArraySize];
        colorsOut = new Color[outflowArraySize];

        main = _particleSystem.main;
        outflowArrayLimit = outflowArraySize - 1;
        position = outflowArrayLimit;

        //Debug.Log(flowratesAndTimes);
    }
    void Update()
    {
        main.startSize = Mathf.Sqrt(flowRate / Mathf.PI);
        main.startColor = outColor;

        // Big brain time! Rolling arrays! (or however you want to call them)
        // It basically saves us from rewriting the entire array because we only write one value per update to each array. The script that's accessing this has to know the last value position tho.
        if (position == 0) position = outflowArrayLimit; else position -= 1;
        liquidAmountsOut[position] = flowRate * Time.deltaTime;
        timesOut[position] = Time.time;
        colorsOut[position] = outColor;

        if (LOG)
        {
            printArray_Float(liquidAmountsOut);
            printArray_Float(timesOut);
            printArray_Color(colorsOut);
        }
    }

    private void printArray_Float(float[] array)
    {
        string log = "";
        for (int i = 0; i < array.Length; i++)
        {
            log += array[i] + "  ";
        }
        Debug.Log(log);
    }

    private void printArray_Color(Color[] array)
    {
        string log = "";
        for (int i = 0; i < array.Length; i++)
        {
            log += array[i] + "  ";
        }
        Debug.Log(log);
    }
}
