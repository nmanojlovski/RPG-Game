using UnityEngine;
using UnityEngine.SceneManagement;
using Photon.Pun;
using Photon.Realtime;          // ? needed for DisconnectCause

public class ReturnButton : MonoBehaviourPunCallbacks   // ? changed to PunCallbacks
{
    public void ReturnToMenu()
    {
        SceneManager.LoadScene("MainMenu");
    }

    public void ReturnToLoading()
    {
        SceneManager.LoadScene("LoadingScene");
    }

    public void OnExitButtonPressed()
    {
        if (PhotonNetwork.IsConnected)
        {
            PhotonNetwork.Disconnect();
        }
        else
        {
            SceneManager.LoadScene("LoadingScene");
        }
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        SceneManager.LoadScene("LoadingScene");
    }
}