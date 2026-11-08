using UnityEngine;

/// <summary>Danh sách model nhân vật dùng làm skin. ID skin = vị trí trong mảng (gửi qua mạng dạng byte).</summary>
[CreateAssetMenu(fileName = "SkinCatalog", menuName = "LiarsCard/Skin Catalog")]
public class SkinCatalog : ScriptableObject
{
    [Tooltip("Model FBX cùng bộ xương với Character.fbx (armature \"character-skate-boy\"). Thứ tự = ID skin, không đổi thứ tự sau khi phát hành.")]
    public GameObject[] models;

    public int Count => models != null ? models.Length : 0;

    public string DisplayName(int id) =>
        id >= 0 && id < Count && models[id] != null ? models[id].name.Replace("character-", "").Replace('-', ' ') : "?";
}
