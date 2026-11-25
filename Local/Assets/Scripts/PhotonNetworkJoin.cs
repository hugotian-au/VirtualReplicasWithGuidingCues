using UnityEngine;
using Photon.Realtime;
using Photon.Pun;
using Meta.XR.MRUtilityKit;

namespace Com.IVE.ECL.RemoteCollaboration
{

	public class PhotonNetworkJoin : MonoBehaviourPunCallbacks
	{
		#region Private Fields
		/// <summary>
		/// Keep track of the current process. Since connection is asynchronous and is based on several callbacks from Photon, 
		/// we need to keep track of this to properly adjust the behavior when we receive call back by Photon.
		/// Typically this is used for the OnConnectedToMaster() callback.
		/// </summary>
		bool isConnecting;

		private byte maxPlayersPerRoom = 4;

		/// <summary>
		/// This client's version number. Users are separated from each other by gameVersion (which allows you to make breaking changes).
		/// </summary>
		string gameVersion = "1";

		#endregion
		// Start is called once before the first execution of Update after the MonoBehaviour is created
		void Start()
		{
			if (MRUK.Instance == null)
			{
				Debug.LogError("MRUK Instance not found.");
				return;
			}

			MRUK.Instance.SceneLoadedEvent.AddListener(OnSceneLoaded);
		}

		// Update is called once per frame
		void Update()
		{

		}

		private void OnSceneLoaded()
		{
			MRUKRoom currentRoom = MRUK.Instance.GetCurrentRoom();

			if (currentRoom == null) return;

			Connect();

		}


		#region Public Methods

		/// <summary>
		/// Start the connection process. 
		/// - If already connected, we attempt joining a random room
		/// - if not yet connected, Connect this application instance to Photon Cloud Network
		/// </summary>
		public void Connect()
		{
			// keep track of the will to join a room, because when we come back from the game we will get a callback that we are connected, so we need to know what to do then
			isConnecting = true;
			// we check if we are connected or not, we join if we are , else we initiate the connection to the server.
			if (PhotonNetwork.IsConnected)
			{
				PhotonNetwork.JoinOrCreateRoom("MyRemoteCollaborationRoom", new RoomOptions { MaxPlayers = this.maxPlayersPerRoom }, TypedLobby.Default);
			}
			else
			{
				// #Critical, we must first and foremost connect to Photon Online Server.
				PhotonNetwork.ConnectUsingSettings();
				PhotonNetwork.GameVersion = this.gameVersion;
			}
		}

		#endregion

		#region MonoBehaviourPunCallbacks CallBacks
		// below, we implement some callbacks of PUN
		// you can find PUN's callbacks in the class MonoBehaviourPunCallbacks


		/// <summary>
		/// Called after the connection to the master is established and authenticated
		/// </summary>
		public override void OnConnectedToMaster()
		{
			// we don't want to do anything if we are not attempting to join a room. 
			// this case where isConnecting is false is typically when you lost or quit the game, when this level is loaded, OnConnectedToMaster will be called, in that case
			// we don't want to do anything.
			if (isConnecting)
			{
				PhotonNetwork.JoinOrCreateRoom("MyRemoteCollaborationRoom", new RoomOptions { MaxPlayers = this.maxPlayersPerRoom }, TypedLobby.Default);
			}
		}

		/// <summary>
		/// Called after disconnecting from the Photon server.
		/// </summary>
		public override void OnDisconnected(DisconnectCause cause)
		{
			Debug.LogError("PUN Basics Tutorial/Launcher:Disconnected");

			isConnecting = false;
		}

		/// <summary>
		/// Called when entering a room (by creating or joining it). Called on all clients (including the Master Client).
		/// </summary>
		/// <remarks>
		/// This method is commonly used to instantiate player characters.
		/// If a match has to be started "actively", you can call an [PunRPC](@ref PhotonView.RPC) triggered by a user's button-press or a timer.
		///
		/// When this is called, you can usually already access the existing players in the room via PhotonNetwork.PlayerList.
		/// Also, all custom properties should be already available as Room.customProperties. Check Room..PlayerCount to find out if
		/// enough players are in the room to start playing.
		/// </remarks>
		public override void OnJoinedRoom()
		{
			Debug.Log("PUN Basics Tutorial/Launcher: OnJoinedRoom() called by PUN. Now this client is in a room.\nFrom here on, your game would be running.");

			// #Critical: We only load if we are the first player, else we rely on  PhotonNetwork.AutomaticallySyncScene to sync our instance scene.
			if (PhotonNetwork.CurrentRoom.PlayerCount == 1)
			{
				Debug.Log("We load the 'Room for 1' ");

			}
		}

		#endregion
	}
}
