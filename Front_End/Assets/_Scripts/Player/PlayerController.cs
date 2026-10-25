using UnityEngine;

public class PlayerController : Singleton<PlayerController>
{
    public FirstPersonLook firstPersonLook;
    public FirstPersonMovement firstPersonMovement;

    [SerializeField] private float sendInterval = 0.05f;
    private float sendTimer;

    private void LateUpdate()
    {
        var network = NetworkManager.Instance;

        if (network == null || !network.IsConnected)
            return;

        sendTimer += Time.unscaledDeltaTime;

        if (sendTimer < sendInterval)
            return;

        sendTimer = 0f;

        network.SendPlayerTransform(
            transform.position,
            transform.eulerAngles.y);
    }
}
