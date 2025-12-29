import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;
import 'package:shared_preferences/shared_preferences.dart';
import 'dart:convert';
import '../services/auth_service.dart';

class CartModel extends ChangeNotifier {
  List<dynamic> _items = [];
  bool isLoading = false;
  String? _userId; // 🔹 Đổi về String

  final String baseUrl = "http://10.0.2.2:5080api";

  List<dynamic> get items => _items;
  String? get userId => _userId; // 🔹 Getter cũng kiểu String

  CartModel() {
    _initCart();
  }

  // 🔹 Khởi tạo cart (lấy userId và fetch dữ liệu từ server)
  Future<void> _initCart() async {
    //_userId = await AuthService.getUserId(); // trả về String?
    if (_userId != null && _userId!.isNotEmpty) {
      await fetchCart();
    }
  }

  // 🟢 Lấy giỏ hàng từ server
  Future<void> fetchCart() async {
    if (_userId == null || _userId!.isEmpty) return;
    try {
      isLoading = true;
      notifyListeners();

      final res = await http.get(Uri.parse("$baseUrl/cart/$_userId"));
      if (res.statusCode == 200) {
        final data = json.decode(res.body);
        if (data is List) {
          _items = data;
        } else {
          _items = data["items"] ?? [];
        }
      } else {
        print("❌ Không thể tải giỏ hàng: ${res.statusCode}");
      }
    } catch (e) {
      print("❌ Lỗi fetchCart: $e");
    } finally {
      isLoading = false;
      notifyListeners();
    }
  }

  // 🗑️ Xóa sản phẩm khỏi giỏ hàng (server)
  Future<void> removeItem(String cartItemId) async {
    try {
      final res = await http.delete(Uri.parse("$baseUrl/cart/item/$cartItemId"));
      if (res.statusCode == 200) {
        _items.removeWhere(
                (item) => item["Cart_Item_ID"].toString() == cartItemId);
        notifyListeners();
      } else {
        print("❌ Xóa thất bại (${res.statusCode})");
      }
    } catch (e) {
      print("❌ Lỗi removeItem: $e");
    }
  }

  // 💰 Tính tổng tiền
  int get totalPrice {
    int total = 0;
    for (var item in _items) {
      final price = (item["Line_Total"] ?? 0) as num;
      total += price.toInt();
    }
    return total;
  }

  // 🔴 Xóa toàn bộ giỏ hàng cục bộ (nếu cần)
  Future<void> clearLocal() async {
    _items.clear();
    notifyListeners();
  }

  // 🟣 Lưu giỏ hàng tạm vào SharedPreferences (offline cache)
  Future<void> saveLocal() async {
    try {
      final prefs = await SharedPreferences.getInstance();
      await prefs.setString('cached_cart', jsonEncode(_items));
    } catch (e) {
      print("❌ Lỗi lưu cache: $e");
    }
  }

  // 🟡 Tải lại giỏ hàng từ cache (khi offline)
  Future<void> loadLocal() async {
    try {
      final prefs = await SharedPreferences.getInstance();
      final saved = prefs.getString('cached_cart');
      if (saved != null) {
        _items = jsonDecode(saved);
        notifyListeners();
      }
    } catch (e) {
      print("❌ Lỗi tải cache: $e");
    }
  }
}
