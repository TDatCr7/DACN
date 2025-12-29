// lib/pages/home_page.dart (FULL FILE) - remove menu bên phải + fix mở lịch sử (userId String)

import 'dart:convert';
import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;
import 'package:carousel_slider/carousel_slider.dart';
import 'package:shared_preferences/shared_preferences.dart';

import '../services/api_config.dart';
import 'order_history_page.dart';
import 'booking_history_page.dart';
import 'showtime_page.dart';
import 'login_page.dart';

class BannerItem {
  final String type;
  final String movieId;
  final String title;
  final String imageUrl;

  const BannerItem({
    required this.type,
    required this.movieId,
    required this.title,
    required this.imageUrl,
  });

  static String _s(dynamic v) => (v ?? '').toString();

  factory BannerItem.fromApi(Map<String, dynamic> m) {
    return BannerItem(
      type: _s(m['type']),
      movieId: _s(m['movieId']),
      title: _s(m['title']),
      imageUrl: _s(m['imageUrl'] ?? m['bannerUrl'] ?? m['imagePath']),
    );
  }

  String get imageResolved => ApiConfig.fileUrl(imageUrl);
}

class Movie {
  final String id;
  final String title;
  final String posterUrl;
  final String summary;
  final int durationMin;
  final String releaseDate;
  final bool isNow;

  final String ageRating;
  final String genres;

  const Movie({
    required this.id,
    required this.title,
    required this.posterUrl,
    required this.summary,
    required this.durationMin,
    required this.releaseDate,
    required this.isNow,
    this.ageRating = '',
    this.genres = '',
  });

  static String _str(dynamic v) => (v ?? '').toString();

  factory Movie.fromApi(Map<String, dynamic> m) {
    final statusRaw = m['status'] ?? m['Status'] ?? 0;

    bool isNow;
    if (statusRaw is bool) {
      isNow = statusRaw;
    } else if (statusRaw is int) {
      isNow = statusRaw == 1;
    } else {
      final s = statusRaw.toString().trim().toLowerCase();
      isNow = (s == '1' || s == 'true' || s == 'now');
    }

    return Movie(
      id: _str(m['movieId'] ?? m['MoviesId'] ?? m['id']),
      title: _str(m['title'] ?? m['Title']),
      posterUrl: _str(m['posterUrl'] ?? m['PosterImage'] ?? m['Poster']),
      summary: _str(m['summary'] ?? m['Summary']),
      durationMin: int.tryParse(_str(m['durationMin'] ?? m['Duration'])) ?? 0,
      releaseDate: _str(m['releaseDate'] ?? m['ReleaseDate']),
      isNow: isNow,
      ageRating: _str(m['ageRating'] ?? m['AgeRating'] ?? ''),
      genres: _str(m['genres'] ?? m['Genres'] ?? ''),
    );
  }

  String get posterResolved => ApiConfig.fileUrl(posterUrl);
}

class HomePage extends StatefulWidget {
  const HomePage({super.key});
  @override
  State<HomePage> createState() => _HomePageState();
}

enum _Tab { now, upcoming }

class _HomePageState extends State<HomePage> {
  bool _loading = true;
  String? _error;

  _Tab _tab = _Tab.now;

  int _bannerIndex = 0;
  int _movieIndex = 0;

  List<BannerItem> _banners = [];
  List<Movie> _now = [];
  List<Movie> _upcoming = [];

  // Drawer user info
  String _drawerName = '-';
  String _drawerEmail = '-';
  String _drawerUserId = '-';

  final GlobalKey<ScaffoldState> _scaffoldKey = GlobalKey<ScaffoldState>();

  Map<String, String> get _headers => const {
    'Accept': 'application/json',
    'Content-Type': 'application/json; charset=utf-8',
  };

  @override
  void initState() {
    super.initState();
    _bootstrap();
    _loadDrawerUserInfo();
  }

  Future<void> _loadDrawerUserInfo() async {
    final prefs = await SharedPreferences.getInstance();

    String firstNonEmpty(List<String?> vs, {String fallback = '-'}) {
      for (final v in vs) {
        final s = (v ?? '').trim();
        if (s.isNotEmpty) return s;
      }
      return fallback;
    }

    final name = firstNonEmpty([
      prefs.getString('name'),
      prefs.getString('fullName'),
      prefs.getString('username'),
      prefs.getString('user_name'),
    ]);

    final email = firstNonEmpty([
      prefs.getString('email'),
      prefs.getString('user_email'),
      prefs.getString('userEmail'),
    ]);

    // ✅ userId string (ưu tiên customer_user_id đã resolve)
    final userId = firstNonEmpty([
      prefs.getString('customer_user_id'),
      prefs.getString('userId'),
      prefs.getString('user_id'),
    ]);

    if (!mounted) return;
    setState(() {
      _drawerName = name;
      _drawerEmail = email;
      _drawerUserId = userId;
    });
  }

