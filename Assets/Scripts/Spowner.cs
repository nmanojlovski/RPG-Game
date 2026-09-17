using UnityEngine;
using Photon.Pun;

public class Spowner : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        PhotonNetwork.Instantiate("Player", new Vector3(Random.Range(-7, +7), 1f, 0), Quaternion.identity);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
