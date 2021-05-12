/*
    leverSync U# Script by Dastmann
    Part of Dastmann's liquid pouring system for VRChat

    Makes a synced global toggle for a gameobject. Originally used for a tap lever, but can be used to toggle anything.

    Made using a tutorial from VowganVR (https://youtu.be/O3VeBzV9HgI)
*/

using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;

public class spoutSync : UdonSharpBehaviour
{
    public GameObject particleSystemToTrigger;

    public override void Interact()
    {
        SendCustomNetworkEvent(NetworkEventTarget.All, "ToggleTarget");
    }

    public override void OnPlayerJoined(VRCPlayerApi player)
    {
        if (Networking.IsMaster)
        {
            if (particleSystemToTrigger.activeSelf)
            {
                SendCustomNetworkEvent(NetworkEventTarget.All, "ToggleTargetTrue");
            }
            else
            {
                SendCustomNetworkEvent(NetworkEventTarget.All, "ToggleTargetFalse");
            }
        }
    }

    public void ToggleTarget()
    {
        particleSystemToTrigger.SetActive(!particleSystemToTrigger.activeSelf);
    }

    public void ToggleTargetTrue()
    {
        particleSystemToTrigger.SetActive(true);
    }

    public void ToggleTargetFalse()
    {
        particleSystemToTrigger.SetActive(false);
    }
}
