\[ Microsoft Account ] (Email cá nhân của bạn)

&#x20;        │

&#x20;        ▼

\[ Microsoft Entra ID Tenant (Azure AD B2C) ] (Tự động tạo ra - Đóng vai trò là Công ty / Tổ chức của bạn)

&#x20;  ├── Mặc định có 1 Domain: ten\_cua\_://onmicrosoft.com (Có thể custom thành company.com)

&#x20;  ├── Quản lý Users (Các thành viên bạn add vào)

&#x20;  └── Quản lý App Registrations (hoặc Service Principal) (Tài khoản Bot cấp cho Terraform)

&#x20;        │

&#x20;        ▼

\[ Azure Subscription ] (Thẻ ngân sách / Gói thanh toán liên kết vào Tenant)

&#x20;        │

&#x20;        ▼

\[ Azure Resources ] (Máy ảo, Ổ cứng, Cơ sở dữ liệu do Terraform tạo ra)



TẠO APP REGISTRATION (TÀI KHOẢN DÀNH CHO TERRAFORM)

Như đã phân tích, để Terraform chạy tự động một cách an toàn và chuyên nghiệp nhất, chúng ta không dùng tài khoản user người thật, mà sẽ tạo ra một "tài khoản bot". Khái niệm này trong Azure gọi là App Registration (hoặc Service Principal).

Cách tạo:

1\. Tại Azure Portal, tìm và vào dịch vụ Microsoft Entra ID.

2\. Ở menu bên trái, chọn App registrations > Bấm New registration.

3\. Điền tên (Ví dụ: terraform-bot), phần Supported account types giữ nguyên mặc định > Bấm Register.

4\. Lúc này màn hình sẽ hiện ra thông tin của Bot. Bạn hãy copy và lưu lại 2 thông số:

&#x09;• Application (client) ID

&#x09;• Directory (tenant) ID

5\. Tiếp tục nhìn menu bên trái của con bot này, chọn Certificates \& secrets > Bấm New client secret > Chọn thời gian hết hạn và bấm Add.

6\. Quan trọng: Hãy copy ngay chuỗi ký tự ở cột Value (đây là Mật khẩu của bot, nó chỉ hiện 1 lần duy nhất).



PHÂN QUYỀN CHO BOT ĐƯỢC XÀI SUBSCRIPTION

Con bot terraform-bot bạn vừa tạo ở Bước 4 hiện tại mới chỉ là một thành viên trong công ty, nó chưa có quyền đụng vào ví tiền (Subscription) của bạn.

1\. Quay lại mục Subscriptions > Chọn Subscription của bạn.

2\. Chọn Access control (IAM) > Add > Add role assignment.

3\. Chọn vai trò là Contributor > Bấm Next.

4\. Ở phần Assign access to, chọn User, group, or service principal. Bấm Select members, gõ tìm tên terraform-bot rồi chọn nó.

5\. Bấm Review + assign.





TỔNG KẾT: CÁCH CẤU HÌNH ĐỂ TERRAFORM CHẠY

Sau khi đi qua đầy đủ 5 bước thiết lập trên Azure Portal, bạn đã có trong tay 4 thông số cốt lõi sau:

1\. Subscription ID (Lấy từ mục Subscriptions)

2\. Tenant ID (Lấy từ bước tạo Bot)

3\. Client ID (Lấy từ bước tạo Bot)

4\. Client Secret (Mật khẩu của Bot lấy ở bước 4)



Linux

export ARM\_CLIENT\_ID="<Client\_ID\_cua\_bot>"

export ARM\_CLIENT\_SECRET="<Client\_Secret\_cua\_bot>"

export ARM\_TENANT\_ID="<Tenant\_ID\_cua\_bot>"

export ARM\_SUBSCRIPTION\_ID="<Subscription\_ID\_cua\_ban>"



Sau khi gán các biến môi trường này xong, bạn chỉ cần đứng tại thư mục có file main.tf và chạy các lệnh quen thuộc: terraform init, terraform plan, terraform apply.





Trong Microsoft Entra ID, việc phân biệt giữa App registrations và Enterprise applications rất dễ gây nhầm lẫn vì chúng thực chất là hai mặt của cùng một đồng xu.

Hiểu một cách đơn giản nhất bằng hình ảnh ẩn dụ:

• App Registration giống như Bản thiết kế (Blue print) hoặc Khuôn đúc.

• Enterprise Application giống như Sản phẩm thực tế được đúc ra từ khuôn đó và đang chạy trong nhà máy của bạn, kèm theo các cấu hình vận hành (ai được quyền vào, chính sách bảo mật là gì)





|Đặc điểm|App registrations (Đăng ký ứng dụng)|Enterprise applications (Ứng dụng doanh nghiệp)|
|-|-|-|
|Bản chất|Là khía cạnh Phát triển (Global/Template). Nơi bạn khai báo cho Azure biết: "Ứng dụng của tôi tên là gì, dùng cơ chế đăng nhập nào, cần xin những quyền gì từ hệ thống".|Là khía cạnh Vận hành (Local/Instance). Là thực thể thực tế (Service Principal) đang chạy trong hệ thống của bạn để người dùng hoặc các công cụ (như Terraform) sử dụng.|
|Đối tượng sử dụng chính|Lập trình viên (Developers) hoặc người cấu hình hệ thống ban đầu.|Quản trị viên hệ thống (IT Admins) để quản lý an ninh, phân quyền truy cập.|
|Quản trị viên hệ thống (IT Admins) để quản lý an ninh, phân quyền truy cập.|Chỉ nằm tại Tenant gốc (nơi ứng dụng được tạo ra).|Xuất hiện ở tất cả các Tenant nào chấp nhận sử dụng ứng dụng này (đối với ứng dụng đa tổ chức - Multi-tenant).|
|Hành động phổ biến|• Tạo mới một ứng dụng/bot.<br />• Tạo và lấy Client Secret, Certificate.<br />• Cấu hình URL chuyển hướng đăng nhập (Redirect URI).|• Gán quyền cho User/Group cụ thể nào được phép đăng nhập vào ứng dụng.<br />• Cấu hình Đăng nhập một lần (SSO - Single Sign-On).<br />• Xem nhật ký lịch sử đăng nhập (Sign-in logs) của ứng dụng.|



trong trường hợp Terraform của bạn?

Khi bạn thực hiện Bước 4 ở câu trả lời trước (vào App registrations bấm New registration tên là terraform-bot), Microsoft Entra ID sẽ tự động làm 2 việc cùng lúc:

1\. Tạo ra một bản ghi trong App registrations (Nơi bạn vào lấy Client Secret).

2\. Tự động sinh ra một bản ghi tương ứng mang tên terraform-bot nằm bên mục Enterprise applications (Đóng vai trò là đại diện pháp lý - Service Principal - để bạn mang đi phân quyền Contributor trong Subscription ở Bước 5).



Khi nào bạn cần vào Enterprise applications một cách chủ động?

Thông thường, khi bạn tích hợp các ứng dụng có sẵn của bên thứ ba (như Zoom, Salesforce, GitHub...) vào hệ thống của công ty để nhân viên đăng nhập bằng tài khoản Microsoft, các ứng dụng đó sẽ nhảy thẳng vào mục Enterprise applications. Bạn sẽ vào đây để quản lý xem nhân viên nào được phép dùng Zoom, nhân viên nào không, chứ bạn không cần phải tự tay "thiết kế" ra chúng ở mục App registrations.

