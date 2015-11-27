using UnityEngine;
using System.Collections;
using System.Net;
using Steamworks;
using System.Collections.Generic;

namespace SGMulti
{

    /// <summary>
    /// An empty interface that allows casting to GServer and GClient.
    /// </summary>
    public interface ISteamGame { }

	public static class GServerDelegate{
		public delegate void DataRecieved(CSteamID sender, Packet packet, int channel);
		public delegate void PlayerConnect(CSteamID remoteSteamID);
		public delegate void PlayerDisconnect(CSteamID remoteSteamID, EP2PSessionError disconnectReason);
		
		public delegate void PlayerAuthenticated(CSteamID remoteSteamID);
        public delegate void ServerShutdown();
        public delegate void UpdateServerDetails();
    }
	
	public enum EPlayerStatus : int{
		Unknown = -1,
		Pending = 0,
		InServer = 1
	}
	
	public enum EGSConnectionError : int{
		Unknown = -1,
		AuthenticationError = 0,
		ServerFull = 1
	}

    // TODO: Allow multiple channels.
    public class GServer : ISteamGame
    {

		#region Events
		
		public event GServerDelegate.DataRecieved DataRecieved;
		public event GServerDelegate.PlayerConnect PlayerConnect;
		public event GServerDelegate.PlayerAuthenticated PlayerAuthenticated;
		public event GServerDelegate.PlayerDisconnect PlayerDisconnect;
        public event GServerDelegate.ServerShutdown ServerShutdown;
        public event GServerDelegate.UpdateServerDetails UpdateServerDetails;

        #endregion

        private List<int> _ExtraChannels = new List<int>();

        public void AddChannelToCheck(int channel)
        {
            _ExtraChannels.Add(channel);
        }

        public int[] GetChannels()
        {
            return _ExtraChannels.ToArray();
        }

        public void Instantiate(string str, Vector3 position)
        {
            Packet packet = CreatePacket(Packet.PACKET_UNITYINSTANTIATE);
            packet.Write(str);
            packet.Write(position);
            packet.Write(Quaternion.identity);
            packet.Send(EP2PSend.k_EP2PSendReliable, 0); // Should we have a dedicated channel for Unity Instantiation.
        }

        public void Instantiate(string str, Vector3 position, Quaternion rotation)
        {
            Packet packet = CreatePacket(Packet.PACKET_UNITYINSTANTIATE);
            packet.Write(str);
            packet.Write(position);
            packet.Write(rotation);
            packet.Send(EP2PSend.k_EP2PSendReliable, 0);
        }

        public GServer(IPAddress unIP, ushort usSteamPort, ushort usGamePort, ushort usQueryPort, EServerMode eServerMode, string pchVersionString)
        {
            _IpAddress = unIP;
			_Players = new Dictionary<CSteamID, EPlayerStatus>();
			 
            m_CallbackSteamServersConnected = Callback<SteamServersConnected_t>.CreateGameServer(OnSteamServersConnected);
            m_CallbackSteamServersConnectFailure = Callback<SteamServerConnectFailure_t>.CreateGameServer(OnSteamServersConnectFailure);
            m_CallbackSteamServersDisconnected = Callback<SteamServersDisconnected_t>.CreateGameServer(OnSteamServersDisconnected);
            m_CallbackPolicyResponse = Callback<GSPolicyResponse_t>.CreateGameServer(OnPolicyResponse);

            m_CallbackGSAuthTicketResponse = Callback<ValidateAuthTicketResponse_t>.CreateGameServer(OnValidateAuthTicketResponse);
            m_CallbackP2PSessionRequest = Callback<P2PSessionRequest_t>.CreateGameServer(OnP2PSessionRequest);
            m_CallbackP2PSessionConnectFail = Callback<P2PSessionConnectFail_t>.CreateGameServer(OnP2PSessionConnectFail);

            _SteamPort = usSteamPort;
            _GamePort = usGamePort;
            _QueryPort = usQueryPort;
			_ServerMode = eServerMode;

            _Initalized = GameServer.Init(IPInteger, SteamPort, GamePort, QueryPort, ServerMode, VersionString);
            if (!_Initalized)
            {
                Debug.LogError("GameServer.Init failed and returned false.");
            }
        }

