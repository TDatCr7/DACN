# 🎬 CinemaS

**CinemaS** là ứng dụng web **ASP.NET Core MVC (.NET 9)** phục vụ **đặt vé xem phim**, **gọi combo/snack**, và **quản lý thành viên/điểm thưởng**. Hệ thống sử dụng **ASP.NET Identity** để xác thực & phân quyền, tích hợp **VNPAY** cho thanh toán, và cung cấp **cổng quản trị** để vận hành dữ liệu rạp, phim, suất chiếu, giá vé, khuyến mãi.

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
10. [Development Tips](#-development-tips)  
11. [Roadmap](#%EF%B8%8F-roadmap)

---

## 📘 Overview

CinemaS hỗ trợ quản lý và vận hành quy trình đặt vé theo các bước: **khám phá phim → chọn suất chiếu → chọn ghế → chọn loại vé → thêm snack → thanh toán → tạo hóa đơn & theo dõi giao dịch**.  
Hệ thống phân tách rõ nghiệp vụ người dùng và nghiệp vụ quản trị thông qua **role-based authorization (Admin/User)**, phù hợp triển khai cho mô hình rạp chiếu có nhiều phòng chiếu, sơ đồ ghế, và chính sách giá/khuyến mãi đa dạng.

---

## ✨ Features

- 🎞️ **Movie discovery & showtimes**: quản lý phim, thể loại, người tham gia, suất chiếu, cụm rạp/phòng chiếu, sơ đồ ghế.  
- 🎟️ **Ticketing flow**: đặt ghế, chọn loại vé, thêm snack/combo, tạo **invoice** và theo dõi trạng thái giao dịch.  
- 💳 **Payments (VNPAY)**: tích hợp VNPAY, cấu hình gateway theo `TmnCode`, `HashSecret`, URL thanh toán/return.  
- 👤 **Accounts & roles**: ASP.NET Identity, phân quyền Admin/User, hỗ trợ xác thực email.  
- 🏅 **Loyalty & promotions**: hạng thành viên, điểm thưởng, khuyến mãi; cấu hình loại ghế/loại vé.  
- 🛠️ **Admin portal**: CRUD dữ liệu vận hành (phim, suất chiếu, giá, khuyến mãi, trạng thái, phân quyền).

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
```

---

## 🧰 Technology Stack

- ⚙️ **.NET 9 / ASP.NET Core MVC**  
- 🗄️ **Entity Framework Core + SQL Server**  
- 🔐 **ASP.NET Identity**  
- 💳 **VNPAY**  
- ✉️ **SMTP Email (Gmail)**  

---

## ⚙️ Prerequisites

- .NET SDK 9.0+  
- SQL Server  
- SMTP account  

---

## 🔧 Configuration

- `ConnectionStrings:CinemaS`
- `EmailSettings`
- `VnPay`

---

## 🗄️ Database Setup

```bash
dotnet ef database update
```

---

## 🚀 Getting Started

```bash
dotnet restore
dotnet run
```

---

## ▶️ Running the Application

Ứng dụng chạy theo ASP.NET URLs đã cấu hình (mặc định HTTPS).

---

## 🧪 Development Tips

- Sử dụng ASP.NET Identity UI mặc định.  
- Kiểm tra mapping static assets trong `wwwroot`.

---

## 🛣️ Roadmap

- Policy-based authorization  
- REST API  
- Dashboard thống kê nâng cao
