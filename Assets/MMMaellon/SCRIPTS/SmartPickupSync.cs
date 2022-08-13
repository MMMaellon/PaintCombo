
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class SmartPickupSync : UdonSharpBehaviour
{
    public VRC_Pickup pickup;
    public Rigidbody rigid;
    public float maxDistanceErr = 0.05f;
    public float maxRotationErr = 5f;
    public float syncInterval = 0.25f;
    public bool localTransforms = false;
    public bool force_gun_orientation_in_vr = true;
    public bool allow_theft_from_self = false;
    [System.NonSerialized, UdonSynced(UdonSyncMode.None)] public Vector3 velocity;
    [System.NonSerialized, UdonSynced(UdonSyncMode.None)] public Vector3 angularVelocity;
    [System.NonSerialized, UdonSynced(UdonSyncMode.None)] public Vector3 relativePos;
    [System.NonSerialized, UdonSynced(UdonSyncMode.None)] public Quaternion relativeRot;
    [System.NonSerialized, UdonSynced(UdonSyncMode.None)] public Vector3 pos;
    [System.NonSerialized, UdonSynced(UdonSyncMode.None)] public Quaternion rot;
    [System.NonSerialized, UdonSynced(UdonSyncMode.None), FieldChangeCallback(nameof(isHeld))] public bool _isHeld;
    [System.NonSerialized, UdonSynced(UdonSyncMode.None)] public bool rightHand;
    [System.NonSerialized] public float sinceLastRequest;
    [System.NonSerialized] public Vector3 posBuff;
    [System.NonSerialized] public Quaternion rotBuff;
    private bool start_ran = false;
    [System.NonSerialized] public Vector3 restPos;
    [System.NonSerialized] public Quaternion restRot;
    private int collisionCount = 0;

    [System.NonSerialized] public float predictionTimeInFuture;
    [System.NonSerialized] public Vector3 predictedPos;
    [System.NonSerialized] public Quaternion predictedRot;
    [System.NonSerialized] public Vector3 predictedVelocity;


    [System.NonSerialized] public Vector3 startPos;
    [System.NonSerialized] public Quaternion startRot;
    [System.NonSerialized] public Vector3 startVelocity;
    [System.NonSerialized] public Vector3 startAngularVelocity;

    public UdonSharpBehaviour[] linkedChildren;

    public bool isHeld
    {
        get => _isHeld;
        set
        {
            _isHeld = value;
            bool is_owner = Networking.LocalPlayer.IsOwner(gameObject);
            if (pickup != null && ((pickup.DisallowTheft && !is_owner) || (!allow_theft_from_self && is_owner)))
            {
                pickup.pickupable = !_isHeld;
            }
        }
    }
    void Start()
    {
        if (!localTransforms)
        {
            pos = GetTransform().position;
            rot = GetTransform().rotation;
        }
        else
        {
            pos = GetTransform().localPosition;
            rot = GetTransform().localRotation;
        }
        if (pickup != null && !force_gun_orientation_in_vr && Networking.LocalPlayer.IsUserInVR())
        {
            pickup.orientation = VRC_Pickup.PickupOrientation.Any;
        }
        restPos = pos;
        restRot = rot;
        start_ran = true;
        if (rigid == null && pickup != null)
        {
            rigid = pickup.GetComponent<Rigidbody>();
        }
    }

    Transform GetTransform()
    {
        if (pickup != null)
        {
            return pickup.transform;
        }
        return transform;
    }

    public override void OnDrop()
    {
        pickup.pickupable = true;
        foreach (UdonSharpBehaviour udon in linkedChildren)
        {
            if (udon != null)
            {
                udon.SendCustomEvent("_OnDrop");
            }
        }
        OnDrop_Delayed();
        SendCustomEventDelayedSeconds(nameof(OnDrop_Delayed), 0.5f, VRC.Udon.Common.Enums.EventTiming.Update);
    }

    public void OnDrop_Delayed()
    {
        Debug.LogWarning("On drop delayed");
        if (!isHeld) //don't sync the first one because velocity might incorrectly be set to 0 for some reason
        {
            RequestSerialization();
        }
        isHeld = false;
        relativePos = Vector3.zero;
    }

    public override void OnPickup()
    {
        Networking.SetOwner(Networking.LocalPlayer, gameObject);
        pickup.pickupable = false;

        foreach (UdonSharpBehaviour udon in linkedChildren)
        {
            if (udon != null)
            {
                udon.SendCustomEvent("_OnPickup");
            }
        }
        OnPickup_Delayed();
        SendCustomEventDelayedSeconds(nameof(OnPickup_Delayed), 0.25f, VRC.Udon.Common.Enums.EventTiming.Update);
    }

    public void OnPickup_Delayed()
    {
        isHeld = true;
        Vector3 leftBone = Networking.LocalPlayer.GetBonePosition(HumanBodyBones.LeftHand);
        Vector3 rightBone = Networking.LocalPlayer.GetBonePosition(HumanBodyBones.RightHand);

        if (leftBone == Vector3.zero)
        {
            rightHand = false;
            Quaternion inverseRot = Quaternion.Inverse(Networking.LocalPlayer.GetRotation());
            relativePos = inverseRot * (GetTransform().position - Networking.LocalPlayer.GetPosition());
            relativeRot = inverseRot * GetTransform().rotation;
        }
        else
        {
            if (pickup.currentHand == VRC_Pickup.PickupHand.None)
            {
                rightHand = Vector3.Distance(leftBone, GetTransform().position) >= Vector3.Distance(rightBone, GetTransform().position);
            }
            else
            {
                rightHand = pickup.currentHand == VRC_Pickup.PickupHand.Right;
            }

            if (rightHand)
            {
                Quaternion inverseRightBoneRot = Quaternion.Inverse(Networking.LocalPlayer.GetBoneRotation(HumanBodyBones.RightHand));
                relativePos = inverseRightBoneRot * (GetTransform().position - rightBone);
                relativeRot = inverseRightBoneRot * GetTransform().rotation;
            }
            else
            {
                Quaternion inverseLeftBoneRot = Quaternion.Inverse(Networking.LocalPlayer.GetBoneRotation(HumanBodyBones.LeftHand));
                relativePos = inverseLeftBoneRot * (GetTransform().position - leftBone);
                relativeRot = inverseLeftBoneRot * GetTransform().rotation;
            }
        }
        RequestSerialization();
    }

    public void MoveToSyncedTransform()
    {
        if (isHeld)
        {
            if (rightHand)
            {
                VRCPlayerApi player = Networking.GetOwner(gameObject);
                Quaternion rightBoneRot = player.GetBoneRotation(HumanBodyBones.RightHand);
                Vector3 rightBone = player.GetBonePosition(HumanBodyBones.RightHand);
                if (rightBone == Vector3.zero)
                {
                    GetTransform().SetPositionAndRotation(player.GetPosition() + (player.GetRotation() * relativePos), player.GetRotation() * relativeRot);
                }
                else
                {
                    GetTransform().SetPositionAndRotation(rightBone + (rightBoneRot * relativePos), rightBoneRot * relativeRot);
                }
            }
            else
            {
                VRCPlayerApi player = Networking.GetOwner(gameObject);
                Quaternion leftBoneRot = player.GetBoneRotation(HumanBodyBones.LeftHand);
                Vector3 leftBone = player.GetBonePosition(HumanBodyBones.LeftHand);
                if (leftBone == Vector3.zero)
                {
                    GetTransform().SetPositionAndRotation(player.GetPosition() + (player.GetRotation() * relativePos), player.GetRotation() * relativeRot);
                }
                else
                {
                    GetTransform().SetPositionAndRotation(leftBone + (leftBoneRot * relativePos), leftBoneRot * relativeRot);
                }
            }
        }
        else
        {
            if (rigid == null || rigid.isKinematic || Networking.LocalPlayer.IsOwner(gameObject) || (sinceLastRequest > syncInterval * 2f))
            {
                if (!localTransforms)
                {
                    GetTransform().SetPositionAndRotation(pos, rot);
                }
                else
                {
                    GetTransform().localPosition = pos;
                    GetTransform().localRotation = rot;
                    if (rigid != null)
                    {
                        rigid.Sleep();
                    }
                }
            }
            else
            {
                //lerp?
            }
        }
    }

    public void Update()
    {
        sinceLastRequest += Time.deltaTime;
        if (Networking.LocalPlayer != null)
        {
            if (Networking.LocalPlayer.IsOwner(gameObject))
            {
                if (isHeld)
                {
                    if (pickup != null && pickup.IsHeld)
                    {
                        if (rightHand)
                        {
                            Quaternion inverseRightBoneRot = Quaternion.Inverse(Networking.LocalPlayer.GetBoneRotation(HumanBodyBones.RightHand));
                            Vector3 rightBone = Networking.LocalPlayer.GetBonePosition(HumanBodyBones.RightHand);
                            posBuff = inverseRightBoneRot * (GetTransform().position - rightBone);
                            rotBuff = inverseRightBoneRot * GetTransform().rotation;
                        }
                        else
                        {
                            Quaternion inverseLeftBoneRot = Quaternion.Inverse(Networking.LocalPlayer.GetBoneRotation(HumanBodyBones.LeftHand));
                            Vector3 rightBone = Networking.LocalPlayer.GetBonePosition(HumanBodyBones.LeftHand);
                            posBuff = inverseLeftBoneRot * (GetTransform().position - rightBone);
                            rotBuff = inverseLeftBoneRot * GetTransform().rotation;
                        }

                        if ((Vector3.Distance(relativePos, posBuff) > maxDistanceErr || Quaternion.Angle(relativeRot, rotBuff) > maxRotationErr) && (sinceLastRequest > syncInterval))
                        {
                            sinceLastRequest = 0;
                            relativePos = posBuff;
                            relativeRot = rotBuff;
                            RequestSerialization();
                        }
                    }
                    else
                    {
                        OnDrop_Delayed();
                    }
                } else
                {
                    if (localTransforms)
                    {
                        posBuff = GetTransform().localPosition;
                        rotBuff = GetTransform().localRotation;
                    } else
                    {
                        posBuff = GetTransform().position;
                        rotBuff = GetTransform().rotation;
                    }
                    if ((collisionCount > 0 && (Vector3.Distance(pos, posBuff) > maxDistanceErr || Quaternion.Angle(rot, rotBuff) > maxRotationErr) || (rigid != null && ((Vector3.Distance(rigid.velocity, velocity) > maxDistanceErr) || Vector3.Distance(rigid.angularVelocity, angularVelocity) > maxRotationErr))) && (sinceLastRequest > syncInterval))
                    {
                        sinceLastRequest = 0;
                        pos = posBuff;
                        rot = rotBuff;
                        RequestSerialization();
                    }
                }
            }
            else
            {
                MoveToSyncedTransform();
            }

            
        }
    }

    public void Respawn()
    {
        if (Networking.LocalPlayer.IsOwner(gameObject))
        {
            if (pickup != null)
            {
                pickup.Drop();
            }
            isHeld = false;
            pos = restPos;
            rot = restRot;
            MoveToSyncedTransform();
            RequestSerialization();
        }
    }
    public override bool OnOwnershipRequest(VRCPlayerApi requestingPlayer, VRCPlayerApi requestedOwner)
    {
        if (pickup != null && isHeld)
        {
            if (pickup.DisallowTheft)
            {
                return false;
            } else if (requestedOwner != null && requestedOwner.isLocal)
            {
                pickup.Drop();
            }
        }
        return true;
    }

    public override void OnOwnershipTransferred(VRCPlayerApi player)
    {
        if (!Networking.LocalPlayer.IsOwner(gameObject) && pickup != null)
        {
            pickup.Drop();
        }
        if (player != null && Utilities.IsValid(player) && player.isLocal)
        {
            foreach (UdonSharpBehaviour udon in linkedChildren)
            {
                if (udon != null)
                {
                    Networking.SetOwner(Networking.LocalPlayer, udon.gameObject);
                }
            }
        }
    }

    public override void OnPreSerialization()
    {
        if (localTransforms)
        {
            pos = GetTransform().localPosition;
            rot = GetTransform().localRotation;
        }
        else
        {
            pos = GetTransform().position;
            rot = GetTransform().rotation;
        }
        if (rigid != null && !rigid.isKinematic)
        {
            velocity = rigid.velocity;
            angularVelocity = rigid.angularVelocity;
        }
        sinceLastRequest = 0;
    }

    public override void OnDeserialization()
    {
        if (rigid != null && !rigid.isKinematic && !isHeld)
        {
            rigid.WakeUp();
            rigid.position = pos;
            rigid.rotation = rot;
            rigid.velocity = velocity;
            rigid.angularVelocity = angularVelocity;
            Debug.LogWarning("velocity.magnitude " + velocity.magnitude);
            Debug.LogWarning("angularVelocity.magnitude " + angularVelocity.magnitude);
        }
        sinceLastRequest = 0;
    }
    private void OnCollisionEnter(Collision collision)
    {
        collisionCount++;
        if (Networking.IsOwner(gameObject))
        {
            if (collision.rigidbody != null && collision.rigidbody.velocity.sqrMagnitude > rigid.velocity.sqrMagnitude)
            {
                Networking.SetOwner(Networking.LocalPlayer, collision.rigidbody.gameObject);
            }
            if (!isHeld)
            {
                RequestSerialization();
            }
        }
    }
    private void OnCollisionExit(Collision collision)
    {
        collisionCount--;
        if (!isHeld && Networking.IsOwner(gameObject))
        {
            RequestSerialization();
        }
    }

    //Here's how this is going to work
    //Owner plays the physics simulation like normal
    //only sync data when there's an event like OnCollisionEnter, OnCollisionExit, OnDrop, or the object falls alseep or wakes up.
    //Remote Clients create a prediction based on synced data and try their best to sync objects to the prediction
    //Predictions are generated when an update comes from the Owner
    //How far predictions look into the future depends on the y component of the velocity
    //If the object is falling it's going to hit the ground pretty soon, so we better sync up physics asap
    //If the object is flying up at a high velocity it'll probably be a while until it hits something so just let it fly and slowly converge with the prediction
    //We use bezier curves to interpolate to predictions
    //This means remote clients basically have a kinematic object on their hands until after we converge with the prediction. Then it becomes a normal physics object again
    //This also means we need to account for gravity in our predictions

    public float CalcConvergeTime()
    {
        if (Physics.gravity.y == 0) //if there's no gravity. We'll probably never hit this edge case
        {
            return 1f;
        }
        return Mathf.Max(0.25f, (velocity.y / -Physics.gravity.y));//this is the time until the apex of a parabolic arc. We set a min of 0.25f so there's always some lerping to smooth things out
    }

    public void CalcPredictedTransform(float timeInFuture)
    {
        predictedPos = pos + velocity * timeInFuture + Physics.gravity * Mathf.Pow(timeInFuture, 2) / 2;// current synced pos + (velocity * time) + effect of gravity
        predictedVelocity = velocity + Physics.gravity * timeInFuture;//current velocity + effect of gravity
        predictedRot = rot * Quaternion.SlerpUnclamped(Quaternion.identity, Quaternion.Euler(angularVelocity), timeInFuture); //current rotation + (angular velocity * time)
        //predicted angular velocity will match the synced value
    }

    public void ConvergeToPrediction()
    {
        if (rigid == null)
        {
            return;
        }
        if (sinceLastRequest < predictionTimeInFuture && predictionTimeInFuture > 0)
        {
            float predictionProgress = sinceLastRequest / predictionTimeInFuture;
            //we have not hit the prediction yet
            if (collisionCount > 0)
            //we are hitting something and can't move freely, let's just apply a force to push our object toward our prediction.
            //If the collision also took place on the Owner's side then we have an update coming soon to save us
            //If the collision only happened on our end then we will desync a little, but it'll snap back on the frame the prediction converging period ends
            {
                rigid.AddForce((predictedPos - rigid.position) / (1 - predictionProgress));
                rigid.AddRelativeTorque((Quaternion.Inverse(rigid.rotation) * Quaternion.Euler(predictedRot.x, predictedRot.y, predictedRot.z)).eulerAngles / (1 - predictionProgress));
            } else
            {
                rigid.position = BezierInterpolateVector(startPos, startVelocity, predictedPos, predictedVelocity, predictionProgress);
                rigid.rotation = BezierInterpolateQuaternion(startRot, startAngularVelocity, predictedRot, angularVelocity, predictionProgress);
            }
        } else if (sinceLastRequest - Time.deltaTime < predictionTimeInFuture)
        {
            //first frame after the prediction period
            rigid.position = predictedPos;
            rigid.rotation = predictedRot;
            rigid.velocity = predictedVelocity;
            rigid.angularVelocity = angularVelocity;
        } else
        {
            //we passed the prediction (and should have converged by now). Just wait for the next update and hope we don't stray too far in the meantime
            //let the physics engine take over
        }
    }

    public float BezierInterpolateFloat(float start, float startVel, float target, float targetVel, float progress)
    {//I'll be honest, I looked up equations for bezier interpolation and got confused and just made this up
        float startingInfluence = start + startVel * progress;//what would happen if you started at the starting value and just traveled at the starting velocity the whole time
        float targetInfluence = target - targetVel * (1 - progress);//what would happen if always traveled the target velocity but made sure to end on at the target
        return Mathf.Lerp(startingInfluence, targetInfluence, progress);//start has a lot of influence at the start and target has a lot of influence at the end. Blend the middle
    }

    //Same thing but with vectors
    public Vector3 BezierInterpolateVector(Vector3 start, Vector3 startVel, Vector3 target, Vector3 targetVel, float progress)
    {
        Vector3 startingInfluence = start + startVel * progress;
        Vector3 targetInfluence = target - targetVel * (1 - progress);
        return Vector3.Lerp(startingInfluence, targetInfluence, progress);
    }

    //Same thing but with Quaternions
    public Quaternion BezierInterpolateQuaternion(Quaternion start, Vector3 startVel, Quaternion target, Vector3 targetVel, float progress)
    {
        Quaternion startingInfluence = Quaternion.Slerp(start, start * Quaternion.Euler(startVel), progress);
        Quaternion targetInfluence = Quaternion.Slerp(target * Quaternion.Inverse(Quaternion.Euler(targetVel)), target, progress);
        return Quaternion.Slerp(startingInfluence, targetInfluence, progress);
    }
    public override void OnPickupUseDown()
    {

        foreach (UdonSharpBehaviour udon in linkedChildren)
        {
            if (udon != null)
            {
                udon.SendCustomEvent("_OnPickupUseDown");
            }
        }
    }

    public override void OnPickupUseUp()
    {
        foreach (UdonSharpBehaviour udon in linkedChildren)
        {
            if (udon != null)
            {
                udon.SendCustomEvent("_OnPickupUseUp");
            }
        }
    }
}
