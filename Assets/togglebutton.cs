
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class togglebutton : UdonSharpBehaviour
{
    public GameObject obj;
    void Start()
    {
        obj.SetActive(false);
    }

    public override void Interact()
    {
        obj.SetActive(!obj.activeSelf);
    }
}
