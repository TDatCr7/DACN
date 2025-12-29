
import 'dart:async';
import 'package:flutter/material.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:http/http.dart' as http;

import 'pages/home_page.dart';
import 'pages/login_page.dart';
import 'pages/register_page.dart';
import 'pages/cart_page.dart';

import 'pages/movie_detail_page.dart';
import 'pages/showtime_page.dart';
import 'pages/seat_selection_page.dart';

import 'pages/snack_page.dart';
import 'pages/invoice_confirm_page.dart';

import 'pages/payment_page.dart';
import 'pages/ticket_success_page.dart';
import 'pages/vnpay_webview_page.dart';

import 'pages/booking_history_page.dart';

import 'services/api_config.dart';

class AppRoutes {
  static const home = '/home';
  static const login = '/login';
  static const register = '/register';
  static const cart = '/cart';

  static const movieDetail = '/movie-detail';
  static const showtimes = '/showtimes';
  static const seats = '/seats';

  static const snacks = '/snacks';
  static const invoiceConfirm = '/invoice_confirm';
  static const vnpayWebview = '/vnpay_webview';

  static const payment = '/payment';
  static const ticketSuccess = '/ticket-success';

  static const bookingHistory = '/booking-history'; // ✅ NEW
}

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();

  final prefs = await SharedPreferences.getInstance();
  final token = (prefs.getString('token') ?? '').trim();

  // ✅ userId lưu dạng STRING (vd: "U001")
  final String userId = (prefs.getString('userId') ??
      prefs.getString('user_id') ??
      '')
      .trim();

  runApp(MyApp(
    isLoggedIn: token.isNotEmpty && userId.isNotEmpty,
    userId: userId.isEmpty ? null : userId,
  ));
}

class MyApp extends StatelessWidget {
  final bool isLoggedIn;
  final String? userId; // ✅ int? -> String?

  const MyApp({super.key, required this.isLoggedIn, required this.userId});

  static Map<String, dynamic> _asMap(Object? args) {
    if (args is Map) return Map<String, dynamic>.from(args);
    return <String, dynamic>{};
  }

  static String _s(Map<String, dynamic> m, String k, [String def = '']) =>
      (m[k] ?? def).toString();

  static int _i(Map<String, dynamic> m, String k, [int def = 0]) {
    final v = m[k];
    if (v is int) return v;
    return int.tryParse((v ?? def).toString()) ?? def;
  }

  static List<String> _listString(Map<String, dynamic> m, String k) {
    final v = m[k];
    if (v is List) return v.map((e) => e.toString()).toList();
    return <String>[];
  }

  static List<Map<String, dynamic>> _listMap(Map<String, dynamic> m, String k) {
    final v = m[k];
    if (v is List) {
      return v
          .where((e) => e is Map)
          .map((e) => Map<String, dynamic>.from(e as Map))
          .toList();
    }
    return <Map<String, dynamic>>[];
  }

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'Cinema App',
      debugShowCheckedModeBanner: false,
      theme: ThemeData(primarySwatch: Colors.deepPurple, useMaterial3: true),

      // luôn đi qua warmup
      home: _WarmupGate(
        isLoggedIn: isLoggedIn,
        userId: userId,
      ),

      routes: {
        AppRoutes.home: (_) => const HomePage(),
        AppRoutes.login: (_) => const LoginPage(),
        AppRoutes.register: (_) => const RegisterPage(),

        // ✅ CartPage nhận userId dạng String
        AppRoutes.cart: (_) => (userId != null && userId!.isNotEmpty)
            ? CartPage(userId: userId!)
            : const LoginPage(),


        AppRoutes.bookingHistory: (_) => const BookingHistoryPage(),
      },

