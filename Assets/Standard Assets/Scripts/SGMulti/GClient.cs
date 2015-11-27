using UnityEngine;
using System.Collections;
using System.Net;
using Steamworks;
using System.Collections.Generic;

namespace SGMulti
{

    public static class IPAddressExtender
    {

        /// <summary>
        /// Converts a IPAddress to a Unsigned 32bit integer.
        /// </summary>
        /// <param name="addr">The address to convert.</param>
        /// <returns>The converted address.</returns>
        public static uint ToUInteger(this IPAddress addr)
        {
            byte[] ipBytes = addr.GetAddressBytes();
            uint ip = (uint)ipBytes[3] << 24;
            ip += (uint)ipBytes[2] << 16;
            ip += (uint)ipBytes[1] << 8;
            ip += (uint)ipBytes[0];
            return ip;
        }

    }

    public static class GClientDelegates
    {
        public delegate void MatchmakingServerResponded(gameserveritem_t gameServer);
        public delegate void MatchmakingServerFailedToRespond();
        public delegate void DataRecieved(GClient sender, Packet packet, int nChannel);
        public delegate void ConnectingToServer(GClient sender, CSteamID server);
        public delegate void ServerConnectionClosed(CSteamID server, EGSConnectionError status, string reason);

        public delegate void MatchmakingListFoundServer(HServerListRequest request, gameserveritem_t server);
        public delegate void MatchmakingListRefresh(HServerListRequest serverListRequest, EMatchMakingServerResponse response, gameserveritem_t[] servers, EServerList type);

        public delegate void ConnectionStatusChanged(GClient sender, CSteamID server, EConnectionStatus status);
	}

    public enum EConnectionStatus
    {
        Unknown = -1,
        Connecting = 0,
        Connected = 1,
        Failed = 2
    }

    public enum EServerList
    {
        Unknown = -1,
        Internet = 0,
        LAN = 1,
        Friends = 2,
        Favorites = 3,
        History = 4,
        Spectator = 5
    }

    // TODO: Allow multiple channels.
    public class GClient : ISteamGame
    {

        private CSteamID _ConnectedTo;
        private EConnectionStatus _ConnectionStatus;
        private HServerListRequest _ServerListRequest;

        #region Instance

        public static GClient Instance
        {
            get { return _Instance; }
        }

        private static GClient _Instance;

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

        public HServerListRequest ServerListRequest
        {
            get { return _ServerListRequest; }
        }

        public CSteamID ConnectedTo
        {
            get { return _ConnectedTo; }
            private set { _ConnectedTo = value; }
        }

        public EConnectionStatus ConnectionStatus
        {
            get { return _ConnectionStatus; }
            protected set
            {
                if (ConnectionStatusChanged != null)
                    ConnectionStatusChanged(this, ConnectedTo, value);

                _ConnectionStatus = value;
            }
        }

        protected Callback<P2PSessionRequest_t> m_CallbackP2PSessionRequest;
        protected Callback<P2PSessionConnectFail_t> m_CallbackP2PSessionConnectFail;

        private ISteamMatchmakingPingResponse matchmakingPingResponse;
        
        /// <summary>
        /// Called when Pinging a Server is successful.
        /// </summary>
        public event GClientDelegates.MatchmakingServerResponded MatchmakingServerResponded;
        /// <summary>
        /// Called when Pinging a Server failed.
        /// </summary>
        public event GClientDelegates.MatchmakingServerFailedToRespond MatchmakingServerFailedToRespond;
        /// <summary>
        /// Called when IsP2PPacketAvailable returns true.
        /// </summary>
        public event GClientDelegates.DataRecieved DataRecieved;
        /// <summary>
        /// Called when we connect to a server.
        /// </summary>
        public event GClientDelegates.ConnectingToServer ConnectingToServer;
        /// <summary>
        /// Called when the Connection Status Changed.
        /// </summary>
        public event GClientDelegates.ConnectionStatusChanged ConnectionStatusChanged;

        public event GClientDelegates.ServerConnectionClosed ServerConnectionClosed;

        public event GClientDelegates.MatchmakingListFoundServer MatchmakingListFoundServer;

        /// <summary>
        /// Called when a server list was refreshed.
        /// </summary>
        public event GClientDelegates.MatchmakingListRefresh MatchmakingListRefresh;

        private void MatchmakingPing_ServerResponded(gameserveritem_t gameServer)
        {
            Debug.Log("ServerResponded");
            if (MatchmakingServerResponded != null)
                MatchmakingServerResponded(gameServer);

            ConnectTo(gameServer.m_steamID);
        }

        private void MatchmakingPing_ServerFailedToRespond()
        {
            Debug.Log("ServerFailedToRespond");
            if(MatchmakingServerFailedToRespond != null)
                MatchmakingServerFailedToRespond();
        }

        void OnP2PSessionRequest(P2PSessionRequest_t request)
        {
            Debug.Log("OnP2PSessionRequest from "+request.m_steamIDRemote);
            SteamNetworking.AcceptP2PSessionWithUser(request.m_steamIDRemote);
        }

