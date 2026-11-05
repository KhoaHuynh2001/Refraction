# Refraction – bản dựng lại bằng Unity

Đây là game giải đố laser **Refraction 3.0** (`com.textualindices.refraction`), viết lại bằng Unity từ bản dịch ngược APK. Bản này chỉ để chơi cá nhân: toàn bộ hình ảnh, âm thanh và 140 level đều thuộc bản quyền của nhà phát triển gốc, nên đừng phát hành lại.

## Mở project

1. Mở Unity Hub, chọn **Add → Add project from disk**, rồi chọn thư mục này.
   - Nên dùng **Unity 6 LTS** (6000.0.x). Unity 2022.3 LTS cũng chạy được.
   - Nếu Hub báo lệch phiên bản, chọn bản Unity bạn đã cài.
2. Lần đầu mở project, Unity tự cài package Input System. Nếu nó hỏi *"enable the new input backends? / restart"*, chọn **Yes**. Code chạy được với cả hệ input cũ lẫn mới.
3. Script `Assets/Editor/RefractionEditor.cs` tự làm các việc sau:
   - tạo scene `Assets/Scenes/Main.unity` và thêm vào Build Settings;
   - đặt màn hình dọc, tên app, package id `com.personal.refraction`.

   Nếu có bước nào không chạy, vào menu **Refraction → Setup Project**.
4. Bấm **Play**. Trong cửa sổ Game nên chọn tỉ lệ dọc, ví dụ 1080x1920.

### Build ra APK

Vào **File → Build Profiles** (bản 2022 là Build Settings), chọn Android, bấm **Switch Platform** rồi **Build**. Máy phải có module Android Build Support.

## Cách chơi

| Thao tác | Chạm / chuột | Phím |
|---|---|---|
| Đặt gương hoặc lăng kính | Kéo từ khay dưới vào ô trống, hoặc bấm nút rồi chạm vào ô | M / P (chọn vật) |
| Chọn vật | Chạm vào vật | |
| Di chuyển vật | Kéo vật đã chọn sang ô khác | |
| Xoay vật | Nút ROTATE, kéo vòng quanh vật đang chọn, hoặc lăn chuột | Q / E |
| Đổi tia ra của lăng kính | Nút TOGGLE, hoặc chạm lại vào lăng kính | Tab |
| Xoá vật | Nút DELETE hoặc chạm nhanh 2 lần | Delete |
| Hoàn tác | Nút UNDO trên thanh trên | Z |
| Đặt lại level | Menu ≡ → RESET | R |
| Mở menu | Nút ≡ | Esc / nút Back của Android |

Các quy tắc mở khoá giữ như bản gốc:

- Mở được sector tiếp theo sau khi giải 11 level của sector trước.
- Muốn bỏ giới hạn này thì vào **Options → Unlock all sectors**.
- Level pack trả phí của bản gốc (level 121–140) giờ là sector 7, mở theo đúng quy tắc trên.

## Đã sửa so với bản gốc

Tôi cắt logic laser của bản gốc ra chạy riêng, rồi so với engine mới trên 50.000 bàn chơi ngẫu nhiên. Chi tiết xem `Tools/README.md`. Các lỗi tìm được:

1. **Crash:** ở level 27, 31, 71, 88 và 131, đặt gương/lăng kính ở một số chỗ làm tia laser đệ quy vô hạn (StackOverflowError), hoặc lỗi chỉ số mảng (ArrayIndexOutOfBounds). Hai lỗi này làm game văng.
2. **Tia laser vẽ sai hoặc không cập nhật:** bản gốc chỉ tính lại một phần mỗi khi bạn thao tác, rồi mỗi khung hình chạy thêm `garbageCollect()` để dọn. Vì vậy cùng một bàn chơi có thể hiển thị khác nhau tuỳ thứ tự thao tác, gặp ở khoảng 2–3 % số ván. Hay gặp nhất là tia vẫn xuyên qua khe chéo giữa 2 cửa vừa đóng.
3. **Mất lăng kính khi mở lại level:** lăng kính chưa có tia nào chiếu tới bị lưu thành `0,x,y`, nhưng lúc mở lại thì code đọc thiếu trường nên lăng kính bị bỏ qua.
4. **Tia ra của lăng kính đang pha màu không được tính khi mở lại level** (cờ `killOutputPrism`).

Engine mới (`Assets/Scripts/Core/Simulator.cs`) tính lại **toàn bộ** bàn chơi sau mỗi thay đổi, mất chưa tới 1 ms. Kết quả chỉ phụ thuộc vào vị trí các vật, không phụ thuộc thứ tự thao tác. Luật chơi vẫn giữ y như bản gốc:

- gương phản xạ một mặt, và mỗi gương chỉ phản xạ 1 tia;
- lăng kính: tia thứ nhất bị tách, tia thứ hai làm lăng kính chuyển sang pha màu;
- quy tắc lách chéo giữa tường/cửa;
- cửa mở sẵn ở level 6-3;
- quy tắc chống "tự giữ cửa" (`tracedDoor`).

Không có ván nào cho kết quả thắng/thua khác với bản gốc.

Các phần của bản gốc **không** được làm lại:

- nút khoá lăng kính (lock);
- lưu nhiều lời giải cho một level;
- chụp màn hình;
- bảng màu "Light" (chỉ giữ bảng màu "Pigment" mặc định);
- quảng cáo, App Center analytics, thanh toán.

Phần thêm mới:

- nút Undo;
- tuỳ chọn hiện chữ cái màu (cho người khó phân biệt màu);
- kéo vòng quanh vật để xoay;
- phím tắt;
- mục LEVEL PACKS chứa level tự làm.

## Thêm level (tự làm hoặc nhờ AI)

- Định dạng và luật chơi đầy đủ: **`Docs/LEVEL_FORMAT.md`**.
- Prompt mẫu để copy cho AI: **`Docs/AI_PROMPT.md`**.
- Level mẫu: `Assets/Resources/CustomLevels/sample_*.json`.

Có hai cách để game nhận level mới:

- Bỏ file `.json` vào `Assets/Resources/CustomLevels/`. Cách này phải build lại.
- Bỏ vào thư mục `CustomLevels` trên máy chơi, rồi trong game vào **LEVEL PACKS → RELOAD**. Cách này không cần build lại.

Kiểm tra level ngay trong Unity bằng menu **Refraction → Validate Custom Levels**, hoặc bằng dòng lệnh:

```
dotnet run --project Tools/RefractionCli -- validate Assets/Resources/CustomLevels
```

## Cấu trúc thư mục

```
Assets/
  Scripts/Core/      luật chơi thuần C#, không phụ thuộc Unity
                     (Level, Simulator, Board, Solver, Validator, MiniJson)
  Scripts/Game/      phần Unity: App (khởi động, camera), GameScreen (bàn chơi),
                     Screens (menu), UI, SaveData, LevelLibrary
  Editor/            tự cấu hình project, import sprite, menu Refraction
  Resources/
    Levels/          140 level gốc đã chuyển sang JSON
    CustomLevels/    level tự thêm
    Sprites/, Tutorials/, Sounds/, Fonts/   tài nguyên lấy từ APK
Docs/                định dạng level, prompt cho AI
Tools/               CLI kiểm tra/giải level; bộ test đối chiếu với logic Java gốc
```

Toàn bộ giao diện được dựng bằng code nên scene gần như trống. Script `App` tự khởi chạy qua `RuntimeInitializeOnLoadMethod`.
Tiến trình chơi được lưu ở `Application.persistentDataPath/save.json`.
