using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using System.Linq;

public class RoomList : MonoBehaviourPunCallbacks
{
    public GameObject RoomPrefab;

    [Header("Settings")]
    public float refreshInterval = 3f;

    private GameObject[] AllRooms = new GameObject[0];
    private List<RoomInfo> cachedRoomList = new List<RoomInfo>();
    private float timer = 0f;

    void Update()
    {
        timer += Time.deltaTime;
        if (timer >= refreshInterval)
        {
            timer = 0f;
            RefreshRoomList();
        }
    }

    public void RefreshRoomList()
    {
        if (PhotonNetwork.InLobby)
            PhotonNetwork.LeaveLobby();
        else
            PhotonNetwork.JoinLobby();
    }

    public override void OnJoinedLobby()
    {
        Debug.Log("Rejoined Lobby");
    }

    public override void OnRoomListUpdate(List<RoomInfo> roomList)
    {
        cachedRoomList = roomList;
        UpdateRoomUI();
    }

    private void UpdateRoomUI()
    {

        foreach (GameObject room in AllRooms)
        {
            if (room != null) Destroy(room);
        }


        var sortedRooms = cachedRoomList
            .Where(room => room.IsOpen && room.IsVisible && room.PlayerCount >= 1)
            .OrderBy(room => room.MaxPlayers - room.PlayerCount)
            .ThenByDescending(room => room.PlayerCount)
            .ToList();

        AllRooms = new GameObject[sortedRooms.Count];

        for (int i = 0; i < sortedRooms.Count; i++)
        {
            RoomInfo room = sortedRooms[i];

            GameObject newRoomUI = Instantiate(RoomPrefab, Vector3.zero, Quaternion.identity,
                                    GameObject.Find("Content").transform);

            Room roomScript = newRoomUI.GetComponent<Room>();

            if (roomScript != null)
            {
                roomScript.Name.text = room.Name;
                roomScript.PlayerCountText.text = $"{room.PlayerCount} / {room.MaxPlayers}";
            }

            AllRooms[i] = newRoomUI;
        }
    }
}