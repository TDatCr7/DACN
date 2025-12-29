import 'dart:convert';
import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;

import '../services/env.dart';
import 'order_detail_page.dart';

class OrderHistoryPage extends StatefulWidget {
  final String userId;
  const OrderHistoryPage({super.key, required this.userId});

  @override
  State<OrderHistoryPage> createState() => _OrderHistoryPageState();
}

class _OrderHistoryPageState extends State<OrderHistoryPage> {
  List<dynamic> items = [];
  bool loading = true;
  String? err;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    try {
      final r = await http.get(Uri.parse('${Env.baseUrl}/api/users/${widget.userId}/orders'));
      if (r.statusCode == 200) {
        setState(() {
          items = json.decode(r.body) as List<dynamic>;
          loading = false;
        });
      } else {
        setState(() {
          loading = false;
          err = 'HTTP ${r.statusCode}';
        });
      }
    } catch (e) {
      setState(() { loading = false; err = '$e'; });
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Lịch sử vé')),
      body: loading
          ? const Center(child: CircularProgressIndicator())
          : (err != null)
          ? Center(child: Text('Lỗi tải dữ liệu: $err'))
          : ListView.separated(
        padding: const EdgeInsets.all(12),
        itemBuilder: (_, i) {
          final x = items[i] as Map<String, dynamic>;
          final orderId = (x['orderId'] ?? '').toString();
          final title = (x['movieTitle'] ?? '').toString();
          final cinema = (x['cinemaName'] ?? '').toString();
          final screen = (x['screenName'] ?? '').toString();
          final startTime = (x['startTime'] ?? '').toString();
          final total = _asInt(x['total']);

          return ListTile(
            title: Text(title, style: const TextStyle(fontWeight: FontWeight.w600)),
            subtitle: Text(
              '$cinema - $screen\nSuất: ${_fmt(startTime)}\nGhế: ${x['seats'] ?? ""}',
            ),
            trailing: Text('${_currency(total)} đ',
                style: const TextStyle(fontWeight: FontWeight.w700)),
            onTap: () {
              if (orderId.isEmpty) return;
              Navigator.push(
                context,
                MaterialPageRoute(
                  builder: (_) => OrderDetailPage(orderId: orderId),
                ),
              );
            },
          );
        },
        separatorBuilder: (_, __) => const Divider(height: 1),
        itemCount: items.length,
      ),
    );
  }

  static String _fmt(String iso) {
    // server trả 'yyyy-MM-ddTHH:mm' → hiển thị 'dd/MM/yyyy - HH:mm'
    try {
      final dt = DateTime.parse(iso);
      final dd = dt.day.toString().padLeft(2, '0');
      final mm = dt.month.toString().padLeft(2, '0');
      return '$dd/$mm/${dt.year} - ${dt.hour.toString().padLeft(2, '0')}:${dt.minute.toString().padLeft(2, '0')}';
    } catch (_) {
      if (iso.length >= 16) return iso.substring(0,16).replaceFirst('T', ' ');
      return iso;
    }
  }

  static int _asInt(dynamic v) {
    if (v is int) return v;
    if (v is num) return v.toInt();
    if (v is String) return int.tryParse(v) ?? 0;
    return 0;
  }

  static String _currency(int v) =>
      v.toString().replaceAllMapped(RegExp(r'(\d)(?=(\d{3})+(?!\d))'), (m) => '${m[1]}.');
}