        void OnP2PSessionFailed(P2PSessionConnectFail_t callback)
        {
            if(callback.m_steamIDRemote == ConnectedTo)
            {
                ConnectionStatus = EConnectionStatus.Failed;
            }
        }

        HAuthTicket ticket = HAuthTicket.Invalid;

        public bool SubmittedTicket
        {
            get;
            protected set;
        }

        public void ServerExiting(EGSConnectionError error = EGSConnectionError.AuthenticationError, string reason = "")
        {
            ConnectionStatus = EConnectionStatus.Failed;
            if (ServerConnectionClosed != null)
                ServerConnectionClosed(ConnectedTo, error, reason);

            if (ticket != HAuthTicket.Invalid)
            {
                SteamUser.CancelAuthTicket(ticket);
                ticket = HAuthTicket.Invalid;
            }
            if (SubmittedTicket)
            {
                SteamUser.EndAuthSession(ConnectedTo);
                SubmittedTicket = false;
            }
            if (ConnectedTo.IsValid())
                SteamNetworking.CloseP2PSessionWithUser(ConnectedTo);
            _ConnectedTo = CSteamID.Nil;
        }

        void SteamMatchmakingServerListResponse_ServerResponded(HServerListRequest request, int serverId)
        {
            gameserveritem_t server = SteamMatchmakingServers.GetServerDetails(request, serverId);
            //ConnectTo(server.m_steamID);
            Debug.LogFormat("Found Server. Server Name: {0} Server Passworded: {1} Server Ping: {2}", server.GetServerName(), server.m_bPassword, server.m_nPing);
            if (MatchmakingListFoundServer != null)
                MatchmakingListFoundServer(request, server);
        }

        void SteamMatchmakingServerListResponse_OnRefreshComplete(HServerListRequest request, EMatchMakingServerResponse response, EServerList type)
        {
            Debug.Log("Test2");
            _ServerListRequest = request;
            Debug.LogFormat("Request: {0} Response: {1}", request, response);

            if (MatchmakingListRefresh != null)
            {
                gameserveritem_t[] servers = new gameserveritem_t[0];
                if (response != EMatchMakingServerResponse.eServerResponded)
                     servers = new gameserveritem_t[SteamMatchmakingServers.GetServerCount(request)];

                MatchmakingListRefresh(request, response, servers, type);
            }
        }

