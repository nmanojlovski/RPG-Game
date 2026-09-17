using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.SceneManagement;

public class NetworkManager : MonoBehaviourPunCallbacks
{
    [Header("UI")]
    public TMP_Text playerCountText;

    [Header("Settings")]
    public int playerNum = 2;

    [Header("Winner UI")]
    public TMP_Text winnerText;


    public TMP_Text playerLeft;
    public TMP_Text statusText;  // Optional: shows "Waiting for players to ready..."
    public Button player1ReadyButton;
    public Button player2ReadyButton;
    public GameObject winnerPanel;

    public static bool GameStarted { get; private set; } = false;

    private bool canStart = false;
    private bool player1Ready = false;
    private bool player2Ready = false;

    void Start()
    {
        GameStarted = false;

        player1ReadyButton.interactable = false;
        player2ReadyButton.interactable = false;

        player1ReadyButton.gameObject.SetActive(false);
        player2ReadyButton.gameObject.SetActive(false);
    }


    void Update()
    {

        
        if (PhotonNetwork.InRoom && !GameStarted && !canStart)
        {
            int playerCount = PhotonNetwork.CurrentRoom.PlayerCount;
            playerCountText.text = "Players in room: " + playerCount;

            if (playerCount == playerNum)
            {
                if (player1Ready && !player2Ready)
                {
                    player1ReadyButton.gameObject.SetActive(false);
                    player2ReadyButton.gameObject.SetActive(true);
                }
                if (!player1Ready && player2Ready)
                {
                    player1ReadyButton.gameObject.SetActive(true);
                    player2ReadyButton.gameObject.SetActive(false);
                }
                if (player1Ready && player2Ready)
                {
                    player1ReadyButton.gameObject.SetActive(false);
                    player2ReadyButton.gameObject.SetActive(false);
                }
                if (!player1Ready && !player2Ready)
                {
                    player1ReadyButton.gameObject.SetActive(true);
                    player2ReadyButton.gameObject.SetActive(true);
                }


                if (PhotonNetwork.LocalPlayer.ActorNumber == 1)
                {
                    player1ReadyButton.interactable = true;
                }
                else if (PhotonNetwork.LocalPlayer.ActorNumber == 2)
                {
                    player2ReadyButton.interactable = true;
                }
            }


            if (playerCount == playerNum && !canStart && !GameStarted && player1Ready && player2Ready)
            {
                
                canStart = true;
                playerCountText.text = " ";
                StartCoroutine(StartCountdown());

                player1ReadyButton.gameObject.SetActive(false);
                player2ReadyButton.gameObject.SetActive(false);
            }
        }

        UpdateStatusText();
        CheckIfPlayerLeft();
    }


    // ========== BUTTON FUNCTIONS (call these from the Buttons) ==========

    public void OnPlayer1ReadyPressed()
    {
        // Only Player 1 is allowed to press this
        if (PhotonNetwork.LocalPlayer.ActorNumber != 1) return;

        photonView.RPC(nameof(RPC_Player1Ready), RpcTarget.All);

        Debug.Log("Pricnato");

        player1ReadyButton.gameObject.SetActive(false);
    }

    public void OnPlayer2ReadyPressed()
    {
        // Only Player 2 is allowed to press this
        if (PhotonNetwork.LocalPlayer.ActorNumber != 2) return;

        photonView.RPC(nameof(RPC_Player2Ready), RpcTarget.All);

        Debug.Log("Pricnato");

        player2ReadyButton.gameObject.SetActive(false);
    }

    // ========== RPCs ==========

    [PunRPC]
    void RPC_Player1Ready()
    {
        player1Ready = true;
        player1ReadyButton.interactable = false;  // Disable after pressing
        UpdateStatusText();
    }

    [PunRPC]
    void RPC_Player2Ready()
    {
        player2Ready = true;
        player2ReadyButton.interactable = false;
        UpdateStatusText();
    }


    void UpdateStatusText()
    {
        int playerCount = PhotonNetwork.CurrentRoom.PlayerCount;

        if (statusText == null) return;

        if (playerCount < 2 && !GameStarted)
        {
            statusText.text = "Waiting for Player";
        }
        else if (!player1Ready && !player2Ready && !GameStarted)
            statusText.text = "Waiting for both players to ready...";
        else if (player1Ready && !player2Ready && !GameStarted)
            statusText.text = "Waiting for Player 2...";
        else if (!player1Ready && player2Ready && !GameStarted)
            statusText.text = "Waiting for Player 1...";
        else if (player1Ready && player2Ready && !GameStarted)
        {
            statusText.text = "Both players ready!";
        }
        else if(GameStarted && playerCount < 2)
        {
            statusText.text = " ";
        }
        else
        {
            statusText.text = " ";
        }
    }

    void CheckIfPlayerLeft()
    {
        if (PhotonNetwork.CurrentRoom == null) return;

        int playerCount = PhotonNetwork.CurrentRoom.PlayerCount;

        if (playerCount < 2 && GameStarted)
        {
            playerLeft.text = "Player has Left... Disconnecting in 5 seconds";
            StartCoroutine(DisconnectAfterDelay());
        }
    }

    public void OnPlayerDied(int deadActorNumber)
    {
        if (!GameStarted) return;

        int winnerActorNumber = (deadActorNumber == 1) ? 2 : 1;

        winnerPanel.gameObject.SetActive(true);
        winnerText.gameObject.SetActive(true);
        winnerText.text = $"Player {winnerActorNumber} Wins!";

        // Tell ALL clients to start the disconnect countdown
        photonView.RPC(nameof(RPC_EndGame), RpcTarget.All);
    }

    [PunRPC]
    void RPC_EndGame()
    {
        StartCoroutine(DisconnectAfterWin());
    }

    IEnumerator DisconnectAfterWin()
    {
        yield return new WaitForSecondsRealtime(5f);
        PhotonNetwork.Disconnect();
    }




    IEnumerator DisconnectAfterDelay()
    {
        yield return new WaitForSecondsRealtime(5f);
        PhotonNetwork.Disconnect();
    }





    IEnumerator StartCountdown()
    {
        for (int i = 3; i > 0; i--)
        {
            playerCountText.text = i.ToString();  
            yield return new WaitForSecondsRealtime(1f);
        }

        playerCountText.text = "GO!";
        yield return new WaitForSecondsRealtime(0.5f);

        playerCountText.text = "";


        GameStarted = true;
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        SceneManager.LoadScene("LoadingScene");
    }
}