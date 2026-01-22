# 🎬 CinemaS

**CinemaS** là ứng dụng web **ASP.NET Core MVC (.NET 9)** phục vụ **đặt vé xem phim**, **gọi combo/snack**, và **quản lý thành viên/điểm thưởng**. Hệ thống sử dụng **ASP.NET Identity** để xác thực & phân quyền, tích hợp **VNPAY** cho thanh toán, và cung cấp **cổng quản trị** để vận hành dữ liệu rạp, phim, suất chiếu, giá vé, khuyến mãi.
**CinemaS** là dự án quản lý rạp phim gồm **Web App (ASP.NET Core MVC .NET 9)** và **Mobile App (Flutter)**, phục vụ **đặt vé xem phim**, **gọi combo/snack**, **quản lý thành viên/điểm thưởng**, và **vận hành nội bộ**. Hệ thống Web tích hợp **ASP.NET Identity** cho xác thực & phân quyền, **VNPAY** cho thanh toán; Mobile App đóng vai trò kênh đặt vé cho khách hàng.

---

## Table of Contents

1. [Overview](#-overview)  
2. [Features](#-features)  
3. [Project Structure](#%EF%B8%8F-project-structure)  
4. [Technology Stack](#-technology-stack)  
5. [Prerequisites](#%EF%B8%8F-prerequisites)  
6. [Configuration](#-configuration)  
7. [Database Setup](#%EF%B8%8F-database-setup)  
8. [Getting Started](#-getting-started)  
9. [Running the Application](#%EF%B8%8F-running-the-application)  
9. [Running the Applications](#%EF%B8%8F-running-the-applications)  
10. [Development Tips](#-development-tips)  
11. [Roadmap](#%EF%B8%8F-roadmap)

---

## 📘 Overview

CinemaS hỗ trợ quản lý và vận hành quy trình đặt vé theo các bước: **khám phá phim → chọn suất chiếu → chọn ghế → chọn loại vé → thêm snack → thanh toán → tạo hóa đơn & theo dõi giao dịch**.  
Hệ thống phân tách rõ nghiệp vụ người dùng và nghiệp vụ quản trị thông qua **role-based authorization (Admin/User)**, phù hợp triển khai cho mô hình rạp chiếu có nhiều phòng chiếu, sơ đồ ghế, và chính sách giá/khuyến mãi đa dạng.

---

## ✨ Features

### 🌐 Web App (ASP.NET Core MVC)

- 🎞️ **Movie discovery & showtimes**: quản lý phim, thể loại, người tham gia, suất chiếu, cụm rạp/phòng chiếu, sơ đồ ghế.  
- 🎟️ **Ticketing flow**: đặt ghế, chọn loại vé, thêm snack/combo, tạo **invoice** và theo dõi trạng thái giao dịch.  
- 💳 **Payments (VNPAY)**: tích hợp VNPAY, cấu hình gateway theo `TmnCode`, `HashSecret`, URL thanh toán/return.  
- 👤 **Accounts & roles**: ASP.NET Identity, phân quyền Admin/User, hỗ trợ xác thực email.  
- 🏅 **Loyalty & promotions**: hạng thành viên, điểm thưởng, khuyến mãi; cấu hình loại ghế/loại vé.  
- 🛠️ **Admin portal**: CRUD dữ liệu vận hành (phim, suất chiếu, giá, khuyến mãi, trạng thái, phân quyền).

### 📱 Mobile App (Flutter)

- ✅ **Đặt vé nhanh** trên thiết bị di động.  
- 🎬 **Duyệt phim & suất chiếu** với luồng đặt vé thân thiện người dùng.  
- 🔔 **Trải nghiệm khách hàng** gọn nhẹ, hỗ trợ triển khai đa nền tảng (iOS/Android).

---

## 🗂️ Project Structure

```
CinemaS/
├── Controllers/
├── Models/
├── Services/
├── VNPAY/
├── Views/
├── wwwroot/
├── Migrations/
├── appsettings.json
└── Program.cs
.
├── CinemaS/                 # Web App (ASP.NET Core MVC)
├── glcinema/                # Mobile App (Flutter)
├── CinemaS.sln
└── README.md
```

---

## 🧰 Technology Stack

### Web App

- ⚙️ **.NET 9 / ASP.NET Core MVC**  
- 🗄️ **Entity Framework Core + SQL Server**  
- 🔐 **ASP.NET Identity**  
- 💳 **VNPAY**  
- ✉️ **SMTP Email (Gmail)**  
- ✉️ **SMTP Email (Gmail)**

### Mobile App

- 📱 **Flutter** (Android/iOS)

---

## ⚙️ Prerequisites

### Web App

- .NET SDK 9.0+  
- SQL Server  
- SMTP account  
- SMTP account

### Mobile App

- Flutter SDK  
- Android Studio/Xcode (tùy nền tảng)

---

## 🔧 Configuration

### Web App

- `ConnectionStrings:CinemaS`
- `EmailSettings`
- `VnPay`

### Mobile App

- Cấu hình môi trường Flutter và các thiết lập theo yêu cầu build/run.

---

## 🗄️ Database Setup

```bash
dotnet ef database update
```

---

## 🚀 Getting Started

### Web App

```bash
dotnet restore
dotnet run
```

### Mobile App

```bash
cd glcinema
flutter pub get
flutter run
```

---

## ▶️ Running the Application
## ▶️ Running the Applications

Ứng dụng chạy theo ASP.NET URLs đã cấu hình (mặc định HTTPS).
- **Web App**: chạy theo ASP.NET URLs đã cấu hình (mặc định HTTPS).  
- **Mobile App**: chạy thông qua thiết bị/emulator do Flutter quản lý.

---

## 🧪 Development Tips

- Sử dụng ASP.NET Identity UI mặc định.  
- Kiểm tra mapping static assets trong `wwwroot`.
- Kiểm tra mapping static assets trong `wwwroot`.  
- Flutter: đảm bảo setup device/emulator trước khi chạy.

---

## 🛣️ Roadmap

- Policy-based authorization  
- REST API  
- Dashboard thống kê nâng cao