        void SteamMatchmakingServerListResponse_ServerFailedToResponded(HServerListRequest request, int serverId)
        {
            try
            {
                gameserveritem_t server = SteamMatchmakingServers.GetServerDetails(request, serverId);
                Debug.LogFormat("Found Server. Server Name: {0} Server Passworded: {1} Server Ping: {2}", server.GetServerName(), server.m_bPassword, server.m_nPing);
            }
            catch(System.Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        public GClient()
        {
            m_CallbackP2PSessionRequest = Callback<P2PSessionRequest_t>.Create(OnP2PSessionRequest);
            
            matchmakingPingResponse = new ISteamMatchmakingPingResponse(MatchmakingPing_ServerResponded, MatchmakingPing_ServerFailedToRespond);

            m_CallbackP2PSessionConnectFail = Callback<P2PSessionConnectFail_t>.Create(OnP2PSessionFailed);

            if (_Instance != null)
                throw new System.InvalidOperationException("Instance was already set.");

            _Instance = this;
        }

        /// <summary>
        /// Uses <see cref="SteamMatchmakingServers.PingServer(uint, ushort, ISteamMatchmakingPingResponse)"/> then connects using <seealso cref="SGMulti.GClient.ConnectTo(CSteamID)"/>
        /// </summary>
        /// <param name="unIP"></param>
        /// <param name="usPort"></param>
        /// <returns>The query of PingServer</returns>
        public HServerQuery ConnectTo(uint unIP, ushort usPort)
        {
            return SteamMatchmakingServers.PingServer(unIP, usPort, matchmakingPingResponse);
        }

        public Packet CreatePacket(long packetId = 0x000000)
        {
            if (!ConnectedTo.IsValid())
                return new Packet(default(GClient), false);
            return new Packet(this, true, packetId);
        }

        byte[] RequestUserToken(out uint pcbTicket, out HAuthTicket ticketSess)
        {
            byte[] ticket = new byte[1024];
            ticketSess = SteamUser.GetAuthSessionTicket(ticket, 1024, out pcbTicket);
            return ticket;
        }

        public bool ServerAuthenticated
        {
            get;
            private set;
        }

        public void Update()
		{
			byte[] recvBuffer;
			
			uint msgSize;
			CSteamID steamIDRemote;
			
			while(SteamNetworking.IsP2PPacketAvailable(out msgSize)){
				recvBuffer = new byte[msgSize];
				
				if(!SteamNetworking.ReadP2PPacket( recvBuffer, msgSize, out msgSize, out steamIDRemote ))
					break;

                if (steamIDRemote != ConnectedTo)
                {

                    Packet packet = new Packet(this, false);
                    packet.Bytes = recvBuffer;

                    Debug.LogFormat("Recieved packet 0x{0} from Server.", packet.PacketID.ToString("X4"));

                    try
                    {
                        if (packet.PacketID == Packet.PACKET_AUTHFAILED)
                        {
                            ServerExiting();
                        }
                        else if(packet.PacketID == Packet.PACKET_USERKICKED)
                        {
                            string reason = "";
                            try
                            {
                                reason = packet.ReadString();
                            }
                            catch {  }
                            ServerExiting(EGSConnectionError.Unknown, reason);
                        }
                        else if(packet.PacketID == Packet.PACKET_INTRODUCTION)
                        {
                            if (packet.ReadString() == _GenerateString)
                            {
                                uint pcbTicket;
                                HAuthTicket ticketSess;

                                byte[] ticket = RequestUserToken(out pcbTicket, out ticketSess);
                                // Successfully introduced from server.
                                Packet msg = CreatePacket(Packet.PACKET_DOAUTH);
                                
                                msg.Write((int)pcbTicket);
                                msg.Write(ticket);

                                msg.Send(EP2PSend.k_EP2PSendReliable, 0);
                            }
                            else
                            {
                                // Failed to introduce.
                                ServerExiting();
                            }
                        }
                    }
                    finally
                    {
                        packet.Seek();
                    }

                    if (DataRecieved != null)
                        DataRecieved(this, packet, 0);

                    packet.Dispose();
                }
			}

            foreach (int channel in _ExtraChannels)
            {
                if (channel == 0) // We Handle it up above.
                    continue;
                while (SteamNetworking.IsP2PPacketAvailable(out msgSize, channel))
                {
                    recvBuffer = new byte[msgSize];

                    if (!SteamNetworking.ReadP2PPacket(recvBuffer, msgSize, out msgSize, out steamIDRemote))
                        break;

                    Packet packet = new Packet(this, false);
                    packet.Bytes = recvBuffer;

                    if (DataRecieved != null)
                        DataRecieved(this, packet, channel);

                    packet.Dispose();
                }
            }

            SteamAPI.RunCallbacks();
        }

        public HServerListRequest RefreshList(EServerList listType, ISteamMatchmakingServerListResponse response = null)
        {
            return RefreshList(listType, new MatchMakingKeyValuePair_t[] { }, response);
        }

        public HServerListRequest RefreshList(EServerList listType, MatchMakingKeyValuePair_t[] filters, ISteamMatchmakingServerListResponse response = null)
        {
            ISteamMatchmakingServerListResponse resp = new ISteamMatchmakingServerListResponse(SteamMatchmakingServerListResponse_ServerResponded, SteamMatchmakingServerListResponse_ServerFailedToResponded, (a, b) =>
            {
                SteamMatchmakingServerListResponse_OnRefreshComplete(a, b, listType);
            });
            if (response != null)
                resp = response;
            switch (listType)
            {
                case EServerList.Favorites:
                    return SteamMatchmakingServers.RequestFavoritesServerList(SteamUtils.GetAppID(), filters, (uint)filters.Length, resp);
                case EServerList.Friends:
                    return SteamMatchmakingServers.RequestFriendsServerList(SteamUtils.GetAppID(), filters, (uint)filters.Length, resp);
                case EServerList.History:
                    return SteamMatchmakingServers.RequestHistoryServerList(SteamUtils.GetAppID(), filters, (uint)filters.Length, resp);
                case EServerList.Internet:
                    return SteamMatchmakingServers.RequestInternetServerList(SteamUtils.GetAppID(), filters, (uint)filters.Length, resp);
                case EServerList.LAN:
                    return SteamMatchmakingServers.RequestLANServerList(SteamUtils.GetAppID(), resp);
                case EServerList.Spectator:
                    return SteamMatchmakingServers.RequestSpectatorServerList(SteamUtils.GetAppID(), filters, (uint)filters.Length, resp);
                default:
                    throw new System.Exception("\"listType\" must not be unknown.");
            }
        }

        private string _GenerateString;

        string GenerateConnectionString()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            _GenerateString = "";
            for(int i = 0; i < 5; i++)
            {
                _GenerateString += chars[Random.Range(0, chars.Length)];
            }
            return _GenerateString;
        }

        public void ConnectTo(CSteamID serverId)
        {
            if (ConnectingToServer != null)
                ConnectingToServer(this, serverId);

            ConnectedTo = serverId;
            ConnectionStatus = EConnectionStatus.Connecting;

            Packet introductionPacket = CreatePacket(Packet.PACKET_INTRODUCTION);
            introductionPacket.Write(GenerateConnectionString());
            introductionPacket.Write(SteamFriends.GetPersonaName());
            introductionPacket.Send(EP2PSend.k_EP2PSendReliable, 0);
        }


    }
}
