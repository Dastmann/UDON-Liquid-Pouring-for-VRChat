/*
    WobbleAndVolume U# Script by Dastmann
    Part of Dastmann's liquid pouring system for VRChat

    This script adds volume consistence and surface wobble to the liquid shader

    
    Some parts are maybe a bit complicated or at least badly written. I might fix that later ^^

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
    [Header("References")]
    public Transform parentTransform;
    public MeshRenderer liquidMeshRenderer;
    [Header("Basic options")]
    public float fillPercentageStart = 0.5f;
    public float cylinderDiameter = 1; // In meters
    public float cylinderHeight = 2; // In meters
    [Tooltip("If the liquid should wobble (BETA)")] public bool WOBBLE = true;
    [Tooltip("If the liquid should spill out")] public bool SPILL = true;
    [Header("Advanced options")]
    public bool LOG = false; // Display Debug.Log() messages
    public bool LOG2 = false;
    public float gravitationalAcceleration = 9.8f;

    // Private variables
    private float offset; // Offset of shader Fill Amount
    private float cylinder2DCrossSectionArea; // This is only a 2D cross section of the cylinder. It makes the calculations easier
    private float baseArea;
    private float cylinderRadius; // Not necessary, but looks prettier
    public float fillPercentageCurrent; //// ONLY TEMPORARILY PUBLIC ////
    private float lastFillPercentage = -1f; // This is so the we don't miss the first fill
    private float liquidHeight;
    private float liquidLevelDistanceFromCenterDefault;
    private float angleOfSpillAdjusted;
    private float liquidArea;
    private float angleOfChangeAdjusted;
    private float distanceCoveredByWaterOnBase;
    private float distanceCoveredByAirOnTopBase;
    private float angleOfSpillTouchingBaseAdjusted;
    private bool computationOfLiquids = true;
    private float cylinderDiagonal;
    private float currentLiquidVelocity = 0f;
    private float lastAngleFromYaxis;
    private float lastOffset;


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
        cylinder2DCrossSectionArea = cylinderDiameter * cylinderHeight;
        fillPercentageCurrent = fillPercentageStart;
        cylinderRadius = cylinderDiameter / 2;
        baseArea = Mathf.PI * Mathf.Pow(cylinderRadius, 2);
        cylinderDiagonal = Mathf.Sqrt(Mathf.Pow(cylinderHeight, 2) + Mathf.Pow(cylinderDiameter, 2));
        lastAngleFromYaxis = GetAngleFromYaxis() + 25.15f; // Some random number so the function gets called the first time
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

    public void ParticleCollision()
    {
        Debug.Log("Amazing!");
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
            currentLiquidVelocity = 0f;
        }
    }

    private void ComputeLiquids()
    {
        //// Get the angle between objects up axis and world Y axis
        float angleFromYaxis = GetAngleFromYaxis();

        if (fillPercentageCurrent < 0.000001) fillPercentageCurrent = 0f;

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
            liquidHeight = scale(0f, 1f, 0f, cylinderHeight, fillPercentageCurrent);
            if (LOG) Debug.Log(nameof(liquidHeight) + " = " + liquidHeight);

            liquidArea = liquidHeight * cylinderDiameter;
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


        Vector3 wobbleDirections = Vector3.zero;
        if (lastAngleFromYaxis != angleFromYaxis || lastOffset != offset)
        {
            lastAngleFromYaxis = angleFromYaxis;
            if (fillPercentageCurrent != 0)
            {
                lastOffset = offset;

                //// Get case number [1,2,-2,3]
                int caseNumber;
                caseNumber = GetCase(angleFromYaxis);

                //// Hande different cases
                offset = GetOffsetFromCase(angleFromYaxis, caseNumber);
            }

            //// Get wobble wobble directions and do a last check if empty
            wobbleDirections = GetWobbleDirections();

            if (LOG) Debug.Log(nameof(offset) + " = " + offset);


        }
        else
        {
            lastAngleFromYaxis = angleFromYaxis;
            
            //// Get wobble wobble directions and do a last check if empty
            wobbleDirections = GetWobbleDirections();
        }

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
            offset = cylinderDiagonal / 2 + 0.01f;
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
                // We put in the case number so that we know when to pour. Same goes for -2
                offset = Case2(angleFromYaxis, liquidArea, caseNumber);
                break;
            case -2:
                // This will calculate it for the air so we have to reverse the result
                float airArea = cylinder2DCrossSectionArea - liquidArea;
                offset = -Case2(angleFromYaxis, airArea, caseNumber);
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
        if (2 * liquidHeight >= cylinderHeight)
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

        //// TODO: IMPLEMENT SPILL ////
        float liquidAcceleration = 0, volumetricFlowRate = 0;
        bool isSpilling = angleFromYaxis >= 90 ? true : false;
        if (isSpilling)
        {
            liquidAcceleration = gravitationalAcceleration * Mathf.Sin((angleFromYaxis - 90) * Mathf.Deg2Rad);
            currentLiquidVelocity += liquidAcceleration * Time.deltaTime;
            volumetricFlowRate = currentLiquidVelocity * baseArea;
        }

        // Transfer volumetric flow rate to the fill percentage
        float currentVolume = baseArea * liquidHeight;
        float amountOfLiquidLost = volumetricFlowRate * Time.deltaTime; // This is in m^3
        float percentageOfLiquidLost = scale(0f, baseArea * cylinderHeight, 0f, 1f, amountOfLiquidLost);
        // Now we finally set the current fill percentage
        if (SPILL) fillPercentageCurrent = fillPercentageCurrent - percentageOfLiquidLost;

        if (LOG2) Debug.Log(nameof(liquidAcceleration) + " = " + liquidAcceleration);
        if (LOG2) Debug.Log(nameof(volumetricFlowRate) + " = " + volumetricFlowRate + " m^3 per second");
        if (LOG2) Debug.Log(nameof(currentVolume) + " = " + currentVolume + " m^3");
        if (LOG2) Debug.Log(nameof(amountOfLiquidLost) + " = " + amountOfLiquidLost + " m^3");
        if (LOG2) Debug.Log(nameof(percentageOfLiquidLost) + " = " + percentageOfLiquidLost);
        if (LOG2) Debug.Log(nameof(currentLiquidVelocity) + " = " + currentLiquidVelocity);

        return angleFromYaxis <= 90 ? offset : -offset;
    }

    private float Case2(float angleFromYaxis, float areaOfMeasurement, float caseNumber)
    {
        //// Calculate case 2 (-2)
        // This part is pretty complicated. Maybe it could be done better? I did this at 2AM so.....
        float angleBaseEdgeToMiddle = (90 - (Mathf.Atan(cylinderHeight / cylinderDiameter)) * Mathf.Rad2Deg);
        float BE2CLineLength = cylinderDiagonal / 2;
        float angleFromYaxisAdjusted = Mathf.Abs(90 - angleFromYaxis);
        float distanceCoveredByLiquidOnEdge = Mathf.Sqrt((2 * areaOfMeasurement) / Mathf.Tan(angleFromYaxisAdjusted * Mathf.Deg2Rad));

        // BE2CLine is the line from the edge of the Base Edge of the cylinder to the Center of rotation of the cylinder
        float angleBetweenWaterLevelAndBE2CLine = (180f - (angleFromYaxisAdjusted + angleBaseEdgeToMiddle));
        float portionOfBE2CLine = (distanceCoveredByLiquidOnEdge * Mathf.Sin(angleFromYaxisAdjusted * Mathf.Deg2Rad)) / Mathf.Sin(angleBetweenWaterLevelAndBE2CLine * Mathf.Deg2Rad);
        float portionOfBE2CLineCloserToCenter = BE2CLineLength - portionOfBE2CLine;
        float liquidOffsetFromCenterOfRotation = (Mathf.Sin((180 - angleBetweenWaterLevelAndBE2CLine) * Mathf.Deg2Rad) * portionOfBE2CLineCloserToCenter);


        //// Pouring calculations
        // Calculate the velocity of pouring liquid
        float liquidHorizontalVelocity = 0;
        float distanceCoveredByLiquidOnBase = (2 * areaOfMeasurement) / distanceCoveredByLiquidOnEdge;
        if (caseNumber == 2)
        {
            float distanceFromWaterLevelAndCorner = Mathf.Sin(angleFromYaxis * Mathf.Deg2Rad) * distanceCoveredByLiquidOnBase;
            liquidHorizontalVelocity = Mathf.Sqrt(2 * gravitationalAcceleration * (distanceFromWaterLevelAndCorner / 2));

            if (LOG2) Debug.Log(nameof(distanceFromWaterLevelAndCorner) + " = " + distanceFromWaterLevelAndCorner);
        }
        else if (caseNumber == -2)
        {
            float distanceFromWaterLevelAndOtherCorner = Mathf.Sin(angleFromYaxis * Mathf.Deg2Rad) * (cylinderDiameter - distanceCoveredByLiquidOnBase);
            liquidHorizontalVelocity = Mathf.Sqrt(2 * gravitationalAcceleration * (distanceFromWaterLevelAndOtherCorner / 2));

            if (LOG2) Debug.Log(nameof(distanceFromWaterLevelAndOtherCorner) + " = " + distanceFromWaterLevelAndOtherCorner);
        }

        // Calculate cross section of flow
        float areaOfCircularSegmentAdjusted = GetAreaOfCircularSegment(angleFromYaxis, distanceCoveredByLiquidOnBase);
        if (caseNumber == -2) areaOfCircularSegmentAdjusted = baseArea - areaOfCircularSegmentAdjusted;

        // Calculate volumetric flow rate
        float currentVolume = baseArea * liquidHeight;
        float liquidAcceleration = 0, volumetricFlowRate = 0;
        bool isSpilling = false;
        if (caseNumber == 2)
        {
            isSpilling = angleFromYaxis >= 90 ? true : false;

            if (isSpilling)
            {
                liquidAcceleration = gravitationalAcceleration * Mathf.Sin((angleFromYaxis - 90) * Mathf.Deg2Rad);
                currentLiquidVelocity += liquidAcceleration * Time.deltaTime;
                volumetricFlowRate = currentLiquidVelocity * baseArea;
            }
        }
        else if (caseNumber == -2)
        {
            currentLiquidVelocity = liquidHorizontalVelocity;
            volumetricFlowRate = liquidHorizontalVelocity * areaOfCircularSegmentAdjusted;

            isSpilling = true;
        }

        // Check if the liquid should be spilling out
        float amountOfLiquidLost = 0, percentageOfLiquidLost = 0;
        if (isSpilling)
        {
            // Transfer volumetric flow rate to the fill percentage
            amountOfLiquidLost = volumetricFlowRate * Time.deltaTime; // This is in m^3
            percentageOfLiquidLost = scale(0f, baseArea * cylinderHeight, 0f, 1f, amountOfLiquidLost);

            // Now we finally set the current fill percentage
            if (SPILL) fillPercentageCurrent = fillPercentageCurrent - percentageOfLiquidLost;
        }

        // Obligatory debugging
        if (LOG)
        {
            Debug.Log(nameof(areaOfMeasurement) + " = " + areaOfMeasurement);
            Debug.Log(nameof(angleFromYaxisAdjusted) + " = " + angleFromYaxisAdjusted);
            Debug.Log(nameof(angleBaseEdgeToMiddle) + " = " + angleBaseEdgeToMiddle);
            Debug.Log(nameof(cylinderDiagonal) + " = " + cylinderDiagonal);
            Debug.Log(nameof(BE2CLineLength) + " = " + BE2CLineLength);
            Debug.Log(nameof(distanceCoveredByLiquidOnEdge) + " = " + distanceCoveredByLiquidOnEdge);
            Debug.Log(nameof(portionOfBE2CLine) + " = " + portionOfBE2CLine);
            Debug.Log(nameof(portionOfBE2CLineCloserToCenter) + " = " + portionOfBE2CLineCloserToCenter);
            Debug.Log(nameof(angleBetweenWaterLevelAndBE2CLine) + " = " + angleBetweenWaterLevelAndBE2CLine);
            Debug.Log(nameof(liquidOffsetFromCenterOfRotation) + " = " + liquidOffsetFromCenterOfRotation);
        }

        if (LOG2) Debug.Log("Time.deltaTime = " + Time.deltaTime);
        if (LOG2) Debug.Log(nameof(liquidHorizontalVelocity) + " = " + liquidHorizontalVelocity);
        if (LOG2) Debug.Log(nameof(distanceCoveredByLiquidOnBase) + " = " + distanceCoveredByLiquidOnBase);
        if (LOG2) Debug.Log(nameof(liquidAcceleration) + " = " + liquidAcceleration);
        if (LOG2) Debug.Log(nameof(volumetricFlowRate) + " = " + volumetricFlowRate + " m^3 per second");
        if (LOG2) Debug.Log(nameof(currentVolume) + " = " + currentVolume + " m^3");
        if (LOG2) Debug.Log(nameof(amountOfLiquidLost) + " = " + amountOfLiquidLost + " m^3");
        if (LOG2) Debug.Log(nameof(percentageOfLiquidLost) + " = " + percentageOfLiquidLost);
        if (LOG2) Debug.Log(nameof(currentLiquidVelocity) + " = " + currentLiquidVelocity);


        return liquidOffsetFromCenterOfRotation;
    }

    private float Case3(float angleFromYaxis)
    {
        distanceCoveredByWaterOnBase = liquidArea / cylinderHeight;
        float offsetBothBasesTouching = Mathf.Sin((angleFromYaxis) * Mathf.Deg2Rad) * ((cylinderDiameter / 2) - distanceCoveredByWaterOnBase);
        //Debug.Log(offsetBothBasesTouching);
        if (LOG || LOG2) Debug.Log(nameof(angleFromYaxis) + " = " + angleFromYaxis);
        if (LOG) Debug.Log(nameof(distanceCoveredByWaterOnBase) + " = " + distanceCoveredByWaterOnBase);
        if (LOG) Debug.Log(nameof(offsetBothBasesTouching) + " = " + offsetBothBasesTouching);

        //// Now we calculate the volumetric flow rate of the liquid!
        // This part is also pretty complicated. I'm sure it could be done better.       Why do I keep doing this at 2AM lol
        // Calculate liquid velocity
        float distanceCoveredByWaterOnTop = (liquidArea - ((Mathf.Tan((90f - angleFromYaxis) * Mathf.Deg2Rad) * Mathf.Pow(cylinderHeight, 2)) / 2)) / cylinderHeight;
        float distanceBetweenWaterLevels = Mathf.Cos((90f - angleFromYaxis) * Mathf.Deg2Rad) * distanceCoveredByWaterOnTop;
        float liquidVelocity = Mathf.Sqrt(2 * gravitationalAcceleration * (distanceBetweenWaterLevels / 2));

        // Calculate cross section of flow
        float areaOfCircularSegmentAdjusted = GetAreaOfCircularSegment(angleFromYaxis, distanceCoveredByWaterOnTop);

        // Calculate volumetric flow rate
        float liquidAcceleration = 0, volumetricFlowRate = 0;
        bool isSpilling = angleFromYaxis >= 90 ? true : false;
        if (isSpilling)
        {
            liquidAcceleration = gravitationalAcceleration * Mathf.Sin((angleFromYaxis - 90) * Mathf.Deg2Rad);
            currentLiquidVelocity += liquidAcceleration * Time.deltaTime;
            volumetricFlowRate = currentLiquidVelocity * baseArea;
        }
        else
        {
            volumetricFlowRate = liquidVelocity * areaOfCircularSegmentAdjusted;
            currentLiquidVelocity = liquidVelocity;
        }

        // Transfer volumetric flow rate to the fill percentage
        float currentVolume = baseArea * liquidHeight;
        float amountOfLiquidLost = volumetricFlowRate * Time.deltaTime; // This is in m^3
        float percentageOfLiquidLost = scale(0f, baseArea * cylinderHeight, 0f, 1f, amountOfLiquidLost);
        // Now we finally set the current fill percentage
        if (SPILL) fillPercentageCurrent = fillPercentageCurrent - percentageOfLiquidLost;

        if (LOG2) Debug.Log("Time.deltaTime = " + Time.deltaTime);

        if (LOG2) Debug.Log(nameof(distanceCoveredByWaterOnTop) + " = " + distanceCoveredByWaterOnTop);
        if (LOG2) Debug.Log(nameof(distanceBetweenWaterLevels) + " = " + distanceBetweenWaterLevels);
        if (LOG2) Debug.Log(nameof(liquidVelocity) + " = " + liquidVelocity);
        if (LOG2) Debug.Log(nameof(baseArea) + " = " + baseArea);
        if (LOG2) Debug.Log(nameof(liquidAcceleration) + " = " + liquidAcceleration);
        if (LOG2) Debug.Log(nameof(volumetricFlowRate) + " = " + volumetricFlowRate + " m^3 per second");
        if (LOG2) Debug.Log(nameof(currentVolume) + " = " + currentVolume + " m^3");
        if (LOG2) Debug.Log(nameof(amountOfLiquidLost) + " = " + amountOfLiquidLost + " m^3");
        if (LOG2) Debug.Log(nameof(percentageOfLiquidLost) + " = " + percentageOfLiquidLost);


        return offsetBothBasesTouching;
    }

    private float GetAreaOfCircularSegment(float angleFromYaxis, float distanceCoveredByWaterOnTop)
    {
        // Calculate the circular segment
        float distanceFromBaseCenterToWaterLevel = (cylinderRadius - distanceCoveredByWaterOnTop);
        float halfDistanceOfWaterLevelOnBase = Mathf.Sqrt(Mathf.Pow(cylinderRadius, 2) - Mathf.Pow(distanceFromBaseCenterToWaterLevel, 2));
        float areaOfTriangularSegment = (halfDistanceOfWaterLevelOnBase * distanceFromBaseCenterToWaterLevel);
        float centralAngleAboveWaterLevel = 2 * Mathf.Asin((halfDistanceOfWaterLevelOnBase / cylinderRadius)) * Mathf.Rad2Deg;
        if (areaOfTriangularSegment < 0) centralAngleAboveWaterLevel = 360 - centralAngleAboveWaterLevel; // invert the selection if water is above the center of the base
        float areaOfCircularSegment = baseArea * (centralAngleAboveWaterLevel / 360) - areaOfTriangularSegment;
        float areaOfCircularSegmentAdjusted = areaOfCircularSegment * Mathf.Sin(angleFromYaxis * Mathf.Deg2Rad);

        if (LOG2) Debug.Log(nameof(distanceFromBaseCenterToWaterLevel) + " = " + distanceFromBaseCenterToWaterLevel);
        if (LOG2) Debug.Log(nameof(halfDistanceOfWaterLevelOnBase) + " = " + halfDistanceOfWaterLevelOnBase);
        if (LOG2) Debug.Log(nameof(areaOfTriangularSegment) + " = " + areaOfTriangularSegment);
        if (LOG2) Debug.Log(nameof(centralAngleAboveWaterLevel) + " = " + centralAngleAboveWaterLevel);
        if (LOG2) Debug.Log(nameof(areaOfCircularSegment) + " = " + areaOfCircularSegment);
        if (LOG2) Debug.Log(nameof(areaOfCircularSegmentAdjusted) + " = " + areaOfCircularSegmentAdjusted);

        return areaOfCircularSegmentAdjusted;
    }

    private void CalculateAngles()
    {
        //// Calculate the angle at which the object has to be rotated so that the liquid touches the TOP lip
        float angleOfSpill = Mathf.Atan(cylinderDiameter / (2 * cylinderHeight - (2 * liquidHeight)));
        // Convert to degrees and object rotation
        angleOfSpillAdjusted = 90 - angleOfSpill * Mathf.Rad2Deg;
        if (LOG) Debug.Log(nameof(angleOfSpillAdjusted) + " = " + angleOfSpillAdjusted); // Log the value


        //// Calculate the angle at which the object has to be rotated so that the liquid touches the BOTTOM lip
        float angleOfChange;
        // Check if we are touching the top base or the bottom base
        if (2 * liquidHeight >= cylinderHeight)
        {
            // We are touching the bottom lip AND the top base
            angleOfChange = Mathf.Atan(distanceCoveredByAirOnTopBase / (cylinderHeight));
        }
        else
        {
            // We are touching the bottom lip and the edge of the cylinder
            angleOfChange = Mathf.Atan(cylinderDiameter / (2 * liquidHeight));
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