        public GServer(IPAddress unIP, ushort usSteamPort, ushort usGamePort, ushort usQueryPort, string pchVersionString)
            : this(unIP, usSteamPort, usGamePort, usQueryPort, EServerMode.eServerModeAuthenticationAndSecure, pchVersionString)
        { }

        #region Steam Callback Variables

        //
        // Various callback functions that Steam will call to let us know about events related to our
        // connection to the Steam servers for authentication purposes.
        //
        // Tells us when we have successfully connected to Steam
        protected Callback<SteamServersConnected_t> m_CallbackSteamServersConnected;

        // Tells us when there was a failure to connect to Steam
        protected Callback<SteamServerConnectFailure_t> m_CallbackSteamServersConnectFailure;

        // Tells us when we have been logged out of Steam
        protected Callback<SteamServersDisconnected_t> m_CallbackSteamServersDisconnected;

        // Tells us that Steam has set our security policy (VAC on or off)
        protected Callback<GSPolicyResponse_t> m_CallbackPolicyResponse;

        //
        // Various callback functions that Steam will call to let us know about whether we should
        // allow clients to play or we should kick/deny them.
        //
        // Tells us a client has been authenticated and approved to play by Steam (passes auth, license check, VAC status, etc...)
        protected Callback<ValidateAuthTicketResponse_t> m_CallbackGSAuthTicketResponse;

        // client connection state
        protected Callback<P2PSessionRequest_t> m_CallbackP2PSessionRequest;
        protected Callback<P2PSessionConnectFail_t> m_CallbackP2PSessionConnectFail;

        #endregion

        #region Steam Callback Functions

        /// <summary>
        /// From Spacewar:
        /// Take any action we need to on Steam notifying us we are now logged in
        /// </summary>
        /// <param name="callback"></param>
        void OnSteamServersConnected(SteamServersConnected_t callback)
        {
            Debug.Log("Connected to Steam Servers!");
            _SteamConnected = true;

            if (UpdateServerDetails != null)
                UpdateServerDetails();
        }

        /// <summary>
        /// From Spacewar:
        /// Called when we were previously logged into steam but get logged out
        /// </summary>
        /// <param name="callback"></param>
        void OnSteamServersDisconnected(SteamServersDisconnected_t callback)
        {
            _SteamConnected = false;
        }

        void OnP2PSessionRequest(P2PSessionRequest_t callback)
        {
            // For now always allow connections, without a kick for being banned.

            Debug.Log("Hey we got someone!");

            if (SteamGameServerNetworking.AcceptP2PSessionWithUser(callback.m_steamIDRemote))
            {
                _Players.Add(callback.m_steamIDRemote, EPlayerStatus.Pending);

                // Now we need a ticket validation response from the client.
                // Have the Client Call SteamUser.RequestEncryptedAppTicket()
				
				if(PlayerConnect != null)
                    PlayerConnect(callback.m_steamIDRemote); // Calls even if the player is not verified.
            }
        }

        void OnP2PSessionConnectFail(P2PSessionConnectFail_t callback)
        {
			_Players.Remove(callback.m_steamIDRemote);
			if(PlayerDisconnect != null)
				PlayerDisconnect(callback.m_steamIDRemote, (EP2PSessionError)callback.m_eP2PSessionError);
        }

        /// <summary>
        /// From Spacewar:
        /// Tells us Steam3 (VAC and newer license checking) has accepted the user connection
        /// </summary>
        /// <param name="callback"></param>
        void OnValidateAuthTicketResponse(ValidateAuthTicketResponse_t callback)
        {
            EAuthSessionResponse response = callback.m_eAuthSessionResponse;
            if (response == EAuthSessionResponse.k_EAuthSessionResponseOK){
                _Players[callback.m_SteamID] = EPlayerStatus.InServer;
                if (PlayerAuthenticated != null)
                    PlayerAuthenticated(callback.m_SteamID);
			}else{
                // Auth ticket failed to validate, kick the player.
                SteamGameServer.EndAuthSession(callback.m_SteamID);

                SendFailedConnection(callback.m_SteamID, EGSConnectionError.AuthenticationError);
                _Players.Remove(callback.m_SteamID);
            }
        }

