using UnityEngine;
using System.Collections;
using SGMulti;
using System.Net;
using Steamworks;

[DisallowMultipleComponent]
public class ServerImplmentation : MonoBehaviour {

    // UDP port for the spacewar server to do authentication on (ie, talk to Steam on)
    public const ushort SERVER_AUTHENTICATION_PORT = 8766;

    // UDP port for the spacewar server to listen on
    public const ushort SERVER_SERVER_PORT = 27015;

    // UDP port for the master server updater to listen on
    public const ushort SERVER_MASTER_SERVER_UPDATER_PORT = 27016;

    public const string SERVER_VERSIONNAME = "0.0.1_patchA";

    public GServer Server
    {
        get;
        private set;
    }

	// Use this for initialization
	void OnEnable() {
        Server = new GServer(IPAddress.Any, SERVER_AUTHENTICATION_PORT, SERVER_SERVER_PORT, SERVER_MASTER_SERVER_UPDATER_PORT, SERVER_VERSIONNAME);

        Server.UpdateServerDetails += Server_UpdateServerDetails;
        Server.DataRecieved += Server_DataRecieved;
        Server.PlayerAuthenticated += Server_PlayerAuthenticated;
        Server.PlayerConnect += Server_PlayerConnect;
        Server.PlayerDisconnect += Server_PlayerDisconnect;

        Server.ModDir = "sgmultitest";
        Server.ProductID = "SteamworksExample";
        Server.GameDescription = "Steamworks Example";

        Server.LogOnAnonymous();

        Server.AllowHeartbeat = true;
    }

    void OnDisable()
    {
        Server.Shutdown();
    }

    /// <summary>
    /// Gets called when a player disconnects, or has a problem connecting.
    /// </summary>
    /// <param name="remoteSteamID"></param>
    /// <param name="disconnectReason"></param>
    private void Server_PlayerDisconnect(CSteamID remoteSteamID, EP2PSessionError disconnectReason)
    {
        Debug.LogFormat("A player left. SteamID: {0}", remoteSteamID.m_SteamID);
        if (disconnectReason == EP2PSessionError.k_EP2PSessionErrorNone)
        {
            Debug.Log("The player left on his own accord.");
        }
        else
        {
            Debug.LogErrorFormat("An error occured while the player was connecting/connected. Reason: {0}", disconnectReason);
        }
    }

    /// <summary>
    /// Get's called when a player gets connected to our server fully.
    /// </summary>
    /// <param name="remoteSteamID"></param>
    private void Server_PlayerConnect(CSteamID remoteSteamID)
    {
        Debug.LogFormat("A player successfully connected with us. SteamID: {0}", remoteSteamID.m_SteamID);
        Packet packet = Server.CreatePacket(0x001122);
        packet.Write(Random.Range(15, 23));
        packet.Send(EP2PSend.k_EP2PSendReliable, 0, remoteSteamID);
    }

    /// <summary>
    /// Get's called when Steam authenticates a user that wants to connect.
    /// </summary>
    /// <param name="remoteSteamID"></param>
    private void Server_PlayerAuthenticated(CSteamID remoteSteamID)
    {
        Debug.LogFormat("A player was authed with steam. SteamID: {0}", remoteSteamID.m_SteamID);
    }

    /// <summary>
    /// Everytime data is recieved from anyone this gets called.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="packet"></param>
    /// <param name="nChannel"></param>
    private void Server_DataRecieved(CSteamID sender, Packet packet, int nChannel)
    {
        Debug.LogFormat("Got message type 0x{0}.", packet.PacketID.ToString("X4"));

    }

    /// <summary>
    /// Get's called once per frame, probably not a good idea.
    /// </summary>
    private void Server_UpdateServerDetails()
    {
        Server.Name = "TestServer";
        Server.MaxPlayers = 32;
        SteamGameServer.SetPasswordProtected(false); // I haven't implemented a password auth. It's simple to implement though.
        Server.Bots = 0;
        Server.Map = "Milky Way";

        foreach(CSteamID steamID in Server.Players.Keys)
        {
            SteamGameServer.BUpdateUserData(steamID, Server.GetPlayerName(steamID), 0);
        }
    }

    // Update is called once per frame
    void Update () {
        Server.Update();
	}
}
