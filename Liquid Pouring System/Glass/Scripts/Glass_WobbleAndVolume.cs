/*
    WobbleAndVolume U# Script by Dastmann
    Part of Dastmann's liquid pouring system for VRChat

    This script adds volume consistence and surface wobble to the liquid shader

    It might not be written the best, but it works pretty well. And that's all that matters ^^

    This project's GitHub: https://github.com/Dastmann/UDON-Liquid-Pouring-for-VRChat

    Wobble part of code and current shader by Minions Art
        Check them out -> https://www.patreon.com/posts/quick-game-art-18245226
*/

using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class Glass_WobbleAndVolume : UdonSharpBehaviour
{
    //// Public variables, accessible in the inspector
    [Header("References")]
    public Transform parentTransform;
    public MeshRenderer liquidMeshRenderer;
    public Transform particleSystemParentTransform;
    public Transform particleContainerTransform;
    public ParticleSystem outflowParticleSystem;
    public Transform audioSourceTransform; // Sound System
    public AudioClip audioclip;

    [Header("Basic options")]
    [Range(0.0f, 0.999f), Tooltip("Percentage of liquid the glass should spawn with")] public float fillPercentageStart = 0.5f;
    [Tooltip("Color the glass should spawn with")] public Color startColor = new Color(1f, 0.01568627f, 0.5083251f, 1f);
    [Tooltip("In meters")] public float cylinderDiameter = 1; // In meters
    [Tooltip("In meters")] public float cylinderHeight = 2; // In meters
    [Tooltip("In meters")] public float glassThickness = 0; // In meters
    [Tooltip("If the liquid should wobble (BETA)")] public bool WOBBLE = true;
    [Tooltip("If the liquid should spill out")] public bool SPILL = true;
    [Tooltip("If it should receive new liquid by collision")] public bool P_COLLISION = true;
    [Tooltip("Is the glass going to make a sound when colliding")] public bool SOUND = true;

    private bool LOG = false; // Display Debug.Log() messages
    private bool LOG2 = false;
    [Tooltip("Particle System rotation vectors visualization (basically just for show, since it shouldn't need debugging)")] private bool LOG_RAYS = false;

    [Header("Advanced options")]
    public float gravitationalAcceleration = 9.8f;

    //// Private variables
    private float offset; // Offset of shader Fill Amount
    private float cylinder2DCrossSectionArea; // This is only a 2D cross section of the cylinder. It makes the calculations easier
    private float baseArea;
    private float cylinderRadius; // Not necessary, but looks prettier
    [Range(0.0f, 0.999f)] private float fillPercentageCurrent; //// ONLY TEMPORARILY PUBLIC ////
    private float lastFillPercentage = -1f; // This is so the we don't miss the first fill
    private Color currentColor;
    private float angleTreshold = 1f; // Simulate surface tension. Avoids simulating cases where the flowrate is really small
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
    private float frictionFactor = 0.2f; // Fake friction
    private bool isSystemStopped = false;
    private Vector3 localUpVector;
    private Vector3 localForwardVector;
    private Vector3 currentPositionWorld;
    private Vector3 currentRotationWorld;
    private Vector3 lastPositionWorld;
    private MaterialPropertyBlock properties;

    //// Wobble variables
    public float MaxWobble = 0.03f;
    public float WobbleSpeed = 1f;
    public float Recovery = 1f;
    private Vector3 lastPos;
    private Vector3 lastRot;
    private float time;
    private float wobbleAmountToAddX;
    private float wobbleAmountToAddZ;


    //// Particle collision variables
    private ParticleSystem particleSys;
    private Transform particleTransform;
    private UdonBehaviour particleRootBehaviour;
    private float[] liquidAmounts, times;
    private Color[] colors;
    private int lastID;
    private float lastCollisionTime;
    private float maxOffsetTime = 0.25f; // This will change based on framerate (not yet implemented)
    private bool P_LOG = false;
    private bool P_LOG2 = false;
    private bool LOG_ARR = false;
    private GameObject other;
    private float maxFillPercentage = 0.99f;
    private int numOfCollisions;
    private float timeDelayParticleRotation = -0.1f;

    //// Particle System Emitter properties
    [Tooltip("How many frames worth of data should be stored in memory (50 seems to bee good)")] public int outflowArraySize = 50;
    private float[] liquidAmountsOut;
    private float[] timesOut;
    private Color[] colorsOut;
    private int position;
    private int outflowArrayLimit;

    //// Sound system
    private AudioSource audioSource;
    private float previousCollisionTime;
    private float collisionTimeTreshold = 0.1f; // In seconds. Used to avoid multi collisions
    [Tooltip("Makes the sound come from the point of collision (kinda pointless but whatever)")]public bool realisticSound = false;

    void Start()
    {
        // Calculate constants and apply default values
        cylinder2DCrossSectionArea = cylinderDiameter * cylinderHeight;
        fillPercentageCurrent = fillPercentageStart;
        currentColor = startColor;
        cylinderRadius = cylinderDiameter / 2;
        baseArea = Mathf.PI * Mathf.Pow(cylinderRadius, 2);
        cylinderDiagonal = Mathf.Sqrt(Mathf.Pow(cylinderHeight, 2) + Mathf.Pow(cylinderDiameter, 2));

        properties = new MaterialPropertyBlock();
        liquidMeshRenderer.GetPropertyBlock(properties);

        localUpVector = parentTransform.up;
        localForwardVector = parentTransform.forward;
        lastAngleFromYaxis = GetAngleFromYaxis() + 25.15f; // Some random number so the function gets called the first time
        outflowParticleSystem.Stop();

        // Liquid spilling arrays and variables
        liquidAmountsOut = new float[outflowArraySize];
        timesOut = new float[outflowArraySize];
        colorsOut = new Color[outflowArraySize];
        outflowArrayLimit = outflowArraySize - 1;
        position = outflowArrayLimit;

        audioSource = audioSourceTransform.gameObject.GetComponent<AudioSource>();
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

    void OnCollisionEnter(Collision collision)
    {
        if (SOUND)
        {
            // Play a sound if the colliding objects had a big impact.
            if (collision.relativeVelocity.magnitude > 2 && Time.time - previousCollisionTime > collisionTimeTreshold)
            {
                previousCollisionTime = Time.time;
                if (realisticSound)
                {
                    // Set the sound source position to the collision contact point
                    ContactPoint contact = collision.GetContact(0);
                    Vector3 pointOfContact = contact.point;

                    audioSourceTransform.position = pointOfContact;
                }

                audioSource.Play();
                audioSource.pitch = Random.Range(0.98f, 1.02f);
            }
        }
    }


    public void ParticleCollision() // Custom method - called from GlassCollider script on GlassParticleCollider
    {
        if (!P_COLLISION) return;

        if (P_LOG) Debug.Log(other);
        numOfCollisions++;

        var otherID = other.GetInstanceID();
        if (otherID != lastID)
        {
            particleSys = other.GetComponent<ParticleSystem>();
            particleTransform = other.GetComponent<Transform>();
            if (other.name == "ContainerColliderOnlyParticles_Glass")
            {
                // Awful sin
                particleRootBehaviour = (UdonBehaviour)particleTransform.parent.parent.parent.parent.parent.gameObject.GetComponent(typeof(UdonBehaviour));
            }
            else if (other.name == "ContainerColliderOnlyParticles_Spout")
            {
                particleRootBehaviour = (UdonBehaviour)particleTransform.parent.parent.gameObject.GetComponent(typeof(UdonBehaviour));
            }
            lastID = otherID;
            if (P_LOG) Debug.Log("new ID: " + otherID);
        }

        // Make sure that particleRootBehaviour is not null. This can happen if the collision is with an "unsupported" particle system
        if (particleRootBehaviour != null)
        {

            if (P_LOG) Debug.Log(particleSys.main.startSize.constant);
            ParticleSystem r = particleSys;
            ParticleSystem.Particle[] particleArray;
            particleArray = new ParticleSystem.Particle[particleSys.main.maxParticles];

            int numParticles = particleSys.GetParticles(particleArray);
            float[] rotationsArray = new float[numParticles];

            string log = "";
            string log2 = "";
            for (int i = 0; i < numParticles; i++)
            {
                log += particleArray[i].GetCurrentSize(particleSys) + "  ";
                log2 += particleArray[i].rotation + "  "; // We use the rotation for getting the elapsed time in seconds from particle birth. This even works with sub-emitters
                rotationsArray[i] = particleArray[i].rotation;
            }

            if (P_LOG) Debug.Log(log);
            if (P_LOG) Debug.Log(log2);
            if (P_LOG) Debug.Log(numParticles);
            if (P_LOG) Debug.Log("Current time: " + Time.time);

            liquidAmounts = (float[])particleRootBehaviour.GetProgramVariable("liquidAmountsOut");
            times = (float[])particleRootBehaviour.GetProgramVariable("timesOut");
            colors = (Color[])particleRootBehaviour.GetProgramVariable("colorsOut");
            // Use rolling arrays - get the position of the last set value
            int positionOfLastValue = (int)particleRootBehaviour.GetProgramVariable("position");

            if (P_LOG2) printArray_Float(liquidAmounts);
            if (P_LOG2) printArray_Float(times);

            if (P_LOG2) Debug.Log("positionOfLastValue = " + positionOfLastValue);


            float[] originalTimes = new float[rotationsArray.Length];
            float currentTime = Time.time;
            for (int i = 0; i < rotationsArray.Length; i++)
            {
                float subtractedTime = currentTime - rotationsArray[i];
                // Check if there is space for the offset. Otherwise it will crash
                if (subtractedTime - timeDelayParticleRotation > times[0])
                {
                    originalTimes[i] = subtractedTime;
                }
                else
                {
                    originalTimes[i] = subtractedTime - timeDelayParticleRotation;
                }
            }
            //if (P_LOG2) printFloatArray(rotationsArray);
            if (P_LOG2) printArray_Float(originalTimes);

            if (originalTimes.Length > 1)
            {
                originalTimes = SortFloatArray(originalTimes); // A very basic sorting algorithm, but it shouldn't cost too much performance
                if (P_LOG2) Debug.Log("Sorted:");
                if (P_LOG2) printArray_Float(originalTimes);
            }

            float liquidVolumeToAdd = 0f;
            Color finalColorToMixWith = currentColor;
            for (int k = 0; k < originalTimes.Length; k++)
            {
                float originalTime = originalTimes[k];

                float closestTimeToCurrent = times[0];
                int positionInArrays = 0;
                int timesLength = times.Length;
                for (int i = 0; i < timesLength; i++)
                {
                    int newValue = i + positionOfLastValue;
                    if (newValue >= timesLength) newValue -= timesLength;

                    if (originalTime > times[newValue])
                    {
                        int newValueMinus1 = (newValue == 0) ? timesLength - 1 : newValue - 1;
                        // Stop the script from accessing "negative" values of the array
                        if (newValue == positionOfLastValue)
                        {
                            positionInArrays = newValue;
                        }
                        // Calculate the average and comapare to the values closest to it and select the closer one
                        else if ((times[newValue] + times[newValueMinus1]) / 2 < originalTime)
                        {
                            positionInArrays = newValueMinus1;
                        }
                        else
                        {
                            positionInArrays = newValue;
                        }

                        closestTimeToCurrent = times[positionInArrays];
                        break;
                    }
                }
                if (P_LOG2) Debug.Log("closestTimeToCurrent = " + closestTimeToCurrent);
                if (P_LOG2) Debug.Log("liquidAmountLostAtThatTime = " + liquidAmounts[positionInArrays]);
                if (P_LOG2) Debug.Log("colorAtThatTime = " + colors[positionInArrays]);
                if (P_LOG2) Debug.Log("positionInArrays = " + positionInArrays);

                // Do a check for "missed collisions" (not every collision is registered)
                // This will make sure we get *most* of the liquid transfered
                if (P_LOG2) Debug.Log("lastCollisionTime = " + lastCollisionTime);
                if (lastCollisionTime < closestTimeToCurrent)
                {
                    if (lastCollisionTime + maxOffsetTime >= closestTimeToCurrent)
                    {
                        if (P_LOG2) Debug.Log("Last and current times are close enough, adding values in between...");
                        int lastPositionInArrays = 0;
                        bool isRollover = false; // Used to tell if we rolled over the max value. Basically overflow
                        for (int i = positionInArrays; i < timesLength + positionOfLastValue; i++)
                        {
                            // Adjust for rollover
                            int i_Adjusted;
                            if (i > timesLength - 1)
                            {
                                i_Adjusted = i - timesLength;
                                isRollover = true;
                            }
                            else
                            {
                                i_Adjusted = i;
                            }

                            if (lastCollisionTime == times[i_Adjusted])
                            {
                                lastPositionInArrays = i_Adjusted;
                                break;
                            }
                        }
                        if (P_LOG2) Debug.Log("lastPositionInArrays = " + lastPositionInArrays);

                        bool isNewValueOlderThanPrevious = isRollover ? lastPositionInArrays + timesLength < positionInArrays : lastPositionInArrays < positionInArrays;
                        if (P_LOG2) Debug.Log("isNewValueSmallerThanPrevious = " + isNewValueOlderThanPrevious);

                        if (isNewValueOlderThanPrevious)
                        {
                            continue;
                        }

                        if (isRollover) lastPositionInArrays += timesLength;

                        for (int i = positionInArrays; i < lastPositionInArrays; i++)
                        {
                            // Adjust for rollover
                            int i_Adjusted;
                            if (i > timesLength - 1)
                            {
                                i_Adjusted = i - timesLength;
                            }
                            else
                            {
                                i_Adjusted = i;
                            }

                            float currentLiquidAmount = liquidAmounts[i_Adjusted];

                            if (liquidVolumeToAdd == 0)
                            {
                                finalColorToMixWith = colors[i_Adjusted];
                            }
                            else
                            {
                                float mixValue = scale(0, liquidVolumeToAdd + currentLiquidAmount, 0, 1, liquidVolumeToAdd);
                                finalColorToMixWith = MixColorsBest(finalColorToMixWith, colors[i_Adjusted], mixValue);
                            }

                            liquidVolumeToAdd += currentLiquidAmount;
                            if (P_LOG) Debug.Log(currentLiquidAmount);
                        }
                    }
                    else
                    {
                        liquidVolumeToAdd += liquidAmounts[positionInArrays];
                        finalColorToMixWith = colors[positionInArrays];
                    }


                    lastCollisionTime = closestTimeToCurrent;

                    if (P_LOG2) Debug.Log("liquidVolumeToAdd = " + liquidVolumeToAdd);
                    if (P_LOG2) Debug.Log("colorToMixWith = " + finalColorToMixWith);
                }

            }

            float percentageOfLiquidGained = scale(0f, baseArea * cylinderHeight, 0f, 1f, liquidVolumeToAdd);

            float newFillPercentage = fillPercentageCurrent + percentageOfLiquidGained;

            // Set the new color of the liquid with mixing it with the old color
            if (P_COLLISION) currentColor = MixColorsBest(finalColorToMixWith, currentColor, scale(0, newFillPercentage, 0, 1, fillPercentageCurrent));
            // Now we finally set the current fill percentage
            if (P_COLLISION) fillPercentageCurrent = newFillPercentage;

            // Limit the amount of liquid (also to prevent weird stuff happening in other places of the script)
            if (fillPercentageCurrent > maxFillPercentage) fillPercentageCurrent = maxFillPercentage;

            if (P_LOG2) Debug.Log("");
        }
    }

    private void CheckForLiquid()
    {
        //// If there is some liquid, compute! Otherwise, save resources
        // This is deliberately after ComputeLiquids() to allow for an extra cycle with the value 0 and clean up
        if (fillPercentageCurrent != 0)
        {
            isSystemStopped = false;
            computationOfLiquids = true;
        }
        else
        {
            // Save on resources
            if (!isSystemStopped)
            {
                isSystemStopped = true;

                computationOfLiquids = false;
                currentLiquidVelocity = 0f;
                outflowParticleSystem.Stop(); // Calling this is expensive
            }
        }
    }

    private void ComputeLiquids()
    {
        // Calling these functions is expensive so we find out now and remember the values
        localUpVector = parentTransform.up;
        localForwardVector = parentTransform.forward;
        currentPositionWorld = parentTransform.position;

        //// Get the angle between objects up axis and world Y axis
        float angleFromYaxis = GetAngleFromYaxis();

        //OffsetParticleSystemRotation(angleFromYaxis);

        bool compute = false;

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
            compute = true; // allow the computation of offset

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
        if (lastAngleFromYaxis != angleFromYaxis || lastOffset != offset || compute == true)
        {
            if (fillPercentageCurrent != 0)
            {
                lastOffset = offset;

                //// Gets the case number [1,2,-2,3]
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
            if (lastPositionWorld != currentPositionWorld || wobbleAmountToAddX != 0 || wobbleAmountToAddZ != 0)
            {
                //// Get wobble wobble directions and do a last check if empty
                wobbleDirections = GetWobbleDirections();
            }
        }

        //// Calculate the other color by shifting the HSV values of the currentColor
        float H, S, V;
        Color.RGBToHSV(currentColor, out H, out S, out V);
        Color topColor = Color.HSVToRGB(H, Mathf.Clamp(S * 0.8f, 0, 1), Mathf.Clamp(V * 1.5f, 0, 1));
        Color foamColor = Color.HSVToRGB(H, Mathf.Clamp(S * 0.5f, 0, 1), Mathf.Clamp(V * 1.8f, 0, 1));
        Color rimColor = Color.HSVToRGB(H, S, Mathf.Clamp(V * 2, 0, 1));

        // Set all calculated values to the material via a material property block
        properties.SetFloat("_FillAmount", 0.5f + offset);
        properties.SetFloat("_WobbleX", wobbleDirections.x);
        properties.SetFloat("_WobbleZ", wobbleDirections.z);
        properties.SetColor("_Tint", currentColor);
        properties.SetColor("_TopColor", topColor);
        properties.SetColor("_FoamColor", foamColor);
        properties.SetColor("_RimColor", rimColor);
        liquidMeshRenderer.SetPropertyBlock(properties);

        lastAngleFromYaxis = angleFromYaxis;
        lastPositionWorld = currentPositionWorld;
    }

    private float GetAngleFromYaxis()
    {
        // Calculate angle from objects up axis to world up (Y) axis. This is used to determine the liquid level and the angles
        float angleFromYaxis = Mathf.Abs(Vector3.SignedAngle(new Vector3(0, 1, 0), localUpVector, localForwardVector));
        if (LOG) Debug.Log(nameof(angleFromYaxis) + " = " + angleFromYaxis);
        return angleFromYaxis;
    }

    private void OffsetParticleSystemRotation(float angleFromYaxis)
    {
        //// Makes the particle system emitter always on the bottom of the top face so that the liquid always flows from the correct point

        // Make a vector in the direcion the glass local up vector is facing. Basically ignore the vertical component of the local up vector
        Vector3 direction = Vector3.Normalize(new Vector3(localUpVector.x, 0, localUpVector.z));
        // Make a right angle vector to the previous one so that we can rotate the other vectors around it
        Vector3 directionRightAngle = Quaternion.Euler(0, 90, 0) * direction;

        // Calculate the rotation the vectors have to be rotated by
        Quaternion rotation = Quaternion.AngleAxis(-angleFromYaxis, directionRightAngle);
        // Rotate the local forward vector by the rotation
        Vector3 forwardRotated = rotation * localForwardVector;

        // Calculate final angle between forwardRotated vector and direction vector 
        float angleOfRotationAlongLocalUp = Vector3.SignedAngle(forwardRotated, direction, new Vector3(0, 1, 0));
        if (LOG) Debug.Log(angleOfRotationAlongLocalUp);

        particleSystemParentTransform.localEulerAngles = new Vector3(0, angleOfRotationAlongLocalUp, 0);

        // Draw cool rays
        if (LOG_RAYS)
        {
            // The resulting angle is between the Yellow and the White vectors
            Debug.DrawRay(parentTransform.position, direction, new Color(255, 255, 0), Time.deltaTime);
            Debug.DrawRay(parentTransform.position, directionRightAngle, new Color(0, 255, 255), Time.deltaTime);

            Debug.DrawRay(parentTransform.position, parentTransform.up, new Color(0, 127, 0), Time.deltaTime);
            Debug.DrawRay(parentTransform.position, parentTransform.forward, new Color(0, 0, 127), Time.deltaTime);
            Debug.DrawRay(parentTransform.position, parentTransform.right, new Color(127, 0, 0), Time.deltaTime);

            Debug.DrawRay(parentTransform.position, forwardRotated, new Color(127, 127, 127), Time.deltaTime);
        }
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
            if (angleFromYaxis - angleTreshold > angleOfSpillAdjusted && angleFromYaxis < angleOfSpillAdjusted + (90f - angleOfSpillAdjusted) * 2)
            {
                // Check if liquid touches both bases at the same time
                if (angleFromYaxis - angleTreshold > angleOfChangeAdjusted && angleFromYaxis < angleOfChangeAdjusted + (90f - angleOfChangeAdjusted) * 2)
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
            if (angleFromYaxis - angleTreshold > angleOfChangeAdjusted && angleFromYaxis < angleOfChangeAdjusted + (90f - angleOfChangeAdjusted) * 2)
            {
                caseNumber = 2;

                // If water is touching both bases at the same time. This will only happen for a short time becouse the water will spill out
                if (angleFromYaxis - angleTreshold > angleOfSpillTouchingBaseAdjusted && angleFromYaxis < angleOfSpillTouchingBaseAdjusted + (90f - angleOfSpillTouchingBaseAdjusted) * 2)
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

        bool isSpilling = angleFromYaxis >= 90 ? true : false;
        if (isSpilling)
        {
            float liquidAcceleration = 0, volumetricFlowRate = 0;

            liquidAcceleration = gravitationalAcceleration * Mathf.Sin((angleFromYaxis - 90) * Mathf.Deg2Rad);
            currentLiquidVelocity += liquidAcceleration * Time.deltaTime;
            volumetricFlowRate = currentLiquidVelocity * baseArea;


            // Transfer volumetric flow rate to the fill percentage
            float currentVolume = baseArea * liquidHeight;
            float amountOfLiquidLost = volumetricFlowRate * Time.deltaTime; // This is in m^3
            float percentageOfLiquidLost = scale(0f, baseArea * cylinderHeight, 0f, 1f, amountOfLiquidLost);

            configureParticleSystem(cylinderDiameter, currentLiquidVelocity, amountOfLiquidLost, angleFromYaxis);

            // Now we finally set the current fill percentage
            if (SPILL) fillPercentageCurrent = fillPercentageCurrent - percentageOfLiquidLost;

            if (LOG2) Debug.Log(nameof(liquidAcceleration) + " = " + liquidAcceleration);
            if (LOG2) Debug.Log(nameof(volumetricFlowRate) + " = " + volumetricFlowRate + " m^3 per second");
            if (LOG2) Debug.Log(nameof(currentVolume) + " = " + currentVolume + " m^3");
            if (LOG2) Debug.Log(nameof(amountOfLiquidLost) + " = " + amountOfLiquidLost + " m^3");
            if (LOG2) Debug.Log(nameof(percentageOfLiquidLost) + " = " + percentageOfLiquidLost);
            if (LOG2) Debug.Log(nameof(currentLiquidVelocity) + " = " + currentLiquidVelocity);
        }
        else
        {
            outflowParticleSystem.Stop();
        }

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

        //// THERE ARE BUGS!! HAVE TO FIX THIS!! NO WAY AROUND IT!! //// THE isSpilling THING IS BROKEN ////
        if (SPILL)
        {
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
                distanceCoveredByLiquidOnBase = cylinderDiameter - distanceCoveredByLiquidOnBase;

                float distanceFromWaterLevelAndOtherCorner = Mathf.Sin(angleFromYaxis * Mathf.Deg2Rad) * (distanceCoveredByLiquidOnBase);
                liquidHorizontalVelocity = Mathf.Sqrt(2 * gravitationalAcceleration * (distanceFromWaterLevelAndOtherCorner / 2));

                if (LOG2) Debug.Log(nameof(distanceFromWaterLevelAndOtherCorner) + " = " + distanceFromWaterLevelAndOtherCorner);
            }

            // Calculate cross section of flow
            float areaOfCircularSegmentAdjusted = GetAreaOfCircularSegment(angleFromYaxis, distanceCoveredByLiquidOnBase);
            //if (caseNumber == -2) areaOfCircularSegmentAdjusted = baseArea - areaOfCircularSegmentAdjusted;

            // Calculate volumetric flow rate
            float currentVolume = baseArea * liquidHeight;
            float liquidAcceleration = 0, volumetricFlowRate = 0;
            bool isSpilling = false;
            if (caseNumber == 2)
            {
                isSpilling = angleFromYaxis >= 90 ? true : false;

                if (isSpilling)
                {
                    // This part was causing me problems since when using the acceleration the final particle velocity unrealisticaly speeds up at the end of pouring
                    // I decided to keep the acceleration for the volumetricFlowRate, but the particles use the liquidHorizontalVelocity which has no acceleration (check line about 40 lines lower)
                    liquidAcceleration = gravitationalAcceleration * Mathf.Sin((angleFromYaxis - 90) * Mathf.Deg2Rad);
                    currentLiquidVelocity += liquidAcceleration * Time.deltaTime;
                    volumetricFlowRate = currentLiquidVelocity * areaOfCircularSegmentAdjusted;
                }
            }
            else if (caseNumber == -2)
            {
                if (angleFromYaxis >= 90)
                {
                    liquidAcceleration = gravitationalAcceleration * Mathf.Sin((angleFromYaxis - 90) * Mathf.Deg2Rad);
                    currentLiquidVelocity += liquidAcceleration * Time.deltaTime * frictionFactor; // Friction factor simulates friction and is just to look nicer
                    volumetricFlowRate = currentLiquidVelocity * baseArea;

                    distanceCoveredByLiquidOnBase = cylinderDiameter;
                }
                else
                {
                    currentLiquidVelocity = liquidHorizontalVelocity;
                    volumetricFlowRate = liquidHorizontalVelocity * areaOfCircularSegmentAdjusted;
                }

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
                if (SPILL)
                {
                    fillPercentageCurrent = fillPercentageCurrent - percentageOfLiquidLost;

                    // I'm using liquidHorizontalVelocity instead of currentLiquidVelocity because the latter due to acceleration makes the liquid shoot out at the end of pouring
                    // This just looks better and possibly is more realistic since there would be some friction irl. However the volume change is still using the acceleration
                    configureParticleSystem(distanceCoveredByLiquidOnBase, liquidHorizontalVelocity, amountOfLiquidLost, angleFromYaxis);
                }
            }
            else
            {
                outflowParticleSystem.Stop();
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
        }

        if (LOG) Debug.Log(nameof(liquidOffsetFromCenterOfRotation) + " = " + liquidOffsetFromCenterOfRotation);

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
        // This part is also pretty complicated. I'm sure it could be done better.       ...Why do I keep doing this at 2AM lol
        // Calculate liquid velocity
        float distanceCoveredByWaterOnTop = (liquidArea - ((Mathf.Tan((90f - angleFromYaxis) * Mathf.Deg2Rad) * Mathf.Pow(cylinderHeight, 2)) / 2)) / cylinderHeight;
        float distanceBetweenWaterLevels = Mathf.Cos((90f - angleFromYaxis) * Mathf.Deg2Rad) * distanceCoveredByWaterOnTop;
        float liquidVelocity = Mathf.Sqrt(2 * gravitationalAcceleration * (distanceBetweenWaterLevels / 2));

        // Calculate cross section of flow
        float areaOfCircularSegmentAdjusted = GetAreaOfCircularSegment(angleFromYaxis, distanceCoveredByWaterOnTop);

        // Calculate volumetric flow rate
        float liquidAcceleration = 0, volumetricFlowRate = 0;

        /*
        bool isSpilling = angleFromYaxis >= 90 ? true : false;
        if (isSpilling)
        {
            liquidAcceleration = gravitationalAcceleration * Mathf.Sin((angleFromYaxis - 90) * Mathf.Deg2Rad);
            currentLiquidVelocity += liquidAcceleration * Time.deltaTime * frictionFactor;
            volumetricFlowRate = currentLiquidVelocity * baseArea;
        }
        else
        {
            volumetricFlowRate = liquidVelocity * areaOfCircularSegmentAdjusted;
            currentLiquidVelocity = liquidVelocity;
        }*/
        // This looks bad. I will just use the simple case

        volumetricFlowRate = liquidVelocity * areaOfCircularSegmentAdjusted;
        currentLiquidVelocity = liquidVelocity;

        // Transfer volumetric flow rate to the fill percentage
        float currentVolume = baseArea * liquidHeight;
        float amountOfLiquidLost = volumetricFlowRate * Time.deltaTime; // This is in m^3
        float percentageOfLiquidLost = scale(0f, baseArea * cylinderHeight, 0f, 1f, amountOfLiquidLost);

        // Now we finally set the current fill percentage
        if (SPILL)
        {
            fillPercentageCurrent = fillPercentageCurrent - percentageOfLiquidLost;

            configureParticleSystem(distanceCoveredByWaterOnTop, currentLiquidVelocity, amountOfLiquidLost, angleFromYaxis);
        }

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

    private void configureParticleSystem(float distanceCoveredByWaterOnTop, float currentLiquidVelocity, float amountOfLiquidLost, float angleFromYaxis)
    {
        //// Configure particle system offset rotation
        OffsetParticleSystemRotation(angleFromYaxis);

        // Set offset from center of base to accomodate for the change of liquid height
        float distanceWaterTopCenterFromTopCenter = cylinderRadius - distanceCoveredByWaterOnTop / 2;
        float distanceAdjusted = scale(0, cylinderRadius, 0, cylinderRadius + glassThickness, distanceWaterTopCenterFromTopCenter);
        particleContainerTransform.localPosition = new Vector3(0, 0, distanceAdjusted);

        if (LOG) Debug.Log(distanceCoveredByWaterOnTop);

        outflowParticleSystem.Play();
        var main = outflowParticleSystem.main;
        main.startSpeed = currentLiquidVelocity;
        main.startSize = distanceCoveredByWaterOnTop;
        main.startColor = currentColor;

        // Configure arrays of outflow particles
        if (position == 0) position = outflowArrayLimit; else position -= 1;
        liquidAmountsOut[position] = amountOfLiquidLost;
        timesOut[position] = Time.time;
        colorsOut[position] = currentColor;

        if (LOG_ARR)
        {
            printArray_Float(liquidAmountsOut);
            printArray_Float(timesOut);
            printArray_Color(colorsOut);
        }
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

        currentRotationWorld = parentTransform.eulerAngles;

        // velocity
        Vector3 velocity = (lastPos - currentPositionWorld) / Time.deltaTime;
        Vector3 angularVelocity = currentRotationWorld - lastRot;


        // add clamped velocity to wobble
        wobbleAmountToAddX += Mathf.Clamp((velocity.x + (angularVelocity.z * 0.2f)) * MaxWobble, -MaxWobble, MaxWobble);
        wobbleAmountToAddZ += Mathf.Clamp((velocity.z + (angularVelocity.x * 0.2f)) * MaxWobble, -MaxWobble, MaxWobble);

        // keep last position
        lastPos = currentPositionWorld;
        lastRot = currentRotationWorld;

        return new Vector3(wobbleAmountX, 0, wobbleAmountZ);
    }

    private void printArray_Float(float[] array)
    {
        // A simple method that prints an array in one line
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

    private float[] SortFloatArray(float[] array)
    {
        // This is a very baisc sorting algorithm, but the array is usually less than 3 elements long so it should be fine
        // It's called insertion sort btw

        float swap;
        for (int i = 0; i < array.Length - 1; i++)
        {
            if (array[i] < array[i + 1])
            {
                continue;
            }
            else
            {
                swap = array[i];
                array[i] = array[i + 1];
                array[i + 1] = swap;

                for (int j = i; j >= 1; j--)
                {
                    if (array[j] > array[j - 1])
                    {
                        break;
                    }
                    else
                    {
                        swap = array[j];
                        array[j] = array[j - 1];
                        array[j - 1] = swap;
                    }
                }
            }
        }
        return array;
    }




    private Color MixColorsBest(Color color1, Color color2, float mix)
    {
        // Save on resources
        if (color1 == color2) return color1;

        float GAMMA = 0.43f;

        Color color1_lin = from_sRGB(color1);
        float bright1 = Mathf.Pow(color1_lin.r + color1_lin.g + color1_lin.b, GAMMA);
        Color color2_lin = from_sRGB(color2);
        float bright2 = Mathf.Pow(color2_lin.r + color2_lin.g + color2_lin.b, GAMMA);

        float intensity = Mathf.Pow(Mathf.Lerp(bright1, bright2, mix), 1 / GAMMA);
        Color finalColor = LerpColor(color1_lin, color2_lin, mix);
        float total = finalColor.r + finalColor.g + finalColor.b;

        if (total != 0)
        {
            finalColor = finalColor * intensity / total;
        }

        finalColor = to_sRGB(finalColor);
        finalColor.a = 1;

        return finalColor;
    }

    private Color LerpColor(Color color1, Color color2, float t)
    {
        float r = Mathf.Lerp(color1.r, color2.r, t);
        float g = Mathf.Lerp(color1.g, color2.g, t);
        float b = Mathf.Lerp(color1.b, color2.b, t);

        return new Color(r, g, b, 1);
    }

    private Color from_sRGB(Color color)
    {
        color.r = (color.r <= 0.04045) ? color.r / 11.92f : Mathf.Pow((color.r + 0.055f) / 1.055f, 2.4f);
        color.g = (color.g <= 0.04045) ? color.g / 11.92f : Mathf.Pow((color.g + 0.055f) / 1.055f, 2.4f);
        color.b = (color.b <= 0.04045) ? color.b / 11.92f : Mathf.Pow((color.b + 0.055f) / 1.055f, 2.4f);
        return color;
    }

    private Color to_sRGB(Color color)
    {
        color.r = (color.r <= 0.0031308) ? color.r * 12.92f : (1.055f * Mathf.Pow(color.r, 1 / 2.4f)) - 0.055f;
        color.g = (color.g <= 0.0031308) ? color.g * 12.92f : (1.055f * Mathf.Pow(color.g, 1 / 2.4f)) - 0.055f;
        color.b = (color.b <= 0.0031308) ? color.b * 12.92f : (1.055f * Mathf.Pow(color.b, 1 / 2.4f)) - 0.055f;
        return color;
    }
}