        /// <summary>
        /// From Spacewar:
        /// Callback from Steam when logon is fully completed and VAC secure policy is set
        /// </summary>
        /// <param name="callback"></param>
        void OnPolicyResponse(GSPolicyResponse_t callback)
        {
            Debug.LogFormat("Steam logged in successfully! Vac Secure: {0}", VACSecure);
        }

        /// <summary>
        /// From Spacewar:
        /// Called when an attempt to login to Steam fails.
        /// </summary>
        /// <param name="callback"></param>
        void OnSteamServersConnectFailure(SteamServerConnectFailure_t callback)
        {
            _SteamConnected = false;

            Debug.LogWarning("We were unable to connect to Steams' Servers");
        }

        #endregion

        #region Properties

        private bool _Dedicated, _Heartbeat = false;
        private string _ProductID, _ServerName, _MapName, _GameDescription, _ModDir = "";
        private int _MaxPlayers = 32;
        private int _HeartbeatInterval = -1;
        private int _BotCount = 0;

        private bool _Initalized = false;
        private bool _SteamConnected = false;

        private IPAddress _IpAddress;

        private ushort _SteamPort, _GamePort, _QueryPort;
        private EServerMode _ServerMode;
        private string _VersionString;

        public string GameDescription
        {
            get { return _GameDescription; }
            set
            {
                _GameDescription = value;
                SteamGameServer.SetGameDescription(_GameDescription);
            }
        }

        public string ModDir
        {
            get { return _ModDir; }
            set
            {
                _ModDir = value;
                SteamGameServer.SetModDir(_ModDir);
            }
        }

        public uint IPInteger
        {
            get
            {
                return _IpAddress.ToUInteger();
            }
        }

        public bool Initalized
        {
            get { return _Initalized; }
        }

        public IPAddress IP
        {
            get { return _IpAddress; }
            set { _IpAddress = value; }
        }

        public string VersionString
        {
            get { return _VersionString; }
            set { _VersionString = value; }
        }

        public EServerMode ServerMode
        {
            get { return _ServerMode; }
            set { _ServerMode = value; }
        }

        public ushort SteamPort
        {
            get { return _SteamPort; }
            set { _SteamPort = value; }
        }

        public ushort GamePort
        {
            get { return _GamePort; }
            set { _GamePort = value; }
        }

        public ushort QueryPort
        {
            get { return _QueryPort; }
            set { _QueryPort = value; }
        }

        public bool SteamConnected
        {
            get { return _SteamConnected; }
        }

        public bool VACSecure
        {
            get { return SteamGameServer.BSecure(); }
        }

        private Dictionary<CSteamID, EPlayerStatus> _Players;

        public bool Dedicated
        {
            get { return _Dedicated; }
            set
            {
                _Dedicated = value;
                SteamGameServer.SetDedicatedServer(_Dedicated);
            }
        }

        public string ProductID
        {
            get { return _ProductID; }
            set
            {
                _ProductID = value;
                SteamGameServer.SetProduct(_ProductID);
            }
        }

        public int MaxPlayers
        {
            get { return _MaxPlayers; }
            set
            {
                _MaxPlayers = value;
                SteamGameServer.SetMaxPlayerCount(_MaxPlayers);
            }
        }

        public string Name
        {
            get { return _ServerName; }
            set
            {
                _ServerName = value;
                SteamGameServer.SetServerName(_ServerName);
            }
        }

        public int Bots
        {
            get { return _BotCount; }
            set
            {
                _BotCount = value;
                SteamGameServer.SetBotPlayerCount(_BotCount);
            }
        }

        public string Map
        {
            get { return _MapName; }
            set
            {
                _MapName = value;
                SteamGameServer.SetMapName(_MapName);
            }
        }

        public CSteamID SteamID
        {
            get { return SteamGameServer.GetSteamID(); }
        }

