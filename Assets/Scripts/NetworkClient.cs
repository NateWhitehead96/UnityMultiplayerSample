using UnityEngine;
using Unity.Collections;
using Unity.Networking.Transport;
using NetworkMessages;
using NetworkObjects;
using System;
using System.Text;
using System.Collections.Generic;
using System.Collections;

public class NetworkClient : MonoBehaviour
{
    public NetworkDriver m_Driver;
    public NetworkConnection m_Connection;
    public string serverIP;
    public ushort serverPort;

    public GameObject cube;
    public GameObject spawnedCube = null;
    public List<NetworkObjects.NetworkPlayer> playerList;

    public string newID;
    void Start ()
    {
        m_Driver = NetworkDriver.Create();
        m_Connection = default(NetworkConnection);
        var endpoint = NetworkEndPoint.Parse(serverIP,serverPort);
        endpoint.Port = serverPort;
        m_Connection = m_Driver.Connect(endpoint);

        playerList = new List<NetworkObjects.NetworkPlayer>();
    }
    
    void SendToServer(string message){
        var writer = m_Driver.BeginSend(m_Connection);
        NativeArray<byte> bytes = new NativeArray<byte>(Encoding.ASCII.GetBytes(message),Allocator.Temp);
        writer.WriteBytes(bytes);
        m_Driver.EndSend(writer);
    }

    void OnConnect(){
        Debug.Log("We are now connected to the server");
        StartCoroutine(SendRepeatUpdate());
        // creating cube
        //// Example to send a handshake message:
        //HandshakeMsg m = new HandshakeMsg();
        //m.player.cubPos = new Vector3(UnityEngine.Random.Range(0, 5), UnityEngine.Random.Range(0, 5), UnityEngine.Random.Range(0, 5));
        //if (m.player.playerCube == null) // if the new connected player has no cube create one
        //{
        //    m.player.playerCube = Instantiate(cube);
        //    spawnedCube = m.player.playerCube;
        //    spawnedCube.transform.position = new Vector3(m.player.cubPos.x, m.player.cubPos.y, m.player.cubPos.z);
        //    Renderer renderer = m.player.playerCube.GetComponent<Renderer>();
        //    renderer.material.color = new Color(UnityEngine.Random.Range(0f, 1f), UnityEngine.Random.Range(0f, 1f), UnityEngine.Random.Range(0f, 1f));
        //    m.player.cubeColor = renderer.material.color;
        //}
        //playerList.Add(m.player);
        //SendToServer(JsonUtility.ToJson(m));
    }

    IEnumerator SendRepeatUpdate()
    {
        while(true)
        {
            yield return new WaitForSeconds(2);
            Debug.Log("Sending player update");
            PlayerUpdateMsg m = new PlayerUpdateMsg();
            //m.player.id = m_Connection.InternalId.ToString();
            m.player.id = newID;
            m.player.cubPos = playerList[0].playerCube.transform.position;
            m.player.playerCube = playerList[0].playerCube;
            Renderer renderer = playerList[0].playerCube.GetComponent<Renderer>();
            renderer.material.color = new Color(UnityEngine.Random.Range(0f, 1f), UnityEngine.Random.Range(0f, 1f), UnityEngine.Random.Range(0f, 1f));
            m.player.cubeColor = renderer.material.color;

            SendToServer(JsonUtility.ToJson(m));
        }
    }

    //IEnumerator SendInput()
    //{
    //    while(true)
    //    {
    //        yield return new WaitForSeconds(2);
    //        Debug.Log("Sending player input");
    //        PlayerInputMsg m = new PlayerInputMsg();
    //    }
    //}