  Future<void> _logout() async {
    final prefs = await SharedPreferences.getInstance();

    await prefs.remove('token');
    await prefs.remove('user_id');
    await prefs.remove('userId');
    await prefs.remove('customer_user_id');
    await prefs.remove('email');
    await prefs.remove('user_email');
    await prefs.remove('userEmail');
    await prefs.remove('name');
    await prefs.remove('fullName');
    await prefs.remove('username');
    await prefs.remove('user_name');

    if (!mounted) return;

    Navigator.of(context).pushAndRemoveUntil(
      MaterialPageRoute(builder: (_) => const LoginPage()),
          (_) => false,
    );
  }

  Future<bool> _retryBool(Future<bool> Function() fn, {int maxTry = 6}) async {
    for (int i = 0; i < maxTry; i++) {
      final ok = await fn();
      if (ok) return true;
      final waitMs = (250 * (1 << i)).clamp(250, 3000);
      await Future.delayed(Duration(milliseconds: waitMs));
      if (!mounted) return false;
    }
    return false;
  }

  Future<bool> _pingHost() async {
    try {
      final r = await http.get(Uri.parse(ApiConfig.host)).timeout(const Duration(seconds: 2));
      return r.statusCode >= 200 && r.statusCode < 500;
    } catch (_) {
      return false;
    }
  }

  List<dynamic>? _decodeListBody(String body) {
    try {
      final decoded = jsonDecode(body);
      if (decoded is List) return decoded;
      if (decoded is Map) {
        final data = decoded['data'] ?? decoded['items'] ?? decoded['result'];
        if (data is List) return data;
      }
    } catch (_) {}
    return null;
  }

  Future<bool> _fetchBannersOnce() async {
    final u = '${ApiConfig.apiBase}/mobile/home/banners';
    try {
      final r = await http.get(Uri.parse(u), headers: _headers).timeout(const Duration(seconds: 8));
      if (r.statusCode != 200) return false;

      final list = _decodeListBody(r.body);
      if (list == null) return false;

      _banners = list
          .where((e) => e is Map)
          .map((e) => BannerItem.fromApi(Map<String, dynamic>.from(e as Map)))
          .where((b) => b.imageResolved.isNotEmpty)
          .toList();
      return true;
    } catch (_) {
      return false;
    }
  }

  Future<bool> _fetchMoviesOnce(_Tab tab) async {
    final t = tab == _Tab.now ? 'now' : 'upcoming';
    final u = '${ApiConfig.apiBase}/mobile/home?tab=$t';
    try {
      final r = await http.get(Uri.parse(u), headers: _headers).timeout(const Duration(seconds: 8));
      if (r.statusCode != 200) return false;

      final list = _decodeListBody(r.body);
      if (list == null) return false;

      final data = list
          .where((e) => e is Map)
          .map((e) => Movie.fromApi(Map<String, dynamic>.from(e as Map)))
          .toList();

      if (tab == _Tab.now) {
        _now = data;
      } else {
        _upcoming = data;
      }
      return true;
    } catch (_) {
      return false;
    }
  }

  Future<void> _bootstrap() async {
    if (!mounted) return;
    setState(() {
      _loading = true;
      _error = null;
    });

    final okPing = await _retryBool(_pingHost, maxTry: 8);
    if (!okPing) {
      if (!mounted) return;
      setState(() {
        _loading = false;
        _error = 'Không kết nối được API host: ${ApiConfig.host}';
      });
      return;
    }

    final okB = await _retryBool(_fetchBannersOnce, maxTry: 5);
    final okNow = await _retryBool(() => _fetchMoviesOnce(_Tab.now), maxTry: 6);
    final okUp = await _retryBool(() => _fetchMoviesOnce(_Tab.upcoming), maxTry: 6);

    if (!mounted) return;
    setState(() {
      _loading = false;
      _error = (okNow || okUp || okB) ? null : 'API trả rỗng hoặc sai endpoint.';
    });
  }

  Future<void> _openOrderHistory() async {
    final prefs = await SharedPreferences.getInstance();

    final uid = (prefs.getString('customer_user_id') ??
        prefs.getString('userId') ??
        prefs.getString('user_id') ??
        '')
        .trim();

    if (uid.isEmpty) return;

    if (!mounted) return;
    Navigator.push(
      context,
      MaterialPageRoute(builder: (_) => const BookingHistoryPage()),
    );
  }


