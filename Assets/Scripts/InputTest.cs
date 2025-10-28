using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InputTest : MonoBehaviour
{
    [Header("Player 1")]
    [SerializeField] GameObject P1_Joystick;
    [SerializeField] GameObject P1_Start;
    [SerializeField] GameObject P1_B1;
    [SerializeField] GameObject P1_B2;
    [SerializeField] GameObject P1_B3;
    [SerializeField] GameObject P1_B4;
    [SerializeField] GameObject P1_B5;
    [SerializeField] GameObject P1_B6;
    [Space(10)]
    [Header("Player 2")]
    [SerializeField] GameObject P2_Joystick;
    [SerializeField] GameObject P2_Start;
    [SerializeField] GameObject P2_B1;
    [SerializeField] GameObject P2_B2;
    [SerializeField] GameObject P2_B3;
    [SerializeField] GameObject P2_B4;
    [SerializeField] GameObject P2_B5;
    [SerializeField] GameObject P2_B6;
    [Space(10)]
    [SerializeField] GameObject Reset;

    void Update()
    {
        P1_Joystick.transform.localPosition = new Vector3(Input.GetAxisRaw("P1_Horizontal")*100f, Input.GetAxisRaw("P1_Vertical")*100f, 0);
        P1_B1.SetActive(Input.GetButton("P1_B1"));
        P1_B2.SetActive(Input.GetButton("P1_B2"));
        P1_B3.SetActive(Input.GetButton("P1_B3"));
        P1_B4.SetActive(Input.GetButton("P1_B4"));
        P1_B5.SetActive(Input.GetButton("P1_B5"));
        P1_B6.SetActive(Input.GetButton("P1_B6"));
        P1_Start.SetActive(Input.GetButton("P1_Start"));

        P2_Joystick.transform.localPosition = new Vector3(Input.GetAxisRaw("P2_Horizontal")*100f, Input.GetAxisRaw("P2_Vertical")*100f, 0);
        P2_B1.SetActive(Input.GetButton("P2_B1"));
        P2_B2.SetActive(Input.GetButton("P2_B2"));
        P2_B3.SetActive(Input.GetButton("P2_B3"));
        P2_B4.SetActive(Input.GetButton("P2_B4"));
        P2_B5.SetActive(Input.GetButton("P2_B5"));
        P2_B6.SetActive(Input.GetButton("P2_B6"));
        P2_Start.SetActive(Input.GetButton("P2_Start"));

        Reset.SetActive(Input.GetButton("Coin"));
    }

}
