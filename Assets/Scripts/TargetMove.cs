using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TargetMove : MonoBehaviour
{

    public float speed;
    public bool moving = false;

    //public float arrivalRadius;
    //public float unitSize;
    //public float unitSpace;
    //public float allUnitsSpaces;
    //public float arrivalSpace;
    //public float separationRadius_min = 2;

    void Start()
    {
        //unitSize = 1;
        //unitSpace = (unitSize + separationRadius_min / 2) * (unitSize + separationRadius_min / 2);
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKey("w") || Input.GetKey("a") || Input.GetKey("s") || Input.GetKey("d"))
        {
            moving = true;
        }
        else
        {
            moving = false;
        }

        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        Vector3 direction = new Vector3(horizontal, 0, vertical);
        gameObject.transform.Translate(direction.normalized*Time.deltaTime*speed);
    }
}