      onGenerateRoute: (settings) {
        final m = _asMap(settings.arguments);

        switch (settings.name) {
          case AppRoutes.movieDetail:
            return MaterialPageRoute(
              settings: settings,
              builder: (_) => MovieDetailPage(
                movieId: _s(m, 'movieId'),
              ),
            );

          case AppRoutes.showtimes:
            return MaterialPageRoute(
              settings: settings,
              builder: (_) => ShowtimePage(
                movieId: _s(m, 'movieId'),
                movieTitle: _s(m, 'movieTitle'),
              ),
            );

          case AppRoutes.seats:
            return MaterialPageRoute(
              settings: settings,
              builder: (_) => SeatSelectionPage(
                movieTitle: _s(m, 'movieTitle'),
                showtimeId: _s(m, 'showtimeId'),
              ),
            );

          case AppRoutes.snacks:
            return MaterialPageRoute(
              settings: settings,
              builder: (_) => const SnackPage(),
            );

          case AppRoutes.invoiceConfirm:
            return MaterialPageRoute(
              settings: settings,
              builder: (_) => const InvoiceConfirmPage(),
            );

          case AppRoutes.vnpayWebview:
            return MaterialPageRoute(
              settings: settings,
              builder: (_) {
                final a = _asMap(settings.arguments);

                final String paymentUrl = _s(a, 'paymentUrl');
                final String orderId = _s(a, 'orderId');
                final String invoiceId = _s(a, 'invoiceId');
                final String uid = _s(a, 'userId'); // ✅ string

                return VNPayWebViewPage(
                  userId: uid.isEmpty ? null : uid,
                  orderId: orderId.isNotEmpty ? orderId : invoiceId,
                  paymentUrl: paymentUrl,
                );
              },
            );

          case AppRoutes.payment:
            return MaterialPageRoute(
              settings: settings,
              builder: (_) => PaymentPage(
                movieTitle: _s(m, 'movieTitle'),
                showtimeId: _s(m, 'showtimeId'),
                selectedSeatIds: _listString(m, 'selectedSeatIds'),
                snacks: _listMap(m, 'snacks'),
                grandTotal: _i(m, 'grandTotal', 0),
              ),
            );

          case AppRoutes.ticketSuccess:
            return MaterialPageRoute(
              settings: settings,
              builder: (_) => TicketSuccessPage(
                qrData: _s(m, 'qrData'),
                orderId: _s(m, 'orderId'),
                movieTitle: _s(m, 'movieTitle'),
              ),
            );
        }

        return null;
      },
    );
  }
}

class _WarmupGate extends StatefulWidget {
  final bool isLoggedIn;
  final String? userId; // ✅ int? -> String?

  const _WarmupGate({required this.isLoggedIn, required this.userId});

  @override
  State<_WarmupGate> createState() => _WarmupGateState();
}

class _WarmupGateState extends State<_WarmupGate> {
  bool _ready = false;
  String _status = 'Đang kết nối API...';

  @override
  void initState() {
    super.initState();
    _warmup();
  }

  Future<void> _warmup() async {
    final base = ApiConfig.apiBase;
    final host = base.replaceAll('/api', '');

    final okPing = await _retry(() => _ping(host), maxTry: 6);
    if (!okPing) {
      setState(() {
        _status = 'Không ping được API (vẫn vào app).';
        _ready = true;
      });
      return;
    }

    await _retry(() => _warmEndpoint('$base/snacks'), maxTry: 4);

    setState(() {
      _status = 'OK';
      _ready = true;
    });
  }

  Future<bool> _retry(Future<bool> Function() fn, {required int maxTry}) async {
    for (int i = 0; i < maxTry; i++) {
      final ok = await fn();
      if (ok) return true;
      final waitMs = 250 * (1 << i);
      await Future.delayed(Duration(milliseconds: waitMs.clamp(250, 3000)));
      if (!mounted) return false;
    }
    return false;
  }

  Future<bool> _ping(String host) async {
    try {
      final uri = Uri.parse(host);
      final r = await http.get(uri).timeout(const Duration(seconds: 2));
      return r.statusCode >= 200 && r.statusCode < 500;
    } catch (_) {
      return false;
    }
  }

  Future<bool> _warmEndpoint(String url) async {
    try {
      final uri = Uri.parse(url);
      final r = await http.get(uri).timeout(const Duration(seconds: 3));
      return r.statusCode >= 200 && r.statusCode < 500;
    } catch (_) {
      return false;
    }
  }

  @override
  Widget build(BuildContext context) {
    if (!_ready) {
      return Scaffold(
        backgroundColor: Colors.black,
        body: Center(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              const CircularProgressIndicator(color: Colors.deepPurple),
              const SizedBox(height: 14),
              Text(_status, style: const TextStyle(color: Colors.white70)),
              const SizedBox(height: 6),
              Text(
                ApiConfig.apiBase,
                style: const TextStyle(color: Colors.white38, fontSize: 12),
              ),
            ],
          ),
        ),
      );
    }

    return widget.isLoggedIn ? const HomePage() : const LoginPage();
  }
}
