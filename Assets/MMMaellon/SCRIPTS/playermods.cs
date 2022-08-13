
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class playermods : UdonSharpBehaviour
{
    void Start()
    {
        // Networking.LocalPlayer.SetRunSpeed(3);
        // Networking.LocalPlayer.SetRunSpeed(3);
        Networking.LocalPlayer.SetJumpImpulse(3);
    }
}
