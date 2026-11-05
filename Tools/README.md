# Công cụ ngoài Unity

Unity bỏ qua thư mục này, nó không bị đóng gói vào game.

## RefractionCli (C#, cần .NET 8 SDK)

Dùng chung code lõi `Assets/Scripts/Core`, nên kết quả giống hệt trong game.

```
dotnet run --project Tools/RefractionCli -- validate Assets/Resources/CustomLevels     # kiểm tra cả thư mục
dotnet run --project Tools/RefractionCli -- show     Assets/Resources/CustomLevels/x.json   # vẽ bàn + liệt kê tia (dùng solution nếu có)
dotnet run --project Tools/RefractionCli -- solve    x.json --depth 4 --budget 500000 --write
dotnet run --project Tools/RefractionCli -- convert  <thư mục assets của APK> <thư mục ra>   # level gốc -> JSON
dotnet run --project Tools/RefractionCli -- diff     runs.jsonl Assets/Resources/Levels .  # so với logic gốc
```

## OriginalLogicHarness (Java)

Đây là phần logic tia laser **nguyên bản**, cắt ra từ `LevelView.java` (bản dịch ngược). Phần vẽ hình và Android đã được thay bằng stub, còn logic giữ nguyên.

Công cụ này chơi ngẫu nhiên hàng chục nghìn ván trên code gốc, rồi so kết quả với engine mới:

```
javac -d out $(find Tools/OriginalLogicHarness/src -name "*.java")
java -DmaxActions=20 -cp out com.textualindices.refraction.Harness <thư mục assets của APK> 1 20000 > runs.jsonl
dotnet run --project Tools/RefractionCli -- diff runs.jsonl Assets/Resources/Levels .
```

Kết quả đo được (50.000 ván ngẫu nhiên, mỗi ván 1–20 thao tác):

| So sánh | Số ván lệch |
|---|---|
| Bản gốc chơi liên tục so với bản gốc mở lại cùng bàn | ~1,8–3,5 % |
| Engine mới so với bản gốc mở lại | ~0,4–0,7 % |
| Cờ thắng/thua khác nhau | 0 |
| Bản gốc bị crash (StackOverflowError, ArrayIndexOutOfBounds) | 23 ván, rơi vào level 27, 31, 71, 88, 131 |

Hàng thứ nhất là lỗi của chính bản gốc: tia laser hiển thị khác nhau tuỳ vào thứ tự người chơi thao tác. Engine mới thì luôn cho ra cùng một kết quả với cùng một bàn.

Phần lệch còn lại (file `diff_examples.txt`) đã được xem từng trường hợp, gồm 3 loại:

1. **Khoảng 2/3 số ca:** tia đi chéo lách giữa 2 cửa vừa đóng. Bản gốc quên tính lại tia đó nên nó cứ xuyên qua. Engine mới chặn tia, đúng với luật mà chính bản gốc đã viết.
2. **Mở lại lời giải có lăng kính đang pha màu:** bản gốc vẽ tia ra của lăng kính nhưng không tính đường đi của nó (cờ `killOutputPrism`). Engine mới tính đúng đường đi.
3. **Vài bàn rất rối:** nhiều lăng kính và cửa phụ thuộc vòng vào nhau. Ở những bàn này chính bản gốc cũng cho kết quả khác nhau giữa lúc chơi liên tục và lúc mở lại.
