using UnityEngine;

public class CursorManager : Singleton<CursorManager>
{
    public void HideCursor()
    {
        Cursor.visible = false;
        Cursor.lockState = Application.isMobilePlatform ? CursorLockMode.None : CursorLockMode.Locked; // khoá con trỏ làm Simulator đổi chuột->chạm bị kẹt giữa màn hình
    }

    public void EnableCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
