# Front_End: Quán bar 3D + Login bằng hội thoại (tiến độ & việc làm tiếp)

Cập nhật: 2026-11-04 · Commit: `b804cff` (branch `main`) · Unity 6000.3.11f1 (URP)

Tài liệu này để lần sau gọi Claude làm tiếp: đọc file này trước, không cần dò lại từ đầu.

---

## 1. Đã làm

### 1.1 Quán bar 3D trong `MainScene`
- 22 model FBX vẽ bằng Blender ở `Assets/_Models/Bar/` (BarShell, BarCounter, BottleShelf, BarStool, PoolTable, Jukebox, Sofa, PendantLamp, Frames, Plant, Barrel, Crate...).
- Phòng 28×20×6 m, tâm tại gốc toạ độ. Tường bắc (nội thất) ở z=10; quầy bar z≈7.6; kệ rượu z≈9.65 (đỉnh 4.44 m).
- FBX từ Blender là Z-up: khi instance phải dùng `Quaternion.Euler(-90, yaw, 0)`.
- 3 khu vực bàn chơi `TableZone_1..3` (prefab `Assets/_Prefabs/TableZone.prefab`) tại x=-9/0/9, z=-1. Mỗi bàn = 1 lobby, 4 ghế, có `tableID` 1..3, biển "BÀN n" (`BillboardY` xoay theo camera), đèn treo, đèn spot.
- Biển neon "LIAR'S CARD" trên tường sau kệ rượu (TMP, y=4.95, z=9.92, fontSize 11).
- Ánh sáng: point light theo vị trí đèn thật, đèn treo có glow/emission, ambient + fog, shadow tắt gần hết, static batching cho object tĩnh.
- Menu dựng lại toàn bộ: `Tools/Liar's Card/Build Bar Scene` (`Assets/_Scripts/Editor/BuildBarScene.cs`).
  - ⚠️ Menu này **xoá và dựng lại** cả Player, camera, bàn... Không chạy lại khi chưa cần.
- Script gameplay liên quan đã sửa: `TableCenterPile3D` (`ActiveTable`), `TableDeck3D` (chỉ dựng bộ bài khi có bàn), `RevolverGun` (`SetRestPose`), `ChairItem` (tham chiếu `tableModel`, `gunSpot`).

### 1.2 Tối ưu hiệu năng UI
- `CardBattleUI` và `TableLobbyUI`: hàm `SetTxt` chỉ gán `text` khi giá trị đổi (tránh TMP dựng lại mesh mỗi frame), cache canvas.
- Tiêu đề phòng chờ chỉ còn `Bàn N` (`TableLobbyUI.titleText`).

### 1.3 Login trong cùng scene (đã bỏ chuyển `LoginScene`)
Luồng chơi:
1. Vào game: camera đứng ở sảnh nhìn quầy bar + kệ rượu (`LoginOverviewAnchor`). Người chơi bị khoá (di chuyển/xoay/tương tác/raycast, ẩn thân + tâm ngắm). Chuột hiển thị.
2. Rê/bấm vào **bartender** (đứng sau quầy tại x=0.4, z=8.85) → camera tiến lại gần (`LoginTalkAnchor`), khung hội thoại hiện, chữ gõ dần.
3. Bartender nói 2 câu (bấm chuột/Space để qua câu), sau đó **hàng nhập đăng nhập hiện ngay trong khung hội thoại** (email, mật khẩu, nút gửi; Enter ở ô mật khẩu = gửi). Có chuyển sang hàng **đăng ký** (email, tên hiển thị, mật khẩu) trong cùng khung.
4. Esc khi đang nhập → bartender nói câu huỷ, camera lùi về sảnh.
5. Đăng nhập thành công → `AuthFormController` gọi `LoginRoomController.Welcome()`: form mờ, bartender chào, camera bay cong ~4 giây về vị trí người chơi (spawn `(0, 1, -6.5)`), trả điều khiển, ẩn UI. Bấm phím/chuột bất kỳ để bỏ qua. Không có `LoginRoomController` thì rơi về `LoadScene("MainScene")` như cũ.

File chính:
| File | Vai trò |
|---|---|
| `Assets/_Scripts/UI/LoginRoomController.cs` | State machine: RoomOverview → Transitioning → LoginActive → EnteringBar → InGame; câu thoại chỉnh ở Inspector (`greetLines`, `welcomeLine`, `cancelLine`). Tự bật canvas, tự thêm `GraphicRaycaster` + sortingOrder 20 cho `RoomHintCanvas`. |
| `Assets/_Scripts/UI/AuthFormController.cs` | Gọi API đăng nhập/đăng ký, kết nối `NetworkManager`, Enter = gửi. |
| `Assets/_Scripts/UI/AuthPanelSwitcher.cs` | Chuyển hàng đăng nhập/đăng ký. |
| `Assets/_Scripts/Misc/BartenderIdle.cs` | Bartender thở nhẹ, đầu quay theo camera, tay lau ly. |
| `Assets/_Scripts/Editor/SetupLoginInBar.cs` | (đã chạy) chuyển Canvas/AuthForm/AuthAPI/NetworkManager/LoginRoom từ LoginScene sang MainScene. |
| `Assets/_Scripts/Editor/SetupBartenderLogin.cs` | Dựng bartender (ghép khối), anchor camera, khung hội thoại, nối controller. Chạy lại được. |
| `Assets/_Scripts/Editor/SetupDialogueForm.cs` | (đã chạy một lần) chuyển ô nhập vào khung hội thoại, xoá Canvas cũ. Cần Canvas cũ nên không chạy lại được. |

