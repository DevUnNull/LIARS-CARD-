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
            Debug.Log($"Fail: {info.Reason}");
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
}
