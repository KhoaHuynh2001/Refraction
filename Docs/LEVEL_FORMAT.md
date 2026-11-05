# Định dạng level Refraction (JSON)

File này mô tả đầy đủ luật chơi và định dạng file level. Bạn (hoặc AI) chỉ cần đọc file này là viết được level mới.
Tên trường JSON giữ nguyên tiếng Anh.

## 1. Đặt file ở đâu

| Vị trí | Khi nào dùng |
|---|---|
| `Assets/Resources/CustomLevels/*.json` | Level đóng gói sẵn trong bản build. Thêm file xong phải build lại. |
| `<persistentDataPath>/CustomLevels/*.json` | Thả file vào là chơi được, không cần build lại. Trong game vào **LEVEL PACKS → RELOAD**. Trong Unity mở menu **Refraction → Open Device Custom Levels Folder** để xem đường dẫn. Trên Android thư mục này là `Android/data/com.personal.refraction/files/CustomLevels/`. |
| `Assets/Resources/Levels/*.json` | 140 level gốc (7 sector × 20 level). Không nên sửa. |

Các level được sắp theo `order`, sau đó theo tên file.

## 2. Bàn chơi

- Kích thước mặc định 8 cột × 10 hàng, đổi được bằng `width`/`height` (rộng 3..12, cao 3..14).
- Toạ độ đếm từ 1. `x` tăng từ **trái sang phải**, `y` tăng từ **trên xuống dưới**. Ô (1,1) ở góc trên bên trái.
- Hướng tính bằng độ, ngược chiều kim đồng hồ, **0 = sang phải, 90 = lên trên**:

```
 135  90  45            ↖  ↑  ↗
 180   ·   0            ←  ·  →
 225 270 315            ↙  ↓  ↘
```

Mỗi bước, tia laser đi sang ô kế bên theo 1 trong 8 hướng. Ví dụ hướng 45: x+1, y−1.

## 3. Màu

| Tên | Pha từ |
|---|---|
| `blue`, `yellow`, `red` | màu gốc |
| `green` | blue + yellow |
| `orange` | yellow + red |
| `purple` | blue + red |

Pha 2 màu không hợp nhau (ví dụ red + green) ra tia trắng. Tia trắng không kích hoạt được gì.

## 4. Vật thể cố định (`objects`)

| `type` | Trường thêm | Hành vi |
|---|---|---|
| `emitter` | `color`, `dir` (0,45,…,315) | Bắn tia màu `color` theo hướng `dir`. Chặn mọi tia khác chiếu vào nó. |
| `target` | `color` | Sáng lên khi bị tia **đúng màu** chiếu vào. Tia sai màu bị hấp thụ. **Thắng khi mọi target đều sáng.** |
| `wall` | – | Chặn tia. |
| `switch` | `color` | Khi bị tia đúng màu chiếu vào thì **mở mọi `door` cùng màu**. Mỗi màu chỉ nên có 1 switch. |
| `door` | `color` | Đóng thì chặn tia như tường, mở thì tia đi xuyên qua. Người chơi được đặt gương/lăng kính lên ô cửa, nhưng vật đó chỉ hoạt động khi cửa mở. |
| `portal` | – | Tia đi vào một portal sẽ đi ra ở **tất cả portal khác**, giữ nguyên hướng và màu. Cần ít nhất 2 portal. |

Một số quy tắc về đường đi của tia:

- **Lách chéo:** khi tia đi chéo mà 2 ô hai bên (ô ngang và ô dọc) đều là tường hoặc cửa đóng, tia bị chặn.
- **Cửa mở sẵn từ đầu:** một cửa chỉ mở sẵn khi emitter bắn xuyên qua chính cửa đó tới switch của nó (level gốc 6-3 dùng mẹo này). Mọi cửa khác bắt đầu ở trạng thái đóng.
- **Không tự giữ cửa:** người chơi không thể tạo một tia đi xuyên qua cửa rồi chính tia đó giữ cửa mở.

## 5. Vật người chơi đặt

Số lượng tối đa được đặt bằng `maxMirrors` và `maxPrisms`.

### Gương (`mirror`)