Đối tượng trong `MainScene` (root): `Bartender`, `LoginOverviewAnchor`, `LoginTalkAnchor`, `LoginRoom`, `RoomHintCanvas` (chứa `DialoguePanel` → `NameText`, `DialogueText`, `FormArea` → `LoginRow`/`RegisterRow`), `AuthForm`, `AuthAPI`, `NetworkManager`.

---

## 2. Chưa kiểm tra / rủi ro đã biết
- **Chưa Play thử đầy đủ** luồng: bấm bartender → gõ chữ → đăng nhập thật → camera lùi → di chuyển. Bố cục khung hội thoại (1500×340) mới chỉ tính toán, chưa nhìn thấy thật.
- Lỗi "không gõ được/không bấm được" đã được xử lý (thiếu `GraphicRaycaster` ở `RoomHintCanvas`), nhưng chưa xác nhận lại sau sửa.
- API: `AuthAPI` baseUrl là `http://localhost:5280` (lấy từ LoginScene), bản fallback cũ là `5000`. Kiểm tra đúng cổng backend.
- Đã xoá `AuthApiClient_Fallback` khỏi MainScene (trùng chức năng với `AuthAPI`); chơi trực tiếp MainScene không cần login thì các script dùng `AuthApiClient.Instance` vẫn có `AuthAPI`.
- `LoginScene.unity` vẫn còn trong Build Settings, không dùng nữa.
- Vật liệu mồ côi `LoginPC_Dark.mat`, `LoginPC_Screen.mat` (chưa commit) của máy tính cũ có thể xoá.
- Hai file Unity tự sửa, **không commit**: `LiberationSans SDF - Fallback.asset`, `ProjectSettings/URPProjectSettings.asset`.
- Evaluator của MCP `run_csharp` (Mono.CSharp) không hỗ trợ hàm cục bộ/`Func` nhiều tham số, và `return` sớm; việc dựng scene nên viết thành menu Editor rồi gọi `ExecuteMenuItem`. Không lưu scene được khi editor đang Play.

---

## 3. Có thể làm tiếp (gợi ý, theo thứ tự)
1. **Play thử & chỉnh bố cục** khung hội thoại/ô nhập; tự chọn ô email khi form hiện (`EventSystem.SetSelectedGameObject`).
2. **Bartender đẹp hơn:** thay bản ghép khối bằng model Blender (kèm animation lau ly/gật đầu); có thể thêm giọng nói/âm thanh gõ chữ.
3. **Hội thoại phong phú:** bartender nhắc tên người chơi sau đăng nhập ("Chào {displayName}"), câu khác khi sai mật khẩu, khi đăng ký thành công.
4. **Dọn dẹp:** xoá `LoginScene` khỏi Build Settings, xoá `LoginPC_*.mat`, cân nhắc gộp các menu Setup vào `BuildBarScene` để dựng lại scene một lần.
5. **Tối ưu thêm:** baked lighting/light probes, occlusion culling cho quán 28×20; kiểm tra Profiler khi có nhiều người chơi.
6. **Gameplay trong quán:** nhấn mạnh việc chọn bàn (biển BÀN 1..3 hiện số người/ trạng thái lobby), hiệu ứng khi ngồi ghế, nhạc nền jukebox, tương tác bartender sau khi đã vào quán (ví dụ hỏi luật chơi).
7. **Quán đa bàn:** hiển thị người chơi khác di chuyển trong quán (`RemotePlayerManager`) — chỉ sau đăng nhập.

## 4. Quy ước làm việc với dự án này
- Trả lời tiếng Việt; Ponytail mode (thay đổi nhỏ nhất, cuối reply ghi "đã bỏ qua/rủi ro").
- Chỉ commit/push khi người dùng yêu cầu. Remote: `https://github.com/DevUnNull/LIARS-CARD-.git`, nhánh `main`.
- Không chạy `Liars Card/Setup CoreScene Demo Now` (ghi đè `MainScene`).
- Kiểm tra compile: `AssetDatabase.Refresh()` rồi đợi số "Domain Reload Profiling" trong `%LOCALAPPDATA%/Unity/Editor/Editor.log` tăng; log còn dòng lỗi cũ, chỉ tin lỗi có dạng `Assets/...cs(dòng,cột): error`.
