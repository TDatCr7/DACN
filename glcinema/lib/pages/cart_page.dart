import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;
import 'dart:convert';

const String baseUrl = "http://10.0.2.2:5080/api";

class CartPage extends StatefulWidget {
  final String userId;
  const CartPage({super.key, required this.userId});

  @override
  State<CartPage> createState() => _CartPageState();
}

class _CartPageState extends State<CartPage> {
  List cartItems = [];
  bool isLoading = true;
  double total = 0;

  @override
  void initState() {
    super.initState();
    fetchCart();
  }

  Future<void> fetchCart() async {
    try {
      final res = await http.get(Uri.parse("$baseUrl/cart/${widget.userId}"));
      if (res.statusCode == 200) {
        final data = json.decode(res.body);
        setState(() {
          cartItems = (data["items"] ?? []) as List;
          total = double.tryParse(data["total"]?.toString() ?? "0") ?? 0;
          isLoading = false;
        });
      } else {
        throw Exception("Không thể tải giỏ hàng");
      }
    } catch (e) {
      print("❌ Lỗi fetchCart: $e");
      setState(() => isLoading = false);
    }
  }

  /// 🗑 Hộp thoại xác nhận xóa
  Future<void> confirmDelete(String cartItemId, String name) async {
    final shouldDelete = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text("Xác nhận xóa"),
        content: Text("Bạn có chắc muốn xóa '$name' khỏi giỏ hàng không?"),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context, false),
            child: const Text("Hủy"),
          ),
          ElevatedButton(
            style: ElevatedButton.styleFrom(
              backgroundColor: Colors.redAccent,
            ),
            onPressed: () => Navigator.pop(context, true),
            child: const Text("Xóa"),
          ),
        ],
      ),
    );

    if (shouldDelete == true) {
      removeFromCart(cartItemId);
    }
  }

  /// 🗑 Xóa sản phẩm khỏi giỏ hàng
  Future<void> removeFromCart(String cartItemId) async {
    try {
      final res = await http.delete(
        Uri.parse("$baseUrl/cart/item/$cartItemId"),
      );

      print("🗑 DELETE: $baseUrl/cart/item/$cartItemId");
      print("🔍 Status: ${res.statusCode}");
      print("🔍 Body: ${res.body}");

      if (res.statusCode == 200) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text("🗑 Đã xóa sản phẩm khỏi giỏ hàng"),
            backgroundColor: Colors.redAccent,
          ),
        );
        fetchCart(); // cập nhật lại danh sách
      } else {
        throw Exception("Không thể xóa sản phẩm");
      }
    } catch (e) {
      print("❌ Lỗi removeFromCart: $e");
    }
  }

  /// 💳 Thanh toán
  Future<void> checkout() async {
    try {
      final res = await http.post(
        Uri.parse("$baseUrl/cart/${widget.userId}/checkout"),
      );

      if (res.statusCode == 200) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text("✅ Thanh toán thành công!"),
            backgroundColor: Colors.green,
          ),
        );
        setState(() {
          cartItems.clear();
          total = 0;
        });
      } else {
        throw Exception("Thanh toán thất bại");
      }
    } catch (e) {
      print("❌ Lỗi checkout: $e");
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text("⚠️ Lỗi khi thanh toán"),
          backgroundColor: Colors.redAccent,
        ),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text("Giỏ hàng của bạn"),
        backgroundColor: Colors.redAccent,
        centerTitle: true,
      ),
      body: isLoading
          ? const Center(
        child: CircularProgressIndicator(color: Colors.redAccent),
      )
          : cartItems.isEmpty
          ? const Center(
        child: Text("🛒 Chưa có sản phẩm nào trong giỏ hàng."),
      )
          : SafeArea(
        child: Column(
          children: [
            // 🧾 Danh sách sản phẩm cuộn được
            Expanded(
              child: ListView.builder(
                padding: const EdgeInsets.all(8),
                itemCount: cartItems.length,
                itemBuilder: (context, index) {
                  final item = cartItems[index];
                  return Card(
                    margin: const EdgeInsets.symmetric(
                        horizontal: 8, vertical: 6),
                    child: ListTile(
                      leading: ClipRRect(
                        borderRadius: BorderRadius.circular(8),
                        child: Image.asset(
                          item["Image_URL"] ??
                              "assets/placeholder.png",
                          width: 60,
                          height: 60,
                          fit: BoxFit.cover,
                        ),
                      ),
                      title: Text(
                        item["Name"] ?? "Không có tên",
                        style: const TextStyle(
                            fontWeight: FontWeight.w600),
                      ),
                      subtitle: Text(
                        "Số lượng: ${item["Quantity"] ?? 1}",
                        style:
                        const TextStyle(color: Colors.black54),
                      ),
                      trailing: Column(
                        mainAxisAlignment:
                        MainAxisAlignment.spaceBetween,
                        crossAxisAlignment: CrossAxisAlignment.end,
                        children: [
                          Text(
                            "${item["Line_Total"] ?? 0} ₫",
                            style: const TextStyle(
                              color: Colors.redAccent,
                              fontWeight: FontWeight.bold,
                            ),
                          ),
                          IconButton(
                            icon: const Icon(Icons.delete,
                                color: Colors.grey),
                            tooltip: "Xóa sản phẩm",
                            onPressed: () => confirmDelete(
                              item["Cart_Item_ID"],
                              item["Name"] ?? "Sản phẩm",
                            ),
                          ),
                        ],
                      ),
                    ),
                  );
                },
              ),
            ),

            // 💰 Tổng cộng + Thanh toán (cố định cuối trang)
            Container(
              padding: const EdgeInsets.all(16),
              decoration: const BoxDecoration(
                color: Colors.white,
                boxShadow: [
                  BoxShadow(
                    color: Colors.black12,
                    offset: Offset(0, -1),
                    blurRadius: 6,
                  )
                ],
              ),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Row(
                    mainAxisAlignment:
                    MainAxisAlignment.spaceBetween,
                    children: [
                      const Text(
                        "Tổng cộng:",
                        style: TextStyle(
                            fontSize: 16,
                            fontWeight: FontWeight.bold),
                      ),
                      Text(
                        "${total.toStringAsFixed(0)} ₫",
                        style: const TextStyle(
                          color: Colors.redAccent,
                          fontSize: 18,
                          fontWeight: FontWeight.bold,
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: 10),
                  SizedBox(
                    width: double.infinity,
                    child: ElevatedButton.icon(
                      onPressed: checkout,
                      icon: const Icon(Icons.payment),
                      label: const Text("Thanh toán"),
                      style: ElevatedButton.styleFrom(
                        backgroundColor: Colors.redAccent,
                        padding: const EdgeInsets.symmetric(
                            vertical: 14),
                        shape: RoundedRectangleBorder(
                          borderRadius: BorderRadius.circular(8),
                        ),
                        textStyle: const TextStyle(fontSize: 16),
                      ),
                    ),
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}
