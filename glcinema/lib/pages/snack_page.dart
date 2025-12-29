import 'dart:convert';
import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;

import '../services/api_config.dart';
import 'invoice_confirm_page.dart';

class SnackPageArgs {
  final String showTimeId;
  final List<String> seatIds;
  final String? promoCode;
  SnackPageArgs({
    required this.showTimeId,
    required this.seatIds,
    this.promoCode,
  });
}

class SnackItemVm {
  final String snackId;
  final String name;
  final int price;
  final String? image;
  final String? description;

  SnackItemVm({
    required this.snackId,
    required this.name,
    required this.price,
    this.image,
    this.description,
  });

  factory SnackItemVm.fromJson(Map<String, dynamic> j) => SnackItemVm(
    snackId: (j['snackId'] ?? '').toString(),
    name: (j['name'] ?? '').toString(),
    price: (j['price'] ?? 0).round(),
    image: j['image']?.toString(),
    description: j['description']?.toString(),
  );
}

class SnackPage extends StatefulWidget {
  const SnackPage({super.key});

  @override
  State<SnackPage> createState() => _SnackPageState();
}

class _SnackPageState extends State<SnackPage> {
  late SnackPageArgs args;

  bool loading = true;
  String? error;
  List<SnackItemVm> snacks = [];

  final Map<String, int> selected = {};

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();

    final a = ModalRoute.of(context)?.settings.arguments;
    if (a is SnackPageArgs) {
      args = a;
    } else {
      final m = (a is Map) ? Map<String, dynamic>.from(a) : <String, dynamic>{};
      args = SnackPageArgs(
        showTimeId: (m['showtimeId'] ?? m['showTimeId'] ?? '').toString(),
        seatIds: (m['selectedSeatIds'] is List)
            ? (m['selectedSeatIds'] as List).map((e) => e.toString()).toList()
            : (m['seatIds'] is List)
            ? (m['seatIds'] as List).map((e) => e.toString()).toList()
            : <String>[],
        promoCode: (m['promoCode'] ?? m['PromoCode'])?.toString(),
      );
    }

    _loadSnacks();
  }

  Future<void> _loadSnacks() async {
    setState(() {
      loading = true;
      error = null;
    });

    try {
      final res = await http
          .get(Uri.parse('${ApiConfig.apiBase}/snacks'))
          .timeout(const Duration(seconds: 12));

      if (res.statusCode != 200) {
        throw Exception('HTTP ${res.statusCode}: ${res.body}');
      }

      final decoded = jsonDecode(res.body);

      // hỗ trợ cả 2 dạng: List hoặc {data:[...]}
      final List<dynamic> arr = (decoded is List)
          ? decoded
          : (decoded is Map && decoded['data'] is List)
          ? (decoded['data'] as List)
          : <dynamic>[];

      snacks = arr
          .where((e) => e is Map)
          .map((e) => SnackItemVm.fromJson(Map<String, dynamic>.from(e as Map)))
          .toList();

      setState(() => loading = false);
    } catch (e) {
      setState(() {
        loading = false;
        error = e.toString();
      });
    }
  }

  void _inc(String id) => setState(() => selected[id] = (selected[id] ?? 0) + 1);

  void _dec(String id) {
    setState(() {
      final cur = selected[id] ?? 0;
      if (cur <= 1) {
        selected.remove(id);
      } else {
        selected[id] = cur - 1;
      }
    });
  }

  int _snacksTotal() {
    int total = 0;
    for (final s in snacks) {
      total += s.price * (selected[s.snackId] ?? 0);
    }
    return total;
  }

  void _goPreviewInvoice() {
    final picked = selected.entries
        .where((e) => e.value > 0)
        .map((e) => {'snackId': e.key, 'quantity': e.value})
        .toList();

    Navigator.pushNamed(
      context,
      '/invoice_confirm',
      arguments: InvoiceConfirmArgs(
        showTimeId: args.showTimeId,
        seatIds: args.seatIds,
        snacks: picked,
        promoCode: args.promoCode,
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    if (loading) {
      return Scaffold(
        appBar: AppBar(title: const Text('Chọn snack')),
        body: const Center(child: CircularProgressIndicator()),
      );
    }

    if (error != null) {
      return Scaffold(
        appBar: AppBar(title: const Text('Chọn snack')),
        body: Center(child: Text(error!)),
      );
    }

    return Scaffold(
      appBar: AppBar(title: const Text('Chọn snack')),
      body: ListView.separated(
        padding: const EdgeInsets.all(12),
        itemBuilder: (ctx, i) {
          final s = snacks[i];
          final q = selected[s.snackId] ?? 0;

          // FIX: ApiConfig không có resolveUrl nữa -> dùng fileUrl
          final imgUrl = ApiConfig.fileUrl(s.image ?? '');

          return Container(
            padding: const EdgeInsets.all(12),
            decoration: BoxDecoration(
              borderRadius: BorderRadius.circular(14),
              border: Border.all(color: Colors.black12),
            ),
            child: Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                ClipRRect(
                  borderRadius: BorderRadius.circular(12),
                  child: imgUrl.isEmpty
                      ? Container(width: 64, height: 64, color: Colors.black12)
                      : Image.network(
                    imgUrl,
                    width: 64,
                    height: 64,
                    fit: BoxFit.cover,
                    loadingBuilder: (context, child, loadingProgress) {
                      if (loadingProgress == null) return child;
                      return Container(
                        width: 64,
                        height: 64,
                        alignment: Alignment.center,
                        child: const SizedBox(
                          width: 18,
                          height: 18,
                          child: CircularProgressIndicator(strokeWidth: 2),
                        ),
                      );
                    },
                    errorBuilder: (_, __, ___) =>
                        Container(width: 64, height: 64, color: Colors.black12),
                  ),
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        s.name,
                        style: const TextStyle(fontSize: 16, fontWeight: FontWeight.w600),
                      ),
                      const SizedBox(height: 6),
                      Text('${s.price} VND', style: const TextStyle(color: Colors.black54)),
                      if ((s.description ?? '').isNotEmpty) ...[
                        const SizedBox(height: 6),
                        Text(s.description!, style: const TextStyle(color: Colors.black54)),
                      ],
                    ],
                  ),
                ),
                const SizedBox(width: 12),
                Column(
                  children: [
                    IconButton(
                      onPressed: () => _inc(s.snackId),
                      icon: const Icon(Icons.add_circle_outline),
                    ),
                    Text('$q', style: const TextStyle(fontSize: 16, fontWeight: FontWeight.w600)),
                    IconButton(
                      onPressed: q == 0 ? null : () => _dec(s.snackId),
                      icon: const Icon(Icons.remove_circle_outline),
                    ),
                  ],
                ),
              ],
            ),
          );
        },
        separatorBuilder: (_, __) => const SizedBox(height: 10),
        itemCount: snacks.length,
      ),
      bottomNavigationBar: SafeArea(
        child: Container(
          padding: const EdgeInsets.all(12),
          decoration: const BoxDecoration(border: Border(top: BorderSide(color: Colors.black12))),
          child: Row(
            children: [
              Expanded(
                child: Text(
                  'Snack tạm tính: ${_snacksTotal()} VND',
                  style: const TextStyle(fontSize: 14, fontWeight: FontWeight.w600),
                ),
              ),
              ElevatedButton(
                onPressed: _goPreviewInvoice,
                child: const Text('Xem hóa đơn'),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
