# Prompt mẫu để nhờ AI tạo level

Copy toàn bộ khối dưới đây vào ChatGPT, Claude, Gemini… Đính kèm thêm file `Docs/LEVEL_FORMAT.md` nếu AI đọc được file. Sửa phần **[YÊU CẦU]** theo ý bạn.

---

```
Bạn là người thiết kế level cho game giải đố laser "Refraction". Hãy tạo level dạng JSON đúng định dạng dưới đây.

LUẬT (tóm tắt):
- Bàn 8 cột x 10 hàng. x = 1..8 từ trái sang phải, y = 1..10 từ trên xuống dưới.
- Hướng: 0 = phải, 45 = phải-lên, 90 = lên, 135 = trái-lên, 180 = trái, 225 = trái-xuống, 270 = xuống, 315 = phải-xuống.
  Đi hướng 45 nghĩa là x+1, y-1; hướng 270 nghĩa là y+1.
- Màu: blue, yellow, red (gốc); green = blue+yellow, orange = yellow+red, purple = blue+red.
- Vật cố định: emitter(color, dir), target(color) cần được tia đúng màu chiếu vào, wall chặn tia,
  switch(color) mở mọi door cùng màu khi bị tia đúng màu chiếu vào, door(color) đóng thì chặn,
  portal: tia vào 1 portal sẽ ra ở các portal khác, giữ nguyên hướng.
- Tia đi chéo bị chặn nếu 2 ô hai bên đều là tường hoặc cửa đóng.
- Người chơi đặt mirror (gương) và prism (lăng kính) lên ô trống, tối đa maxMirrors / maxPrisms.
- Mirror có "angle" là bội số 22.5. Gương một mặt: tia hướng a chỉ phản xạ khi 0 < (angle - a) mod 360 < 180,
  hướng ra = (2*angle - a) mod 360. Chiếu thẳng góc thì bị hấp thụ. Mỗi gương chỉ phản xạ 1 tia.
  Góc gương để bẻ tia từ hướng vào A sang hướng ra B: angle = (A + B) / 2, hoặc (A + B) / 2 + 180;
  chọn giá trị thoả điều kiện mặt phản xạ. Không bẻ được 0° hay 180°.
- Prism nhận 1 tia: tách orange -> yellow + red, green -> yellow + blue, purple -> blue + red,
  màu gốc -> 2 tia cùng màu. Mặc định ra ở hướng vào+45 và vào-45.
  Nhận tia thứ 2: gộp thành 1 tia màu pha. Luôn ghi rõ hướng ra trong solution:
  "split":[{"color":..,"dir":..},{"color":..,"dir":..}] hoặc "combineDir":..
- Thắng khi mọi target đều sáng.

ĐỊNH DẠNG (một file JSON cho mỗi level):
{
  "id": "ai_001", "name": "Tên level", "author": "AI", "order": 1,
  "width": 8, "height": 10, "maxMirrors": 3, "maxPrisms": 1,
  "objects": [
    {"type":"emitter","x":1,"y":5,"color":"orange","dir":0},
    {"type":"target","x":8,"y":1,"color":"red"},
    {"type":"wall","x":6,"y":5}
  ],
  "solution": [
    {"type":"prism","x":4,"y":5,"split":[{"color":"yellow","dir":315},{"color":"red","dir":45}]},
    {"type":"mirror","x":7,"y":2,"angle":45}
  ]
}

YÊU CẦU BẮT BUỘC:
1. Mỗi level PHẢI có "solution" giải được. Hãy dò từng tia ô theo ô trong solution và kiểm tra mọi target đều sáng.
2. Không 2 vật trùng ô; vật trong solution phải nằm trên ô trống (hoặc ô door).
3. Bàn trống (chưa đặt gì) KHÔNG được thắng sẵn.
4. maxMirrors / maxPrisms bằng số vật trong solution cộng thêm 0–2.
5. Có door thì phải có switch cùng màu.
6. Vẽ bàn chơi dạng ASCII kèm theo để kiểm tra (# tường, E emitter, T target, S switch, D door, O portal, M gương, P lăng kính).

[YÊU CẦU]: Tạo 5 level độ khó tăng dần, có dùng door/switch và ít nhất 1 level dùng prism để pha màu.
Đặt tên id ai_001..ai_005. Trả về mỗi level trong một khối ```json riêng.
```

---

## Sau khi AI trả kết quả

1. Lưu mỗi khối JSON thành một file, ví dụ `ai_001.json`, vào `Assets/Resources/CustomLevels/` hoặc thư mục CustomLevels trên máy chơi.
2. Chạy kiểm tra:
   - Trong Unity: **Refraction → Validate Custom Levels**.
   - Hoặc bằng lệnh: `dotnet run --project Tools/RefractionCli -- validate Assets/Resources/CustomLevels`.
3. Nếu có lỗi (thường gặp nhất là `The included solution does NOT solve the level`), gửi lại cho AI:
   - thông báo lỗi;
   - kết quả của lệnh `show`, liệt kê từng tia đi tới đâu.

   AI dựa vào đó để tự sửa. Thường chỉ cần 1–2 vòng là xong.
4. Vào game, mở **LEVEL PACKS** rồi bấm **RELOAD**.
