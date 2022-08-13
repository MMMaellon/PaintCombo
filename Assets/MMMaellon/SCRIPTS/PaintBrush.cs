
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class PaintBrush : UdonSharpBehaviour
{
    [UdonSynced(UdonSyncMode.Smooth)] public Color _color = Color.clear;
    public MeshRenderer mesh;

    public sfx sfx_source;

    public Animator anim;
    public SmartPickupSync smartPickup;

    private float syncInterval = 0.25f;
    private float lastSync = -99f;
    private bool syncQueued = false;
    private Vector3 lastPos = Vector3.zero;
    private Quaternion lastRot = Quaternion.identity;
    private float lastSpeed = 0f;
    private float lastCalcSpeedTime = -99f;

    public Color color{
        get => _color;
        set{
            _color = value;
            if (value.a < 1)
            {
                mesh.enabled = false;
                if (Networking.LocalPlayer != null && Networking.LocalPlayer.IsOwner(gameObject))
                {
                    RequestSerialization();
                }
            } else
            {
                mesh.enabled = true;
                Sync();
            }
        }
    }

    void Start()
    {
        color = color;
    }

    public void Sync()
    {
        if (Networking.LocalPlayer.IsOwner(gameObject))
        {
            mesh.material.color = color;
            if (lastSync + syncInterval < Time.timeSinceLevelLoad)
            {
                RequestSerialization();
                lastSync = Time.timeSinceLevelLoad;
                syncQueued = false;
            }
            else if (!syncQueued)
            {
                SendCustomEventDelayedSeconds(nameof(Sync), syncInterval * 2);
                syncQueued = true;
            }
        }
        else
        {
            mesh.material.color = HSVBlend(mesh.material.color, color, 0.1f);
            if (mesh.material.color != color)
            {
                SendCustomEventDelayedFrames(nameof(Sync), 1);
            }
        }
    }

    public void OnTriggerStay(Collider other)
    {
        Collide(other);
    }

    public void OnCollisionStay(Collision collision)
    {
        Collide(collision.collider);
    }

    public void Collide(Collider other)
    {
        if (Networking.LocalPlayer == null || other == null || !Utilities.IsValid(other) || smartPickup == null || smartPickup.pickup == null || !smartPickup.pickup.IsHeld)
        {
            return;
        }
        PaintableObject paintable = other.GetComponent<PaintableObject>();
        MeshRenderer otherMesh = other.GetComponent<MeshRenderer>();
        if (paintable != null && (!paintable.complete || !paintable.colorAfterComplete))
        {
            if (color.a >= 1)
            {
                paintable.Splat(transform.position, color);
            }
        }
        else if (Networking.LocalPlayer.IsOwner(gameObject) && otherMesh != null && otherMesh.material != null && otherMesh.material.color != null)
        {
            if (color.a < 1 && otherMesh.material.color.a >= 1)
            {
                color = otherMesh.material.color;
                if (sfx_source != null)
                {
                    sfx_source.PlayClear(transform.position);
                }
            }
            else if (otherMesh.material.color.a < 1 && color.a >= 1 && color != Color.clear)
            {
                //maybe play a SFX to let them know the color got washed off
                color = Color.clear;
                if (sfx_source != null)
                {
                    sfx_source.PlayClear(transform.position);
                }
            }
            else if (otherMesh.material.color.a >= 1 && lastCalcSpeedTime + Time.deltaTime > Time.timeSinceLevelLoad - 0.001f)
            {
                color = HSVBlend(color, otherMesh.material.color, lastSpeed * 10);
                if (sfx_source != null)
                {
                    sfx_source.PlayStir(transform.position, lastSpeed * 100);
                }
            }
        }
        CalcSpeed();
    }

    public void CalcSpeed()
    {
        if (lastCalcSpeedTime + Time.deltaTime < Time.timeSinceLevelLoad - 0.001f)
        {
            lastSpeed = 0;
        } else
        {
            //means we calculated it last frame
            Vector3 velocity = mesh.transform.position - lastPos;
            lastSpeed = (Mathf.Lerp(lastSpeed, velocity.sqrMagnitude + Quaternion.Angle(lastRot, mesh.transform.rotation) / 360, 0.05f) / 250) / Time.deltaTime;
        }
        lastPos = mesh.transform.position;
        lastRot = mesh.transform.rotation;
        lastCalcSpeedTime = Time.timeSinceLevelLoad;
    }

    public Color HSVBlend(Color a, Color b, float amount)
    {float oldH;
        float oldS;
        float oldV;
        float newH;
        float newS;
        float newV;
        Color.RGBToHSV(a, out oldH, out oldS, out oldV);
        Color.RGBToHSV(b, out newH, out newS, out newV);
        //wrap the hue around
        if (newH < oldH)
        {
            if (Mathf.Abs(newH - oldH) > 0.5f)
            {
                newH += 1.0f;
            }
        }
        else
        {
            if (Mathf.Abs(newH - oldH) > 0.5f)
            {
                newH -= 1.0f;
            }
        }
        newH = Mathf.Lerp(oldH, newH, amount);
        newS = Mathf.Lerp(oldS, newS, amount);
        newV = Mathf.Lerp(oldV, newV, amount);
        //unwrap the hue
        if (newH < 0)
        {
            newH += 1.0f;
        }
        else if (newH > 1.0f)
        {
            newH -= 1.0f;
        }
        // return Color.Lerp(Color.HSVToRGB(newH, newS, newV), Color.Lerp(a, b, amount), 0.5f);
        return Color.HSVToRGB(newH, newS, newV);
    }

    public void _OnPickupUseDown()
    {
        anim.SetBool("flip", !Networking.LocalPlayer.IsUserInVR());
    }

    public void _OnPickupUseUp()
    {
        anim.SetBool("flip", false);
    }

    public override void OnDeserialization()
    {
        color = color;
    }
}
