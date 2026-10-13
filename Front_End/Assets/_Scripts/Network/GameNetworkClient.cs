using LiteNetLib;
using LiteNetLib.Utils;
using UnityEngine;

public class GameNetworkClient : MonoBehaviour
{
    private NetManager client;

    private NetPeer serverPeer;
    private readonly NetDataWriter writer = new NetDataWriter();
    private float sendTimer;

    private void Start()
    {
        var listener = new EventBasedNetListener();
        client = new NetManager(listener);

        listener.PeerConnectedEvent += peer =>
        {
            serverPeer = peer;
            Debug.Log("Connected!");
        };

        listener.PeerDisconnectedEvent += (peer, info) =>
        {
            serverPeer = null;
            PlayerManager.Instance.ClearPlayers();
            Debug.Log($"Fail: {info.Reason}");
        };

        listener.NetworkReceiveEvent += (peer, reader, channel, method) =>
        {
            HandleReceived(reader);
        };

        if (!client.Start())
        {
            Debug.LogError("Can not start");
            return;
        }

        client.Connect("127.0.0.1", 9050, "Liar");
        Debug.Log("Connecting");
    }

    private void Update()
    {
        client?.PollEvents();
    }

    private void LateUpdate()
    {
        if (serverPeer == null || serverPeer.ConnectionState != ConnectionState.Connected || PlayerController.Instance == null)
        {
            return;
        }

        sendTimer += Time.deltaTime;

        if (sendTimer < 0.05f)
            return;

        sendTimer = 0f;
        SendMovement();
    }

    private void OnDestroy()
    {
        client?.Stop();
    }

    private void SendMovement()
    {
        Vector3 position = PlayerController.Instance.gameObject.transform.position;
        float yaw = PlayerController.Instance.gameObject.transform.eulerAngles.y;

        writer.Reset();

        writer.Put((byte)1);
        writer.Put(position.x);
        writer.Put(position.y);
        writer.Put(position.z);
        writer.Put(yaw);

        serverPeer.Send(writer, DeliveryMethod.Sequenced);
    }

    private void HandleReceived(NetPacketReader reader)
    {
        try
        {
            if (reader.AvailableBytes < 1)
                return;

            byte messageType = reader.GetByte();

            switch (messageType)
            {
                case 2:
                    {
                        if (reader.AvailableBytes != 4)
                            return;

                        int playerId = reader.GetInt();
                        PlayerManager.Instance.SetMyPlayerId(playerId);
                        break;
                    }

                case 3:
                    {
                        if (reader.AvailableBytes != 4)
                            return;

                        int playerId = reader.GetInt();
                        PlayerManager.Instance.AddRemotePlayer(playerId);
                        break;
                    }

                case 4:
                    {
                        if (reader.AvailableBytes != 4)
                            return;

                        int playerId = reader.GetInt();
                        PlayerManager.Instance.RemoveRemotePlayer(playerId);
                        break;
                    }
                case 5:
                    {
                        if (reader.AvailableBytes != 20)
                            return;

                        int playerId = reader.GetInt();

                        float x = reader.GetFloat();
                        float y = reader.GetFloat();
                        float z = reader.GetFloat();
                        float yaw = reader.GetFloat();

                        PlayerManager.Instance.UpdateRemotePosition(playerId, new Vector3(x, y, z),yaw);
                        break;
                    }
            }
        }
        finally
        {
            reader.Recycle();
        }
    }
}