        public bool IsRestartRequired()
        {
            return SteamGameServer.WasRestartRequested();
        }

        public uint PublicIP
        {
            get { return SteamGameServer.GetPublicIP(); }
        }

        public bool AllowHeartbeat
        {
            get { return _Heartbeat; }
            set
            {
                _Heartbeat = value;
                SteamGameServer.EnableHeartbeats(_Heartbeat);
            }
        }

        public int HeartbeatInterval
        {
            get { return _HeartbeatInterval; }
            set
            {
                _HeartbeatInterval = value;
                SteamGameServer.SetHeartbeatInterval(_HeartbeatInterval);
            }
        }

        public Dictionary<CSteamID, EPlayerStatus> Players
        {
            get
            {
                Dictionary<CSteamID, EPlayerStatus> statuses = new Dictionary<CSteamID, EPlayerStatus>();
                foreach(KeyValuePair<CSteamID, EPlayerStatus> player in _Players)
                {
                    statuses.Add(player.Key, player.Value);
                }
                return statuses;
            }
        }

        /// <summary>
        /// All players that are connected.
        /// </summary>
        public CSteamID[] ConnectedPlayers
        {
            get
            {
                CSteamID[] players = new CSteamID[_Players.Count];
                _Players.Keys.CopyTo(players, 0);
                return players;
            }
        }

        #endregion

        public string GetPlayerName(CSteamID steamID)
        {
            try
            {
                return _UserNames[steamID];
            }
            catch { return ""; }
        }

        void RemovePlayerFromServer(CSteamID id)
        {
            if (PlayerDisconnect != null)
                PlayerDisconnect(id, EP2PSessionError.k_EP2PSessionErrorNone);

            _Players.Remove(id);

            SteamGameServer.EndAuthSession(id);
        }

        public void ForceHeartbeat()
        {
            SteamGameServer.ForceHeartbeat();
        }

        public CSteamID CreateFakePlayer()
        {
            return SteamGameServer.CreateUnauthenticatedUserConnection();
        }

        public void Shutdown()
        {
            if(AllowHeartbeat)
                AllowHeartbeat = false;

            if (ServerShutdown != null)
                ServerShutdown();

            SteamGameServer.LogOff();

            GameServer.Shutdown();
        }

        /// <summary>
        /// Call this after ModDir, ProductID, GameDescription are set.
        /// </summary>
        /// <returns></returns>
        public void LogOnAnonymous()
        {
            SteamGameServer.LogOnAnonymous();
        }

        public void LogOff()
        {
            SteamGameServer.LogOff();
        }

        // Packet related method

        public Packet CreatePacket(long packetType = 0x000000)
        {
            return new Packet(this, true, packetType);
        }

        private Dictionary<CSteamID, string> _UserNames = new Dictionary<CSteamID, string>();

