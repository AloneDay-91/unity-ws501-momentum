using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class deplacJ1 : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        // Déplacement du joueur 1 avec les touches ZQSD
        if (Input.GetAxis("P1_Horizontal") > 0){
            transform.Translate(Vector3.right * Time.deltaTime * 5);
        }
        if (Input.GetAxis("P1_Horizontal") < 0){
            transform.Translate(Vector3.left * Time.deltaTime * 5);
        }
        if (Input.GetAxis("P1_Vertical") > 0){
            transform.Translate(Vector3.forward * Time.deltaTime * 5);
        }
        if (Input.GetAxis("P1_Vertical") < 0){
            transform.Translate(Vector3.back * Time.deltaTime * 5);
        }
    }
}