- `angle` là góc của mặt gương, phải là bội số của 22,5 (0, 22.5, 45, …, 337.5). Khi mới đặt, gương có góc 135, tức hình `\`.
- Gương **chỉ phản xạ ở một mặt**. Tia có hướng `a` chỉ được phản xạ khi `0 < (angle − a) mod 360 < 180`. Hướng đi ra là `(2·angle − a) mod 360`.
- Tia chiếu thẳng góc vào gương bị hấp thụ. Tia chiếu vào mặt sau cũng bị hấp thụ.
- Mỗi gương chỉ phản xạ **một tia**. Tia thứ hai chiếu tới bị hấp thụ.

**Bảng tra góc gương** để bẻ tia từ hướng *vào* sang hướng *ra* (dấu – nghĩa là không làm được):

| vào \ ra | 0 | 45 | 90 | 135 | 180 | 225 | 270 | 315 |
|---|---|---|---|---|---|---|---|---|
| 0 | – | 22.5 | 45 | 67.5 | – | 112.5 | 135 | 157.5 |
| 45 | 202.5 | – | 67.5 | 90 | 112.5 | – | 157.5 | 180 |
| 90 | 225 | 247.5 | – | 112.5 | 135 | 157.5 | – | 202.5 |
| 135 | 247.5 | 270 | 292.5 | – | 157.5 | 180 | 202.5 | – |
| 180 | – | 292.5 | 315 | 337.5 | – | 202.5 | 225 | 247.5 |
| 225 | 292.5 | – | 337.5 | 0 | 22.5 | – | 247.5 | 270 |
| 270 | 315 | 337.5 | – | 22.5 | 45 | 67.5 | – | 292.5 |
| 315 | 337.5 | 0 | 22.5 | – | 67.5 | 90 | 112.5 | – |

Ví dụ: tia đi sang phải (0) muốn quay xuống dưới (270) thì đặt gương góc 135.

### Lăng kính (`prism`)

Lăng kính nhận tối đa 2 tia:

- **Nhận 1 tia → tách làm 2.** Tia orange tách thành yellow + red, green thành yellow + blue, purple thành blue + red. Tia màu gốc tách thành 2 tia cùng màu.
  - Hướng mặc định: `vào + 45` cho màu thứ nhất và `vào − 45` cho màu thứ hai (theo thứ tự yellow/red, yellow/blue, blue/red). Ví dụ tia orange đi sang phải (0) tách thành yellow đi 45 và red đi 315.
  - Nếu lăng kính nằm sát mép bàn, hướng mặc định sẽ né mép. Lúc đó **nên ghi rõ hướng** bằng trường `split`.
- **Nhận tia thứ 2 → pha màu.** Hai tia gộp thành 1 tia màu pha. Hướng mặc định là trung bình hai hướng vào, làm tròn xuống theo bội số 45. Nên ghi rõ bằng `combineDir`.
- Tia thứ 3 bị hấp thụ.
- Một tia đã từng đi ra từ lăng kính P thì không thể quay lại làm đầu vào cho chính P.
- Người chơi có thể xoay từng tia ra theo bước 45°. Một tia ra không được trùng hướng với tia ra kia và không được chĩa ngược về hướng tia vào.

## 6. Cấu trúc file

```json
{
  "id": "ai_001",
  "name": "Two Ways",
  "author": "AI",
  "order": 1,
  "width": 8,
  "height": 10,
  "maxMirrors": 3,
  "maxPrisms": 1,
  "objects": [
    {"type": "emitter", "x": 1, "y": 5, "color": "orange", "dir": 0},
    {"type": "target",  "x": 8, "y": 1, "color": "red"},
    {"type": "target",  "x": 8, "y": 9, "color": "yellow"},
    {"type": "wall",    "x": 6, "y": 5},
    {"type": "switch",  "x": 3, "y": 1, "color": "blue"},
    {"type": "door",    "x": 4, "y": 4, "color": "blue"},
    {"type": "portal",  "x": 2, "y": 8}
  ],
  "solution": [
    {"type": "mirror", "x": 4, "y": 7, "angle": 45},
    {"type": "prism",  "x": 4, "y": 5,
     "split": [{"color": "yellow", "dir": 315}, {"color": "red", "dir": 45}]},
    {"type": "prism",  "x": 6, "y": 2, "combineDir": 90}
  ],
  "tutorial": []
}
```

Ý nghĩa các trường:

- `id`: bắt buộc nên có, không trùng giữa các level. Tiến trình chơi được lưu theo tên file.
- `name`: tên hiện trong menu.
- `author`, `order`, `tutorial`: không bắt buộc.
- `solution`: **rất nên có**. Trình kiểm tra dùng nó để chứng minh level giải được, và người chơi bấm **SHOW SOLUTION** trong menu để xem.
- File được phép có comment dạng `// ...` và dấu phẩy thừa ở cuối.

## 7. Kiểm tra level

1. **Trong Unity:** menu **Refraction → Validate Custom Levels (with solver)**. Nếu muốn tìm lời giải cho một file, chọn file `.json` trong Project rồi chạy **Refraction → Validate + Solve Selected Level**.
2. **Bằng dòng lệnh** (cần .NET 8 SDK):

   ```
   dotnet run --project Tools/RefractionCli -- validate Assets/Resources/CustomLevels
   dotnet run --project Tools/RefractionCli -- show  Assets/Resources/CustomLevels/ai_001.json
   dotnet run --project Tools/RefractionCli -- solve Assets/Resources/CustomLevels/ai_001.json --depth 4 --write
   ```

   - `show` vẽ bàn chơi dạng ASCII và liệt kê từng tia.
   - `solve --write` tìm lời giải rồi ghi vào file. Solver chỉ thử được tới khoảng 4–5 vật; level khó hơn thì phải tự ghi `solution`.

Trình kiểm tra **báo lỗi** khi:

- vật thể nằm ngoài bàn hoặc 2 vật trùng ô;
- thiếu emitter hoặc thiếu target;
- có cửa mà không có switch cùng màu;
- có màu target/switch không thể tạo ra từ các emitter (kể cả khi pha bằng lăng kính);
- level đã thắng sẵn khi chưa đặt gì;
- `solution` không giải được, hoặc dùng quá `maxMirrors`/`maxPrisms`.

## 8. Mẹo thiết kế

- Trong 140 level gốc, giới hạn vật thường cho dư khá nhiều (ví dụ 10/10). Muốn level khó thì để giới hạn sát với số vật trong lời giải.
- Đặt tường để chặn đường đi thẳng. Dùng cửa/switch để bắt người chơi làm theo thứ tự: mở cửa trước rồi mới tới được target.
- Một target secondary (green/orange/purple) cần emitter cùng màu, hoặc 2 tia màu gốc gộp lại trong lăng kính.
- Nên chạy validate sau mỗi lần sửa level.
