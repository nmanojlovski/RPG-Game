using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using TMPro;

public class CreateAndJoin : MonoBehaviourPunCallbacks
{

    public TMP_InputField input_Create;
    public TMP_InputField input_Join;

    public void CreateRoom()
    {
        //if (string.IsNullOrWhiteSpace(input_Create.text))
        //{
        //    warningUI.ShowWarning("Enter room name to create!", 2f);
        //    return;
        //}


       // if (playerCountDropdown.value == 0)
       // {
            PhotonNetwork.CreateRoom(
            input_Create.text,
            new RoomOptions() { MaxPlayers = 2 },
            TypedLobby.Default
            );
       // }
    }

    public void JoinRoom()
    {
        //if (string.IsNullOrWhiteSpace(input_Join.text))
        //{
        //    warningUI.ShowWarning("Enter room name to join!", 2f);
        //    return;
        //}

        PhotonNetwork.JoinRoom(input_Join.text);
    }

    public override void OnJoinedRoom()
    {
            PhotonNetwork.LoadLevel(2);   
    }

    public void JoinRoomInList(string RoomName)
    {
        PhotonNetwork.JoinRoom(RoomName);
    }

}
