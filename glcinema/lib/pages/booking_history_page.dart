import 'dart:convert';
import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;
import 'package:shared_preferences/shared_preferences.dart';

import '../services/api_config.dart';

class BookingHistoryPage extends StatefulWidget {
  const BookingHistoryPage({super.key});

  @override
  State<BookingHistoryPage> createState() => _BookingHistoryPageState();
}

class _BookingHistoryPageState extends State<BookingHistoryPage> {
  bool _loading = true;
  String? _error;
  List<Map<String, dynamic>> _items = [];

  Map<String, String> get _headers => const {
    'Accept': 'application/json',
    'Content-Type': 'application/json; charset=utf-8',
  };

  @override
  void initState() {
    super.initState();
    _load();
  }

  String _getStr(Map<String, dynamic> m, String k) => (m[k] ?? '').toString();

  List<dynamic>? _decodeList(dynamic decoded) {
    if (decoded is Map) {
      final items = decoded['items'] ?? decoded['data'] ?? decoded['result'];
      if (items is List) return items;
    }
    if (decoded is List) return decoded;
    return null;
  }

  Future<String> _getUserIdOrEmpty() async {
    final prefs = await SharedPreferences.getInstance();
    return (prefs.getString('customer_user_id') ??
        prefs.getString('userId') ??
        prefs.getString('user_id') ??
        '')
        .trim();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
      _items = [];
    });

    try {
      final userId = await _getUserIdOrEmpty();
      if (userId.isEmpty) {
        setState(() {
          _loading = false;
          _error = 'Thiếu userId';
        });
        return;
      }

      final uri = Uri.parse('${ApiConfig.apiBase}/booking/history').replace(
      queryParameters: {
          'userId': userId,
          'page': '1',
          'pageSize': '50',
        },
      );

      final http.Response r =
      await http.get(uri, headers: _headers).timeout(const Duration(seconds: 15));

      if (r.statusCode != 200) {
        setState(() {
          _loading = false;
          _error = 'HTTP ${r.statusCode}: ${r.body}';
        });
        return;
      }

      final decoded = jsonDecode(r.body);
      final list = _decodeList(decoded);
      if (list == null) {
        setState(() {
          _loading = false;
          _error = 'Body không đúng format JSON list/items';
        });
        return;
      }

      final mapped = list
          .where((e) => e is Map)
          .map((e) => Map<String, dynamic>.from(e as Map))
          .toList();

      setState(() {
        _loading = false;
        _items = mapped;
      });
    } catch (e) {
      setState(() {
        _loading = false;
        _error = 'Exception: $e';
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Lịch sử đặt vé')),
      body: _loading
          ? const Center(child: CircularProgressIndicator())
          : (_error != null)
          ? Center(child: Text(_error!, textAlign: TextAlign.center))
          : (_items.isEmpty)
          ? const Center(child: Text('Không có dữ liệu'))
          : ListView.separated(
        padding: const EdgeInsets.all(12),
        itemCount: _items.length,
        separatorBuilder: (_, __) => const SizedBox(height: 10),
        itemBuilder: (context, i) {
          final it = _items[i];
          final invoiceId = _getStr(it, 'invoiceId');
          final movieTitle = _getStr(it, 'movieTitle');
          final room = _getStr(it, 'room');
          final totalPrice = _getStr(it, 'totalPrice');

          return Card(
            elevation: 0,
            shape: RoundedRectangleBorder(
              borderRadius: BorderRadius.circular(14),
              side: BorderSide(color: Colors.black.withOpacity(0.08)),
            ),
            child: ListTile(
              title: Text(movieTitle.isEmpty ? 'N/A' : movieTitle),
              subtitle: Text('Invoice: $invoiceId\nPhòng: $room'),
              trailing: Text(totalPrice),
            ),
          );
        },
      ),
    );
  }
}
