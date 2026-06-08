<div align="center">

<a href="https://optimizerduck.vercel.app/"><img src="./.github/assets/optimizerDuck.png" alt="optimizerDuck Banner" title="optimizerDuck"/></a>

# [optimizerDuck](https://optimizerduck.vercel.app/)

**optimizerDuck là công cụ tối ưu Windows miễn phí, mã nguồn mở, tập trung vào hiệu năng, quyền riêng tư và sự đơn giản.**

[![Release](https://img.shields.io/github/release/itsfatduck/optimizerDuck?color=fed114&label=Phi%C3%AAn%20b%E1%BA%A3n&style=flat-square)](https://github.com/itsfatduck/optimizerDuck/releases/latest)
[![Downloads](https://img.shields.io/github/downloads/itsfatduck/optimizerDuck/total?label=L%C6%B0%E1%BB%A3t%20t%E1%BA%A3i&style=flat-square&color=lightgreen)](https://github.com/itsfatduck/optimizerDuck/releases)
[![Stars](https://img.shields.io/endpoint?url=https://api.pinstudios.net/api/badges/stars/itsfatduck/optimizerDuck&style=flat-square)](https://github.com/itsfatduck/optimizerDuck/stargazers)
[![License](https://img.shields.io/badge/Gi%E1%BA%A5y%20ph%C3%A9p-GPL_v3-red?style=flat-square)](./LICENSE)
[![Discord](https://img.shields.io/discord/1091675679994675240?color=5865f2&label=Discord&style=flat-square)](https://discord.gg/tDUBDCYw9Q)
<br>
[![CI](https://github.com/itsfatduck/optimizerDuck/actions/workflows/ci.yml/badge.svg)](https://github.com/itsfatduck/optimizerDuck/actions/workflows/ci.yml)
[![.NET Latest](https://img.shields.io/badge/.NET_Runtime-M%E1%BB%9Bi%20nh%E1%BA%A5t-ef99dd?style=flat-square)](https://dotnet.microsoft.com/en-us/download)
[![Supported OS](https://img.shields.io/badge/H%E1%BB%87%20%C4%91i%E1%BB%81u%20h%C3%A0nh-Windows_10%2B_x64-0078d4?style=flat-square)](https://www.microsoft.com/en-us/software-download/)

**[Bắt đầu](https://optimizerduck.vercel.app/docs/guides/getting-started) | [Cách hoạt động](https://optimizerduck.vercel.app/docs/guides/how-it-works) | [Câu hỏi thường gặp](https://optimizerduck.vercel.app/docs/faq/general)**

[English](README.md) | **Tiếng Việt** | [繁體中文](README.zh-TW.md) | [简体中文](README.zh-CN.md) | [Русский](README.ru-RU.md) | [Français](README.fr-FR.md) | [Español](README.es-ES.md) | [한국어](README.ko-KR.md)

<details>
<summary>⭐ Lịch sử Star</summary>

Nếu optimizerDuck giúp PC bạn ngon hơn, hãy cho repo một ⭐ và rủ thêm bạn bè xài cùng nhé.
Càng nhiều sao càng có động lực cải thiện công cụ.

<a href="https://www.star-history.com/#itsfatduck/optimizerDuck&legend=top-left">
 <picture>
   <source media="(prefers-color-scheme: dark)" srcset="https://api.star-history.com/svg?repos=itsfatduck/optimizerDuck&theme=dark&legend=top-left" />
   <source media="(prefers-color-scheme: light)" srcset="https://api.star-history.com/svg?repos=itsfatduck/optimizerDuck&legend=top-left" />
   <img alt="Star History Chart" src="https://api.star-history.com/svg?repos=itsfatduck/optimizerDuck&legend=top-left" />
 </picture>
</a>

</details>

<img src="./.github/assets/app.png" alt="optimizerDuck Chế độ tối" title="optimizerDuck Chế độ tối" width="800"/>

</div>

---

## Bắt đầu nhanh

1. Tải về từ **[GitHub Releases](https://github.com/itsfatduck/optimizerDuck/releases/latest)**
2. Chạy trực tiếp file `.exe`, không cần cài đặt
3. Chọn các tối ưu bạn muốn, áp dụng và khởi động lại máy khi sẵn sàng

> [!TIP]
> Luôn tạo **điểm khôi phục hệ thống** trước khi thực hiện thay đổi.

> [!NOTE]
> | | Ngôn ngữ | Tên bản địa | Người dịch |
> |------|----------|-------------|------------|
> | 🇺🇸 | English (United States) | English | Chính thức & khuyến nghị |
> | 🇻🇳 | Vietnamese | Tiếng Việt | [itsfatduck](https://github.com/itsfatduck) |
> | 🇹🇼 | Traditional Chinese | 正體中文 | [abc0922001](https://github.com/abc0922001) |
> | 🇨🇳 | Simplified Chinese | 简体中文 | [wcxu21](https://github.com/wcxu21) |
> | 🇷🇺 | Russian | Русский | [Foodhead](https://github.com/Foodhead) |
> | 🇫🇷 | French | Français | [Robocnop](https://github.com/Robocnop) |
> | 🇰🇷 | Korean | 한국어 | [klfnn](https://github.com/klfnn) |
> | 🇪🇸 | Spanish | Español | [thexxtt](https://github.com/thexxtt) |

> Muốn thêm ngôn ngữ của bạn? Xem [CONTRIBUTING.md](./CONTRIBUTING.md) ([bản tiếng Nhật](./CONTRIBUTING.ja-JP.md)).

---

## optimizerDuck làm gì

Bản thân Windows vốn ổn. Nhưng cài mới xong là nó kèm theo cả đống dịch vụ, telemetry, app mặc định và tác vụ lên lịch mà chắc bạn chưa từng nghe tới, tất cả đều âm thầm chạy nền, ngốn CPU, RAM và ổ cứng. Trong khi mấy tính năng có thể giúp bạn tận dụng tối đa phần cứng lại không được bật sẵn.

optimizerDuck gom hết vào một chỗ cho bạn quét dọn đồ thừa và bật mấy thứ hữu ích lên.

Công cụ này sẽ chỉnh sửa một số cài đặt Windows để giảm tải và chặn mấy thứ linh tinh chạy ngầm, đồng thời tích hợp sẵn mấy tiện ích cho bạn xem cái gì đang chạy, gỡ mấy thứ không cần và hoàn tác nếu có vấn đề.

> [!NOTE]
> Mọi tối ưu hóa đều có thể áp dụng thủ công. optimizerDuck chỉ giúp bạn thực hiện các tối ưu này dễ dàng hơn.

### Tối ưu hệ thống

Hơn 30 tinh chỉnh chia làm 6 nhóm, cái nào cũng có mô tả rõ ràng và gắn nhãn rủi ro để bạn biết mình đang bấm vào cái gì trước khi áp dụng.

| Danh mục                | Nội dung                                                                                                                                                          |
| :---------------------- | :---------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Hiệu suất**           | Tinh chỉnh Service Host dựa trên RAM máy bạn, điều chỉnh ưu tiên tiến trình, giảm độ trễ bàn phím, tối ưu Multimedia Scheduler cho trải nghiệm chơi game mượt hơn |
| **Quyền riêng tư**      | Vô hiệu hóa telemetry Windows, báo cáo lỗi, ID quảng cáo, theo dõi vị trí, Cortana, Copilot và gợi ý phân phối nội dung                                           |
| **GPU**                 | Tinh chỉnh registry theo từng hãng cho GPU AMD, NVIDIA và Intel, bao gồm trạng thái nguồn, clock gating và độ trễ hiển thị                                        |
| **Nguồn điện**          | Tắt chế độ ngủ đông và khởi động nhanh, tắt USB selective suspend, cài đặt power plan hiệu năng cao tùy chỉnh, vô hiệu hóa power throttling                       |
| **Bloatware & Dịch vụ** | Chặn hành vi cài lại ứng dụng OEM và tinh chỉnh chế độ khởi động cho hơn 200 dịch vụ Windows                                                                      |
| **Trải nghiệm**         | Loại bỏ độ trễ hiển thị menu, tắt hiệu ứng hình ảnh như animation thanh taskbar và độ trong suốt để máy phản hồi nhanh hơn                                        |

> [!NOTE]
> Mấy cái tối ưu ở đây đều tham khảo từ các tool nổi tiếng có nhiều người xài, không có gì do AI code hay thêm bừa bãi hết. Cái nào cũng được chọn vì có tác dụng thiệt.

### Tùy chỉnh

Không cần mò registry, toàn toggle, dropdown với ô nhập số hết, tất cả gom một chỗ. Chia làm bốn nhóm:

- **Màn hình nền**: Ẩn/hiện icon (This PC, Thùng rác, Network, User Files, Control Panel), bỏ mũi tên shortcut
- **Tùy chỉnh chung**: Căn taskbar, widget, nút Task View và End Task, giây đồng hồ, chế độ tối, đuôi file, file ẩn, lịch sử clipboard, compact view, snap assist, checkbox, menu chuột phải cổ điển, tìm kiếm Bing
- **Chơi game**: Game Mode, Game Bar, quay video nền, tăng tốc chuột, tối ưu toàn màn hình, lập lịch GPU
- **Hệ thống**: Tự bật Num Lock khi khởi động

### Công cụ tích hợp

| Công cụ               | Chức năng                                                                                                                    |
| :-------------------- | :--------------------------------------------------------------------------------------------------------------------------- |
| **System Dashboard**  | Xem thông tin CPU, RAM, GPU, ổ đĩa lưu trữ và chi tiết hệ điều hành trong một bảng                                           |
| **Startup Manager**   | Liệt kê mọi ứng dụng và tác vụ khởi động cùng máy, bật/tắt chúng, mở vị trí file                                             |
| **Scheduled Tasks**   | Duyệt, chạy, dừng, bật, tắt hoặc xóa các tác vụ lên lịch của Windows                                                         |
| **Disk Cleanup**      | Quét và dọn file tạm, cache hệ thống, file thừa Windows Update, prefetch, thumbnail, thùng rác, crash dump và bản Windows cũ |
| **Bloatware Remover** | Liệt kê tất cả gói AppX có thể gỡ kèm nhãn rủi ro (An toàn, Thận trọng, Không rõ), giúp bạn chọn những gì muốn xóa           |

---

## An toàn

Thay đổi cài đặt hệ thống có rủi ro, nên tụi mình xây dựng công cụ này với ưu tiên hàng đầu là an toàn và dễ hoàn tác.

Xem [Chính sách bảo mật](./PRIVACY.md) để biết chi tiết về cách chúng tôi xử lý dữ liệu.

- **Tự động backup**: Mỗi thay đổi đều ghi file hoàn tác vào thư mục riêng. Bạn có thể phục hồi từng cái hoặc rollback hết
- **Hoàn tác một click**: Undo cái nào đã áp dụng thẳng từ giao diện
- **Gắn nhãn rủi ro**: Mỗi tinh chỉnh có nhãn An toàn, Trung bình hoặc Rủi ro tùy mức ảnh hưởng
- **Không tự ý áp dụng**: Bạn chọn thì mới chạy, tool không tự động bật gì hết
- **Nhắc tạo restore point**: Lần đầu tối ưu, app sẽ gợi ý bạn tạo điểm khôi phục Windows

---

## Câu hỏi thường gặp

### Dùng optimizerDuck có an toàn không?

Có chứ. optimizerDuck là **mã nguồn mở** (GPL v3), nghĩa là ai cũng có thể tự xem code, kiểm tra hoặc tự build được. Mỗi bản release đều được **GitHub Actions** build tự động từ source công khai, không có vụ giấu giếm hay nhét file lạ vào sau khi build. Nếu không yên tâm thì bạn clone repo về, gõ `dotnet build` là ra file `.exe` ngay.

Ứng dụng **hoàn toàn không** thu thập telemetry, thông tin sử dụng hay dữ liệu cá nhân gì hết. Xem thêm [Chính sách bảo mật](./PRIVACY.md).

### optimizerDuck có thực sự giúp máy chạy nhanh hơn, giảm lag hay tăng tốc mạng không?

Có thể giúp được. Mấy cái tinh chỉnh trong optimizerDuck đều **tham khảo từ các tool nổi tiếng, hướng dẫn trong cộng đồng và khuyến nghị từ hãng phần cứng**, chứ không phải AI bịa ra hay thêm đại cho có. Mỗi cái đều canh vào một cài đặt thật mà Windows để ở mức an toàn quá mức (ví dụ như nhóm service host, chế độ nguồn GPU, bóp băng thông mạng, lập lịch tiến trình).

Không có mấy món registry hãm vô đây, cái nào cũng có mục đích rõ ràng và đã được kiểm chứng qua cộng đồng với tài liệu từ hãng.

### Sao Windows SmartScreen / Defender lại báo động khi tải về?

Tại optimizerDuck không có ký số (code-sign) — vì cái chứng chỉ ký số mắc khủng khiếp với dự án mã nguồn mở. Windows mà gặp file `.exe` chưa ký tải từ mạng về thì SmartScreen mặc định hiện cảnh báo. Chuyện bình thường thôi, **không** có nghĩa là file độc hại đâu.

Để qua mặt thì bấm **"Thông tin khác" > "Chạy"**. Nếu vẫn còn lo:
- Tự build `.exe` từ [source](https://github.com/itsfatduck/optimizerDuck)
- Gửi file lên mấy trang sandbox như ANY.RUN kiểm tra độc lập

### Bị lỗi có hoàn tác được không?

Được. Trước khi áp dụng cái gì optimizerDuck cũng tạo file để hoàn tác hết. Bạn có thể undo từng cái riêng lẻ hoặc quăng hết từ giao diện chỉ một cú bấm. Trước lần tinh chỉnh đầu tiên app cũng sẽ gợi ý tạo điểm khôi phục Windows luôn.

### Xài được trên Windows 10 và Windows 11 không?

Được. optimizerDuck hỗ trợ cả **Windows 10 (x64)** và **Windows 11 (x64)**.

### Cần quyền admin không?

Cần. Vì nó can thiệp vào cài đặt hệ thống với registry Windows, nên bắt buộc phải chạy với quyền quản trị viên.

### optimizerDuck có thu thập dữ liệu của mình không?

Không. Trong app không có cái gì gọi là telemetry, phân tích hay gửi dữ liệu về đâu hết. Nó chạy ngoại tuyến hoàn toàn, không gửi gì đi cả.

---

## Chi tiết kỹ thuật

- **Framework**: WPF trên .NET 10, sử dụng thư viện WPF UI cho thiết kế Fluent
- **Hệ thống hoàn tác**: Bốn loại bước hoàn tác (Registry, Service, Scheduled Task, Shell) với trạng thái lưu JSON, xử lý file an toàn đa luồng
- **Giao diện**: Chế độ Tối (mặc định), Sáng và Tương phản cao, hỗ trợ Mica backdrop
- **Không cần cài đặt**: Chạy dưới dạng file .exe duy nhất, không cần cài đặt
- **Hệ thống sao lưu**: Thư mục cục bộ sao lưu cho mọi thay đổi, khôi phục một cú nhấp
- **Khám phá tự động**: Optimization và Feature categories được khám phá tự động qua reflection + custom attributes, không cần đăng ký thủ công
- **Không telemetry**: Ứng dụng không thu thập bất kỳ dữ liệu người dùng nào

---

## Tài liệu

### [Tài liệu chính thức](https://optimizerduck.vercel.app/docs/guides/getting-started)

Hướng dẫn từng bước, chi tiết các tối ưu và mẹo xài optimizerDuck hiệu quả.

---

## Đóng góp

Báo bug, thêm tối ưu mới, cải thiện tài liệu hay đóng góp bản dịch — tất cả đều welcome. Xem [CONTRIBUTING.md](./CONTRIBUTING.md) ([bản tiếng Nhật](./CONTRIBUTING.ja-JP.md)).

---

## Cộng đồng

> [!TIP]
> Vào Discord để được hỗ trợ, chia sẻ mẹo và tám chuyện với người dùng khác cùng contributor.
>
> <a href="https://discord.gg/tDUBDCYw9Q"><img src="https://discord.com/api/guilds/1091675679994675240/widget.png?style=banner2" alt="Discord Banner 2"/></a>

Nếu optimizerDuck có ích cho PC của bạn:

- ⭐ Star repo
- 💬 Vào Discord trao đổi
- 🐞 Báo bug trên GitHub
- 🎁 Ủng hộ dự án [tại đây](https://optimizerduck.vercel.app/docs/contribute/support-me)

### Liên kết

- 🌐 [Website](https://optimizerduck.vercel.app/)
- 📖 [Tài liệu](https://optimizerduck.vercel.app/docs/guides/getting-started)
- 💬 [Discord](https://discord.gg/tDUBDCYw9Q)
- 🐞 [Issues](https://github.com/itsfatduck/optimizerDuck/issues)

Báo lỗi, góp ý tính năng, dịch thuật hay kể cả chỉ là kể trải nghiệm xài — tất cả đều giúp dự án tiến xa hơn.

---

## Tuyên bố miễn trừ trách nhiệm

optimizerDuck được cung cấp **"nguyên trạng"**, không bảo hành dưới bất kỳ hình thức nào.

Bằng cách sử dụng công cụ này, bạn đồng ý rằng tác giả không chịu trách nhiệm về sự mất ổn định hệ thống, mất dữ liệu hoặc các vấn đề do phần mềm bên thứ ba hoặc thay đổi từ người dùng.

Luôn tạo **điểm khôi phục** trước khi áp dụng thay đổi.

> [!NOTE]
> optimizerDuck sửa đổi cài đặt hệ thống và registry Windows. Tự chịu rủi ro khi sử dụng. Chúng tôi khuyến nghị sao lưu dữ liệu quan trọng và tạo điểm khôi phục trước khi thực hiện thay đổi.
>
> Xem [Điều khoản dịch vụ](./TERMS.md), [Chính sách bảo mật](./PRIVACY.md) và [Tuyên bố miễn trừ](./DISCLAIMER.md) để biết thêm.

---

## Giấy phép

<div align="center">

<a href="./LICENSE">
<img src=".github/assets/gplv3.png" alt="GPL v3 License" title="GPL v3 License"/>
</a>

**[Giấy phép GPL v3](https://www.gnu.org/licenses/gpl-3.0.en.html)**<br>Xem [LICENSE](./LICENSE).

</div>

<div align="center">

## Cảm ơn tất cả đóng góp viên

[![Contributors](https://contrib.rocks/image?repo=itsfatduck/optimizerDuck)](https://github.com/itsfatduck/optimizerDuck/graphs/contributors)

</div>
