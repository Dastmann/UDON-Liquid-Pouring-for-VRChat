/*
    WobbleAndVolume U# Script by Dastmann
    Part of Dastmann's liquid pouring system for VRChat

    This script adds volume consistence and surface wobble to the liquid shader

    
    Next step might be to do most of this in the shader itself. Maybe next version? We'll see ^^

    GitHub: https://github.com/Dastmann/UDON-Liquid-Pouring-for-VRChat

    Wobble part of code and current shader by Minions Art
        Check him out -> https://www.patreon.com/posts/quick-game-art-18245226
*/
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class Glass_WobbleAndVolume : UdonSharpBehaviour
{
    // Public variables, accessible in the inspector
    public Transform parentTransform;
    public MeshRenderer liquidMeshRenderer;
    public float fillPercentageStart = 0.5f;
    public float cylinderDiameter = 1; // In meters
    public float cylinderHeight = 2; // In meters
    public bool LOG = false; // Display Debug.Log() messages
    public bool WOBBLE = true;

    // Private variables
    private float offset; // Offset of shader Fill Amount
    private float cylinderArea;
    public float fillPercentageCurrent; //// ONLY TEMPORARILY PUBLIC ////
    private float lastFillPercentage = -1f; // This is so the we don't miss the first fill
    private float waterHeight;
    private float liquidLevelDistanceFromCenterDefault;
    private float angleOfSpillAdjusted;
    private float liquidArea;
    private float angleOfChangeAdjusted;
    private float distanceCoveredByWaterOnBase;
    private float distanceCoveredByAirOnTopBase;
    private float angleOfSpillTouchingBaseAdjusted;
    private bool computationOfLiquids;


    // Wobble variables
    public float MaxWobble = 0.03f;
    public float WobbleSpeed = 1f;
    public float Recovery = 1f;
    private Vector3 lastPos;
    private Vector3 lastRot;
    private float time;
    private float wobbleAmountToAddX;
    private float wobbleAmountToAddZ;

    void Start()
    {
        // Calculate constants and apply default values
        cylinderArea = cylinderDiameter * cylinderHeight;
        fillPercentageCurrent = fillPercentageStart;
    }

    void Update()
    {
        //// Check if there is any liquid
        if (computationOfLiquids)
        {
            //// Do some crazy stuff
            ComputeLiquids();
        }

        CheckForLiquid();
    }

    private void CheckForLiquid()
    {
        //// If there is some liquid, compute! Otherwise, save resources
        // This is deliberately after ComputeLiquids() to allow for an extra cycle with the value 0 and clean up
        if (fillPercentageCurrent != 0)
        {
            computationOfLiquids = true;
        }
        else
        {
            computationOfLiquids = false;
        }
    }

    private void ComputeLiquids()
    {
        //// Get the angle between objects up axis and world Y axis
        float angleFromYaxis = GetAngleFromYaxis();

        // Do some optimization
        if (fillPercentageCurrent == lastFillPercentage)
        {
            // The amount of liquid is the same. We can save on resources on not calculating all of the angles

            //// Save current state
            lastFillPercentage = fillPercentageCurrent;

        }
        else
        {
            // The amount of liquid has changed. We need to calculate the angles at which the cylinder starts spilling as well as
            // the angles at which the liquid changes height to conserve volume

            //// Save current state
            lastFillPercentage = fillPercentageCurrent;

            //// Scale percentage (0 - 1) to the actual dimensions in meters
            waterHeight = scale(0f, 1f, 0f, cylinderHeight, fillPercentageCurrent);
            if (LOG) Debug.Log(nameof(waterHeight) + " = " + waterHeight);

            liquidArea = waterHeight * cylinderDiameter;
            if (LOG) Debug.Log(nameof(liquidArea) + " = " + liquidArea);  // Log the value

            //// Calculate the necessary distances for trigonometry
            // This is with the cylinder laying on its side
            distanceCoveredByWaterOnBase = liquidArea / cylinderHeight;
            // This is the TOTAL distance ("air") not covered by water on both bases. It is used in case 4
            distanceCoveredByAirOnTopBase = 2 * (cylinderDiameter - distanceCoveredByWaterOnBase);
            if (LOG) Debug.Log(nameof(distanceCoveredByWaterOnBase) + " = " + distanceCoveredByWaterOnBase);  // Log the value
            if (LOG) Debug.Log(nameof(distanceCoveredByAirOnTopBase) + " = " + distanceCoveredByAirOnTopBase);  // Log the value

            //// Calculate the angles which are used as limits of different cases as well as checking for liquid spilling
            CalculateAngles();

            //// Calculate the distance from the center of rotation of the cylinder to the liquid surface. This is with the cylinder in its default orientation
            liquidLevelDistanceFromCenterDefault = scale(0f, 1f, -(cylinderHeight / 2), (cylinderHeight / 2), fillPercentageCurrent);
        }

        //// Get case number [1,2,-2,3]
        int caseNumber;
        caseNumber = GetCase(angleFromYaxis);

        //// Hande different cases
        offset = GetOffsetFromCase(angleFromYaxis, caseNumber);

        //// Get wobble wobble directions
        Vector3 wobbleDirections;
        wobbleDirections = GetWobbleDirections();

        if (LOG) Debug.Log(nameof(offset) + " = " + offset);


        MaterialPropertyBlock properties = new MaterialPropertyBlock();
        liquidMeshRenderer.GetPropertyBlock(properties);
        properties.SetFloat("_FillAmount", 0.5f + offset);
        properties.SetFloat("_WobbleX", wobbleDirections.x);
        properties.SetFloat("_WobbleZ", wobbleDirections.z);
        liquidMeshRenderer.SetPropertyBlock(properties);
    }

    private float GetAngleFromYaxis()
    {
        // Calculate angle from objects up axis to world up (Y) axis. This is used to determine the liquid level and the angles
        float angleFromYaxis = Mathf.Abs(Vector3.SignedAngle(new Vector3(0, 1, 0), parentTransform.up, parentTransform.forward));
        if (LOG) Debug.Log(nameof(angleFromYaxis) + " = " + angleFromYaxis);
        return angleFromYaxis;
    }

    private Vector3 GetWobbleDirections()
    {
        Vector3 wobbleDirections;
        // Make sure that when there is supposed to be no liquid, there is no liquid (and also disable the wobble, it does weird things)
        if (fillPercentageCurrent == 0)
        {
            offset += 0.01f;
            wobbleDirections = Vector3.zero;
        }
        else
        {
            wobbleDirections = (WOBBLE ? calculateWobble() : Vector3.zero);
        }

        return wobbleDirections;
    }

    private float GetOffsetFromCase(float angleFromYaxis, int caseNumber)
    {
        float offset;
        if (LOG) Debug.Log(nameof(caseNumber) + " = " + caseNumber);  // Log the value
        switch (caseNumber)
        {
            case 1:
                offset = Case1(angleFromYaxis);
                break;
            case 2:
                offset = Case2(angleFromYaxis, liquidArea);
                break;
            case -2:
                float airArea = cylinderArea - liquidArea;
                offset = -Case2(angleFromYaxis, airArea);
                break;
            case 3:
                offset = Case3(angleFromYaxis);
                break;
            default:
                offset = 0f;
                break;
        }

        return offset;
    }

    private int GetCase(float angleFromYaxis)
    {
        int caseNumber;
        // Check if liquid touches top or bottom lip first and calculate accordingly
        if (2 * waterHeight >= cylinderHeight)
        {
            //// Liquid touches the top lip first
            // Check if liquid touches top base and the edge of the cylinder
            if (angleFromYaxis > angleOfSpillAdjusted && angleFromYaxis < angleOfSpillAdjusted + (90f - angleOfSpillAdjusted) * 2)
            {
                // Check if liquid touches both bases at the same time
                if (angleFromYaxis > angleOfChangeAdjusted && angleFromYaxis < angleOfChangeAdjusted + (90f - angleOfChangeAdjusted) * 2)
                {
                    caseNumber = 3;

                }
                else
                {
                    caseNumber = -2;
                }
            }
            else
            {
                caseNumber = 1;
            }

        }
        else
        {
            //// Liquid touches the bottom lip first
            // Check if we are touching one base and the wall of the cylinder
            if (angleFromYaxis > angleOfChangeAdjusted && angleFromYaxis < angleOfChangeAdjusted + (90f - angleOfChangeAdjusted) * 2)
            {
                caseNumber = 2;

                // If water is touching both bases at the same time. This will only happen for a short time becouse the water will spill out
                if (angleFromYaxis > angleOfSpillTouchingBaseAdjusted && angleFromYaxis < angleOfSpillTouchingBaseAdjusted + (90f - angleOfSpillTouchingBaseAdjusted) * 2)
                {
                    caseNumber = 3;
                }
            }
            else
            {
                caseNumber = 1;
            }
        }

        return caseNumber;
    }

    private float Case1(float angleFromYaxis)
    {
        //// Calculate case 1
        float offset = Mathf.Sin((90 - angleFromYaxis) * Mathf.Deg2Rad) * (-liquidLevelDistanceFromCenterDefault);
        return angleFromYaxis <= 90 ? offset : -offset;
    }

    private float Case2(float angleFromYaxis, float portionOfBase)
    {
        //// Calculate case 2 (-2)
        // This part is pretty complicated. Maybe it could be done better? I did this at 2AM so.....
        float angleBaseEdgeToMiddle = (90 - (Mathf.Atan(cylinderHeight / cylinderDiameter)) * Mathf.Rad2Deg);
        float cylinderDiagonal = Mathf.Sqrt(Mathf.Pow(cylinderHeight, 2) + Mathf.Pow(cylinderDiameter, 2));
        float BE2CLineLength = cylinderDiagonal / 2;
        float angleFromYaxisAdjusted = Mathf.Abs(90 - angleFromYaxis);
        float distanceCoveredByWaterOnEdge = Mathf.Sqrt((2 * portionOfBase) / Mathf.Tan(angleFromYaxisAdjusted * Mathf.Deg2Rad));

        // BE2CLine is the line from the edge of the Base Edge of the cylinder to the Center of rotation of the cylinder
        float angleBetweenWaterLevelAndBE2CLine = (180f - (angleFromYaxisAdjusted + angleBaseEdgeToMiddle));
        float portionOfBE2CLine = (distanceCoveredByWaterOnEdge * Mathf.Sin(angleFromYaxisAdjusted * Mathf.Deg2Rad)) / Mathf.Sin(angleBetweenWaterLevelAndBE2CLine * Mathf.Deg2Rad);
        float portionOfBE2CLineCloserToCenter = BE2CLineLength - portionOfBE2CLine;
        float liquidOffsetFromCenterOfRotation = (Mathf.Sin((180 - angleBetweenWaterLevelAndBE2CLine) * Mathf.Deg2Rad) * portionOfBE2CLineCloserToCenter);

        // Obligatory debugging
        if (LOG)
        {
            Debug.Log(nameof(portionOfBase) + " = " + portionOfBase);
            Debug.Log(nameof(angleFromYaxisAdjusted) + " = " + angleFromYaxisAdjusted);
            Debug.Log(nameof(angleBaseEdgeToMiddle) + " = " + angleBaseEdgeToMiddle);
            Debug.Log(nameof(cylinderDiagonal) + " = " + cylinderDiagonal);
            Debug.Log(nameof(BE2CLineLength) + " = " + BE2CLineLength);
            Debug.Log(nameof(distanceCoveredByWaterOnEdge) + " = " + distanceCoveredByWaterOnEdge);
            Debug.Log(nameof(portionOfBE2CLine) + " = " + portionOfBE2CLine);
            Debug.Log(nameof(portionOfBE2CLineCloserToCenter) + " = " + portionOfBE2CLineCloserToCenter);
            Debug.Log(nameof(angleBetweenWaterLevelAndBE2CLine) + " = " + angleBetweenWaterLevelAndBE2CLine);
            Debug.Log(nameof(liquidOffsetFromCenterOfRotation) + " = " + liquidOffsetFromCenterOfRotation);
        }

        return liquidOffsetFromCenterOfRotation;
    }

    private float Case3(float angleFromYaxis)
    {
        distanceCoveredByWaterOnBase = liquidArea / cylinderHeight;
        float offsetBothBasesTouching = Mathf.Sin((angleFromYaxis) * Mathf.Deg2Rad) * ((cylinderDiameter / 2) - distanceCoveredByWaterOnBase);
        //Debug.Log(offsetBothBasesTouching);
        if (LOG) Debug.Log(nameof(angleFromYaxis) + " = " + angleFromYaxis);
        if (LOG) Debug.Log(nameof(distanceCoveredByWaterOnBase) + " = " + distanceCoveredByWaterOnBase);
        if (LOG) Debug.Log(nameof(offsetBothBasesTouching) + " = " + offsetBothBasesTouching);

        return offsetBothBasesTouching;
    }

    private void CalculateAngles()
    {
        //// Calculate the angle at which the object has to be rotated so that the liquid touches the TOP lip
        float angleOfSpill = Mathf.Atan(cylinderDiameter / (2 * cylinderHeight - (2 * waterHeight)));
        // Convert to degrees and object rotation
        angleOfSpillAdjusted = 90 - angleOfSpill * Mathf.Rad2Deg;
        if (LOG) Debug.Log(nameof(angleOfSpillAdjusted) + " = " + angleOfSpillAdjusted); // Log the value


        //// Calculate the angle at which the object has to be rotated so that the liquid touches the BOTTOM lip
        float angleOfChange;
        // Check if we are touching the top base or the bottom base
        if (2 * waterHeight >= cylinderHeight)
        {
            // We are touching the bottom lip AND the top base
            angleOfChange = Mathf.Atan(distanceCoveredByAirOnTopBase / (cylinderHeight));
        }
        else
        {
            // We are touching the bottom lip and the edge of the cylinder
            angleOfChange = Mathf.Atan(cylinderDiameter / (2 * waterHeight));
        }
        // Convert to degrees and object rotation
        angleOfChangeAdjusted = 90 - angleOfChange * Mathf.Rad2Deg;
        if (LOG) Debug.Log(nameof(angleOfChangeAdjusted) + " = " + angleOfChangeAdjusted); // Log the value


        //// Calculate the angle at which the object has to be rotated so that the liquid touches the TOP lip
        // This is different in that the liquid touches the base of the cylinder as well
        float angleOfSpillTouchingBase = Mathf.Atan((2 * liquidArea) / Mathf.Pow(cylinderHeight, 2));
        // Convert to degrees and make that something we can check for
        angleOfSpillTouchingBaseAdjusted = 90 - angleOfSpillTouchingBase * Mathf.Rad2Deg;
        if (LOG) Debug.Log(nameof(angleOfSpillTouchingBaseAdjusted) + " = " + angleOfSpillTouchingBaseAdjusted);
    }

    public float scale(float OldMin, float OldMax, float NewMin, float NewMax, float Value)
    {
        // Maps a value from one range to another
        float OldRange = (OldMax - OldMin);
        float NewRange = (NewMax - NewMin);
        return ((((Value - OldMin) * NewRange) / OldRange) + NewMin);
    }

    private Vector3 calculateWobble()
    {
        time += Time.deltaTime;
        // decrease wobble over time
        wobbleAmountToAddX = Mathf.Lerp(wobbleAmountToAddX, 0, Time.deltaTime * (Recovery));
        wobbleAmountToAddZ = Mathf.Lerp(wobbleAmountToAddZ, 0, Time.deltaTime * (Recovery));

        // make a sine wave of the decreasing wobble
        float pulse = 2 * Mathf.PI * WobbleSpeed;
        float wobbleAmountX = wobbleAmountToAddX * Mathf.Sin(pulse * time);
        float wobbleAmountZ = wobbleAmountToAddZ * Mathf.Sin(pulse * time);

        // velocity
        Vector3 velocity = (lastPos - transform.position) / Time.deltaTime;
        Vector3 angularVelocity = transform.rotation.eulerAngles - lastRot;


        // add clamped velocity to wobble
        wobbleAmountToAddX += Mathf.Clamp((velocity.x + (angularVelocity.z * 0.2f)) * MaxWobble, -MaxWobble, MaxWobble);
        wobbleAmountToAddZ += Mathf.Clamp((velocity.z + (angularVelocity.x * 0.2f)) * MaxWobble, -MaxWobble, MaxWobble);

        // keep last position
        lastPos = transform.position;
        lastRot = transform.rotation.eulerAngles;

        return new Vector3(wobbleAmountX, 0, wobbleAmountZ);
    }
}
