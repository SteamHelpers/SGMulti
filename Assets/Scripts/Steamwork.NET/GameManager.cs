using UnityEngine;
using System.Collections;
using SGMulti;
using Steamworks;
using System.Net;

[DisallowMultipleComponent]
public class GameManager : MonoBehaviour {

    ServerImplmentation gameServer;
    ConsoleManager cmdLineManager;

    SteamManager manager;
    GClient gameClient;

    System.Collections.Generic.List<gameserveritem_t> servers = new System.Collections.Generic.List<gameserveritem_t>();

	// Use this for initialization
	void Start () {
        DontDestroyOnLoad(gameObject);

        if (CommandLineManager.IsServer)
        {
            gameServer = gameObject.AddComponent<ServerImplmentation>();
            cmdLineManager = gameObject.AddComponent<ConsoleManager>();
        }
        else
        {
            manager = gameObject.AddComponent<SteamManager>();
            gameClient = new GClient();
            gameClient.DataRecieved += GameClient_DataRecieved;
            gameClient.ConnectionStatusChanged += GameClient_ConnectionStatusChanged;
            gameClient.ConnectingToServer += GameClient_ConnectingToServer;

            gameClient.MatchmakingListFoundServer += GameClient_MatchmakingListFoundServer;

            //request = gameClient.RefreshList(EServerList.LAN);
        }
	}

    private void GameClient_MatchmakingListFoundServer(HServerListRequest request, gameserveritem_t server)
    {
        servers.Add(server);
    }

    void RefreshServers()
    {
        servers.Clear();
        request = gameClient.RefreshList(EServerList.LAN);
    }

    private void GameClient_ConnectingToServer(GClient sender, CSteamID server)
    {
        Debug.LogFormat("We are ready for liftoff! Connecting to: {0}", server);
    }

    HServerQuery currentQuery;

    Vector2 startServerPosition = new Vector2(100, 20);
    int serverHeight = 30;

    bool drawServerList = true;

    HServerListRequest request = HServerListRequest.Invalid;

    bool waitForChange = false;

    System.Collections.Generic.List<Transform> GetChildrenOf(Transform trans)
    {
        System.Collections.Generic.List<Transform> transforms = new System.Collections.Generic.List<Transform>();

        for(int i = 0; i < trans.childCount; i++)
        {
            transforms.Add(trans.GetChild(i));
        }

        return transforms;
    }

    
    // Legacy GUI
    void OnGUI()
    {
        if(gameClient != null && drawServerList)
        {
            int count = 0;
            if (request != HServerListRequest.Invalid)
            {
                count = SteamMatchmakingServers.GetServerCount(gameClient.ServerListRequest);
                for (int i = 0; i < count; i++)
                {
                    gameserveritem_t server = SteamMatchmakingServers.GetServerDetails(gameClient.ServerListRequest, i);
                    GUI.Label(new Rect(startServerPosition + new Vector2(0, i * serverHeight), new Vector2(200, serverHeight)), server.GetServerName() + " " + (server.m_bSecure ? "(VAC Secure)" : "(Not VAC Secure)"));
                    if (GUI.Button(new Rect(startServerPosition + new Vector2(200, (i * (serverHeight)) / 2), new Vector2(50, 30)), "Join"))
                    {
                        drawServerList = false;
                        gameClient.ConnectTo(server.m_steamID);
                    }
                }
            }
            if (GUI.Button(new Rect(startServerPosition + new Vector2(100, (count * (serverHeight))), new Vector2(100, 30)), "Refresh LAN"))
            {
                request = gameClient.RefreshList(EServerList.LAN);
            }
        }
    }
    

    private void GameClient_ConnectionStatusChanged(GClient sender, CSteamID server, EConnectionStatus status)
    {
        Debug.LogFormat("Server: {0} Connection Status: {1}", server.m_SteamID, status);
    }

    private void GameClient_DataRecieved(GClient sender, Packet packet, int channel)
    {
        CSteamID steamIDSender = sender.ConnectedTo;
        if (packet.PacketID == 0x001122)
        {
            int randomNumber = packet.ReadInteger();
            Debug.LogFormat("Random Number from Server: {0}", randomNumber);
        }
        Debug.LogFormat("Got message type 0x{0}.", packet.PacketID.ToString("X4"));
    }

    void Update()
    {
        if(gameClient != null)
            gameClient.Update();
    }
	
}