		public void Update()
		{
            if (!Initalized)
                return;

            GameServer.RunCallbacks();

            if (SteamConnected)
            {
                if (UpdateServerDetails != null)
                    UpdateServerDetails();
            }

			byte[] recvBuffer;
			
			uint msgSize;
			CSteamID steamIDRemote;
			
			while(SteamGameServerNetworking.IsP2PPacketAvailable(out msgSize)){
				recvBuffer = new byte[msgSize];
				
				if(!SteamGameServerNetworking.ReadP2PPacket( recvBuffer, msgSize, out msgSize, out steamIDRemote ))
					break;
				
				Packet packet = new Packet(this, false);
                packet.Bytes = recvBuffer;
				
				try{
                    if (packet.PacketID == Packet.PACKET_DOAUTH)
                    {
                        // TODO: Cause server to handle Steam Authentication.
                        int byteCount = packet.ReadInteger();
                        OnClientWantsAuth(steamIDRemote, packet.ReadByteArray(byteCount), byteCount);
                    }
                    else if (packet.PacketID == Packet.PACKET_SESSIONINFO)
                    {
                        Packet msg = CreatePacket(Packet.PACKET_SESSIONINFO);
                        msg.Write(SteamGameServer.GetSteamID().m_SteamID);
                        msg.Write(SteamGameServer.BSecure());
                        msg.Write(Name);
                        msg.Send(EP2PSend.k_EP2PSendReliable, 0, steamIDRemote);
                    }
                    else if (packet.PacketID == Packet.PACKET_USERDISCONNECTED)
                    {
                        if (_Players.ContainsKey(steamIDRemote))
                        {
                            if(_Players[steamIDRemote] == EPlayerStatus.Pending)
                            {
                                SteamGameServer.SendUserDisconnect(steamIDRemote);
                                _Players.Remove(steamIDRemote);
                            }
                            else
                            {
                                RemovePlayerFromServer(steamIDRemote);
                            }
                        }
                        else
                        {
                            Debug.LogWarning("How the absoulte loving TomSka joke in a game did this happen? No matching client???");
                        }
                        
                    }
                    else if(packet.PacketID == Packet.PACKET_INTRODUCTION)
                    {
                        string connectionString = packet.ReadString();
                        _UserNames.Add(steamIDRemote, packet.ReadString());

                        Packet msg = CreatePacket(Packet.PACKET_INTRODUCTION);

                        msg.Write(connectionString);

                        msg.Send(EP2PSend.k_EP2PSendReliable, 0, steamIDRemote);
                        Debug.LogFormat("Sent PACKET_INTRODUCTION with connection string of \"{0}\" to {1}.", connectionString, steamIDRemote);
                    }
                }
                finally{
					packet.Seek();
				}

                if (DataRecieved != null)
                    DataRecieved(steamIDRemote, packet, 0);

                packet.Dispose();
			}

            foreach(int channel in _ExtraChannels)
            {
                if (channel == 0) // We Handle it up above.
                    continue;
                while (SteamGameServerNetworking.IsP2PPacketAvailable(out msgSize, channel))
                {
                    recvBuffer = new byte[msgSize];

                    if (!SteamGameServerNetworking.ReadP2PPacket(recvBuffer, msgSize, out msgSize, out steamIDRemote))
                        break;

                    Packet packet = new Packet(this, false);
                    packet.Bytes = recvBuffer;

                    if (DataRecieved != null)
                        DataRecieved(steamIDRemote, packet, channel);

                    packet.Dispose();
                }
            }

        }

        /// <summary>
        /// Kicks the player from the server.
        /// </summary>
        /// <param name="id">The player to kick via SteamID.</param>
        /// <returns>If we sent the kick packet.</returns>
        public bool Kick(CSteamID id, string reason = "")
        {
            if (!_Players.ContainsKey(id))
                return false;
            SendFailedConnection(id, EGSConnectionError.Unknown, reason);
            return true;
        }
		
		void SendFailedConnection(CSteamID recip, EGSConnectionError connectionError, string reason = ""){
			Packet packet = new Packet(this, true, Packet.PACKET_AUTHFAILED);
			
			packet.Write((int)connectionError);
            if(reason != string.Empty)
            {
                packet.Write(reason);
            }
			
			packet.Send(EP2PSend.k_EP2PSendReliable, 0, recip);
		}
		
		void OnClientWantsAuth(CSteamID steamIDRemote, byte[] authPub, int authCub)
		{
			if(_Players.ContainsKey(steamIDRemote)){
				// This got called again, why did it?
				return;
			}
			
			if(_Players.Count >= MaxPlayers){
				SendFailedConnection(steamIDRemote, EGSConnectionError.ServerFull);
			}

            if (SteamGameServer.BeginAuthSession(authPub, authCub, steamIDRemote) != EBeginAuthSessionResult.k_EBeginAuthSessionResultOK){
				SendFailedConnection(steamIDRemote, EGSConnectionError.AuthenticationError);
			}
			
			_Players.Add(steamIDRemote, EPlayerStatus.Pending);
			
			if(PlayerAuthenticated != null)
				PlayerAuthenticated(steamIDRemote);

            Packet msg = new Packet(this, true, Packet.PACKET_AUTHSUCCESS);
            msg.Send(EP2PSend.k_EP2PSendReliable, 0, steamIDRemote);
		}

    }
}