  Drawer _buildUserDrawer() {
    final theme = Theme.of(context);

    return Drawer(
      child: SafeArea(
        child: Column(
          children: [
            Container(
              width: double.infinity,
              padding: const EdgeInsets.fromLTRB(16, 16, 16, 14),
              decoration: BoxDecoration(
                color: theme.colorScheme.surface,
                border: Border(
                  bottom: BorderSide(color: Colors.black.withOpacity(0.08)),
                ),
              ),
              child: Row(
                children: [
                  CircleAvatar(
                    radius: 26,
                    backgroundColor: theme.colorScheme.surfaceContainerHighest,
                    child: Icon(Icons.person, color: theme.colorScheme.onSurfaceVariant),
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          _drawerName,
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                          style: theme.textTheme.titleMedium?.copyWith(fontWeight: FontWeight.w900),
                        ),
                        const SizedBox(height: 4),
                        Text(
                          _drawerEmail,
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                          style: theme.textTheme.bodyMedium?.copyWith(
                            color: theme.textTheme.bodyMedium?.color?.withOpacity(0.75),
                            fontWeight: FontWeight.w600,
                          ),
                        ),
                        const SizedBox(height: 4),
                        Text(
                          'UserId: $_drawerUserId',
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                          style: theme.textTheme.bodySmall?.copyWith(
                            color: theme.textTheme.bodySmall?.color?.withOpacity(0.65),
                            fontWeight: FontWeight.w600,
                          ),
                        ),
                      ],
                    ),
                  ),
                ],
              ),
            ),
            ListTile(
              leading: const Icon(Icons.receipt_long),
              title: const Text('Lịch sử đặt vé'),
              onTap: () {
                Navigator.pop(context);
                _openOrderHistory();
              },
            ),
            const Spacer(),
            Padding(
              padding: const EdgeInsets.all(16),
              child: SizedBox(
                width: double.infinity,
                height: 48,
                child: ElevatedButton.icon(
                  style: ElevatedButton.styleFrom(
                    backgroundColor: const Color(0xFF111827),
                    shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
                  ),
                  onPressed: _logout,
                  icon: const Icon(Icons.logout, color: Colors.white),
                  label: const Text(
                    'Đăng xuất',
                    style: TextStyle(color: Colors.white, fontWeight: FontWeight.w800),
                  ),
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final purpleBg = const Color(0xFF3C1C63);

    if (_loading) {
      return const Scaffold(
        backgroundColor: Colors.black,
        body: Center(child: CircularProgressIndicator(color: Colors.deepPurple)),
      );
    }

    if (_error != null) {
      return Scaffold(
        backgroundColor: Colors.black,
        body: Center(
          child: Padding(
            padding: const EdgeInsets.all(16),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                Text(_error!, style: const TextStyle(color: Colors.white70)),
                const SizedBox(height: 10),
                Text(ApiConfig.apiBase, style: const TextStyle(color: Colors.white38, fontSize: 12)),
                const SizedBox(height: 14),
                ElevatedButton(onPressed: _bootstrap, child: const Text('Tải lại')),
              ],
            ),
          ),
        ),
      );
    }

    final movies = _tab == _Tab.now ? _now : _upcoming;

    return Scaffold(
      key: _scaffoldKey,
      backgroundColor: purpleBg,
      drawer: _buildUserDrawer(),
      body: SafeArea(
        child: Column(
          children: [
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 10),
              child: Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  IconButton(
                    onPressed: () {
                      _loadDrawerUserInfo();
                      _scaffoldKey.currentState?.openDrawer();
                    },
                    icon: const Icon(Icons.person, color: Colors.white),
                  ),
                  Column(
                    children: [
                      Image.asset(
                        'images/logo/img.png',
                        height: 46,
                        errorBuilder: (_, __, ___) => const Icon(Icons.local_movies, size: 46),
                      ),
                      const SizedBox(height: 6),
                    ],
                  ),

                  // ✅ REMOVE: menu bên phải. Giữ chỗ để layout không lệch.
                  const SizedBox(width: 48, height: 48),
                ],
              ),
            ),
            if (_banners.isNotEmpty)
              Padding(
                padding: const EdgeInsets.symmetric(horizontal: 14),
                child: ClipRRect(
                  borderRadius: BorderRadius.circular(14),
                  child: Stack(
                    children: [
                      CarouselSlider.builder(
                        itemCount: _banners.length,
                        itemBuilder: (context, index, realIndex) {
                          final b = _banners[index];
                          return Image.network(
                            b.imageResolved,
                            fit: BoxFit.cover,
                            width: double.infinity,
                            errorBuilder: (_, __, ___) => const Center(
                              child: Icon(Icons.broken_image, color: Colors.white70),
                            ),
                          );
                        },
                        options: CarouselOptions(
                          height: 150,
                          viewportFraction: 1,
                          autoPlay: true,
                          autoPlayInterval: const Duration(seconds: 4),
                          onPageChanged: (i, _) => setState(() => _bannerIndex = i),
                        ),
                      ),
                      Positioned(
                        bottom: 8,
                        left: 0,
                        right: 0,
                        child: Row(
                          mainAxisAlignment: MainAxisAlignment.center,
                          children: List.generate(_banners.length, (i) {
                            final active = i == _bannerIndex;
                            return Container(
                              width: 8,
                              height: 8,
                              margin: const EdgeInsets.symmetric(horizontal: 3),
                              decoration: BoxDecoration(
                                shape: BoxShape.circle,
                                color: active ? Colors.redAccent : Colors.white38,
                              ),
                            );
                          }),
                        ),
                      ),
                    ],
                  ),
                ),
              ),
            Padding(
              padding: const EdgeInsets.symmetric(vertical: 12),
              child: Row(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  _tabButton("Đang chiếu", _tab == _Tab.now, () {
                    setState(() {
                      _tab = _Tab.now;
                      _movieIndex = 0;
                    });
                  }),
                  const SizedBox(width: 22),
                  _tabButton("Sắp chiếu", _tab == _Tab.upcoming, () {
                    setState(() {
                      _tab = _Tab.upcoming;
                      _movieIndex = 0;
                    });
                  }),
                ],
              ),
            ),
            Expanded(
              child: movies.isEmpty
                  ? Center(
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    const Text("Không có phim", style: TextStyle(color: Colors.white70)),
                    const SizedBox(height: 10),
                    ElevatedButton(onPressed: _bootstrap, child: const Text('Tải lại')),
                  ],
                ),
              )
                  : Column(
                children: [
                  Expanded(
                    child: CarouselSlider.builder(
                      itemCount: movies.length,
                      itemBuilder: (context, index, realIndex) {
                        final movie = movies[index];
                        return Padding(
                          padding: const EdgeInsets.symmetric(horizontal: 10),
                          child: ClipRRect(
                            borderRadius: BorderRadius.circular(14),
                            child: Image.network(
                              movie.posterResolved,
                              fit: BoxFit.cover,
                              width: double.infinity,
                              errorBuilder: (_, __, ___) => const Center(
                                child: Icon(Icons.broken_image, size: 80, color: Colors.white38),
                              ),
                            ),
                          ),
                        );
                      },
                      options: CarouselOptions(
                        enlargeCenterPage: true,
                        viewportFraction: 0.78,
                        height: double.infinity,
                        autoPlay: true,
                        autoPlayInterval: const Duration(seconds: 5),
                        onPageChanged: (i, _) => setState(() => _movieIndex = i),
                      ),
                    ),
                  ),
                  const SizedBox(height: 12),
                  _MovieInfoPanel(
                    movie: movies[_movieIndex.clamp(0, movies.length - 1)],
                    onBook: () {
                      final m = movies[_movieIndex.clamp(0, movies.length - 1)];
                      Navigator.push(
                        context,
                        MaterialPageRoute(
                          builder: (_) => ShowtimePage(movieId: m.id, movieTitle: m.title),
                        ),
                      );
                    },
                  ),
                  const SizedBox(height: 14),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _tabButton(String text, bool active, VoidCallback onTap) {
    return GestureDetector(
      onTap: onTap,
      child: Text(
        text,
        style: TextStyle(
          fontWeight: active ? FontWeight.w800 : FontWeight.w500,
          color: active ? Colors.white : Colors.white70,
        ),
      ),
    );
  }
}

class _MovieInfoPanel extends StatelessWidget {
  final Movie movie;
  final VoidCallback onBook;

  const _MovieInfoPanel({required this.movie, required this.onBook});

  @override
  Widget build(BuildContext context) {
    final panel = const Color(0xFF2B0F45);

    return Container(
      width: double.infinity,
      margin: const EdgeInsets.symmetric(horizontal: 14),
      padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
      decoration: BoxDecoration(
        color: panel,
        borderRadius: BorderRadius.circular(14),
        boxShadow: const [
          BoxShadow(
            blurRadius: 16,
            offset: Offset(0, 10),
            color: Color(0x33000000),
          ),
        ],
      ),
      child: Row(
        children: [
          Expanded(
            child: Text(
              movie.title,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: const TextStyle(
                color: Colors.white,
                fontWeight: FontWeight.w900,
                fontSize: 18,
              ),
            ),
          ),
          const SizedBox(width: 12),
          SizedBox(
            height: 42,
            child: ElevatedButton(
              onPressed: movie.isNow ? onBook : null,
              style: ElevatedButton.styleFrom(
                backgroundColor: const Color(0xFF6D32A8),
                disabledBackgroundColor: Colors.white24,
                shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
              ),
              child: const Padding(
                padding: EdgeInsets.symmetric(horizontal: 14),
                child: Text(
                  'ĐẶT VÉ',
                  style: TextStyle(
                    fontWeight: FontWeight.w900,
                    color: Color(0xFFFFD54D),
                  ),
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }
}
