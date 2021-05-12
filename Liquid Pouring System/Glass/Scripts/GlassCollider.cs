/*
    GlassCollider U# Script by Dastmann
    Part of Dastmann's liquid pouring system for VRChat

    Sends a custom event to the main Udon Behaviour
    
    This project's GitHub: https://github.com/Dastmann/UDON-Liquid-Pouring-for-VRChat
*/

using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class GlassCollider : UdonSharpBehaviour
{
    private int lastID;
    private ParticleSystem particleSys;

    public UdonBehaviour LiquidBehaviour;
    public ParticleSystem filterParticleSystem; // What system to ignore
    private bool LOG = false;

    void OnParticleCollision(GameObject other)
    {
        if (LOG) Debug.Log(other);

        // filter self collisions
        if (other == filterParticleSystem) return;
        if (LOG) Debug.Log("Collision detected");

        LiquidBehaviour.SetProgramVariable("other", other);
        LiquidBehaviour.SendCustomEvent("ParticleCollision");
    }
}