    void OnData(DataStreamReader stream){
        NativeArray<byte> bytes = new NativeArray<byte>(stream.Length,Allocator.Temp);
        stream.ReadBytes(bytes);
        string recMsg = Encoding.ASCII.GetString(bytes.ToArray());
        NetworkHeader header = JsonUtility.FromJson<NetworkHeader>(recMsg);

        switch(header.cmd){
            case Commands.HANDSHAKE:
            HandshakeMsg hsMsg = JsonUtility.FromJson<HandshakeMsg>(recMsg);
                newID = hsMsg.player.id;
                hsMsg.player.playerCube = Instantiate(cube);
                playerList.Add(hsMsg.player);
            Debug.Log("Handshake message received!");
            break;
            case Commands.PLAYER_UPDATE:
            PlayerUpdateMsg puMsg = JsonUtility.FromJson<PlayerUpdateMsg>(recMsg);
            Debug.Log("Player update message received!");
            break;
            case Commands.SERVER_UPDATE:
            ServerUpdateMsg suMsg = JsonUtility.FromJson<ServerUpdateMsg>(recMsg);
                //sync cube pos to players here
                Debug.Log("Num clients connected: " + suMsg.players.Count);
                foreach(NetworkObjects.NetworkPlayer player in suMsg.players)
                {
                    if (player.id != newID) // if the player is me skip the update
                    {
                        int index = -1;
                        for (int i = 0; i < playerList.Count; i++)
                        {
                            if (playerList[i].id == player.id)
                            {
                                index = i;
                                playerList[index].cubPos = player.cubPos;
                                //playerList[index].playerCube.GetComponent<Renderer>().material.color = player.cubeColor;
                                playerList[index].playerCube.transform.position = player.cubPos;
                                playerList[index].cubeColor = player.cubeColor;
                            }
                        }

                        //int index = playerList.IndexOf(player);
                        Debug.Log("first Index num: " + index);
                        if (index == -1) // no player id in player list
                        {
                            index = playerList.Count;
                            NetworkObjects.NetworkPlayer temp = new NetworkObjects.NetworkPlayer();
                            temp.id = player.id;
                            temp.playerCube = Instantiate(cube);
                            temp.cubPos = player.cubPos;
                            temp.playerCube.GetComponent<Renderer>().material.color = player.cubeColor;
                            temp.playerCube.transform.position = player.cubPos;
                            temp.cubeColor = player.cubeColor;
                            playerList.Add(temp);
                            index = playerList.IndexOf(player);
                            Debug.Log("second Index num: " + index);

                            //playerList[index].playerCube = player.playerCube;
                        }

                    }
                    
                    // update the player cube
                    
                }

            Debug.Log("Server update message received!");
            break;
            default:
            Debug.Log("Unrecognized message received!");
            break;
        }
    }

    void Disconnect(){
        m_Connection.Disconnect(m_Driver);
        m_Connection = default(NetworkConnection);
    }

    void OnDisconnect(){
        Debug.Log("Client got disconnected from server");
        m_Connection = default(NetworkConnection);
    }

    public void OnDestroy()
    {
        m_Driver.Dispose();
    }   
    void Update()
    {
        m_Driver.ScheduleUpdate().Complete();

        if (!m_Connection.IsCreated)
        {
            return;
        }

        DataStreamReader stream;
        NetworkEvent.Type cmd;
        cmd = m_Connection.PopEvent(m_Driver, out stream);
        while (cmd != NetworkEvent.Type.Empty)
        {
            if (cmd == NetworkEvent.Type.Connect)
            {
                OnConnect();
            }
            else if (cmd == NetworkEvent.Type.Data)
            {
                OnData(stream);
            }
            else if (cmd == NetworkEvent.Type.Disconnect)
            {
                OnDisconnect();
            }

            cmd = m_Connection.PopEvent(m_Driver, out stream);
        }
        // my input
        if(Input.GetKey("w")) // moving in
        {
            playerList[0].playerCube.transform.Translate(new Vector3(0, 0, -.05f));
        }
        if (Input.GetKey("s")) // moving out
        {
            playerList[0].playerCube.transform.Translate(new Vector3(0, 0, .05f));
        }
        if (Input.GetKey("a")) // moving left
        {
            playerList[0].playerCube.transform.Translate(new Vector3(-0.05f, 0, 0));
        }
        if (Input.GetKey("d")) // moving right
        {
            playerList[0].playerCube.transform.Translate(new Vector3(0.05f, 0,0));
        }
    }
}