// lib/pages/movie_detail_page.dart
// FIX:
// - Không dùng summary nữa (CHỈ detailDescription).
// - Trường hợp API chưa có / rỗng => "Đang cập nhật…"
// - Không overflow: bọc bằng ClipRect + Text ellipsis đúng chỗ.
// - Nút play ở GIỮA hero.

import 'dart:convert';
import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;
import 'package:url_launcher/url_launcher.dart';

import 'showtime_page.dart';

const String apiBase = "http://10.0.2.2:5080";

class MovieDetailPage extends StatefulWidget {
  final String movieId;
  const MovieDetailPage({super.key, required this.movieId});

  @override
  State<MovieDetailPage> createState() => _MovieDetailPageState();
}

class _MovieDetailPageState extends State<MovieDetailPage> {
  bool _loading = true;
  bool _expanded = false;
  Map<String, dynamic>? _data;

  Map<String, String> get _headers => const {
    'Accept': 'application/json',
    'Content-Type': 'application/json; charset=utf-8',
  };

  @override
  void initState() {
    super.initState();
    _fetch();
  }

  Future<void> _fetch() async {
    try {
      final url = Uri.parse('$apiBase/api/mobile/home/movie/${widget.movieId}');
      final r = await http.get(url, headers: _headers).timeout(const Duration(seconds: 20));
      if (r.statusCode == 200) {
        final decoded = jsonDecode(r.body);
        if (decoded is Map) _data = Map<String, dynamic>.from(decoded as Map);
      }
    } catch (_) {}
    if (!mounted) return;
    setState(() => _loading = false);
  }

  String _str(dynamic v) => (v ?? '').toString();
  int _int(dynamic v) => int.tryParse(_str(v)) ?? 0;

  String _resolve(String raw) {
    final p = raw.trim();
    if (p.isEmpty) return '';
    if (p.startsWith('http')) return p;
    if (p.startsWith('assets/')) return p;
    final normalized = p.startsWith('/') ? p.substring(1) : p;
    return '$apiBase/$normalized';
  }

  // ✅ CHỈ detailDescription
  String _detailDescriptionOnly(Map<String, dynamic> d) => _str(d['detailDescription']).trim();

  Future<void> _openTrailer(BuildContext context, String trailerUrl) async {
    final t = trailerUrl.trim();
    if (t.isEmpty) return;

    String? youTubeIdFromUrl(String url) {
      final u = Uri.tryParse(url);
      if (u == null) return null;
      if (u.host.contains('youtube.com') && u.path == '/watch') return u.queryParameters['v'];
      if (u.host.contains('youtu.be') && u.pathSegments.isNotEmpty) return u.pathSegments.first;
      if (u.host.contains('youtube.com') && u.pathSegments.length >= 2 && u.pathSegments.first == 'shorts') {
        return u.pathSegments[1];
      }
      return null;
    }

    final id = youTubeIdFromUrl(t);
    if (id != null) {
      final uriApp = Uri.parse('vnd.youtube:$id');
      if (await canLaunchUrl(uriApp)) {
        final ok = await launchUrl(uriApp, mode: LaunchMode.externalNonBrowserApplication);
        if (ok) return;
      }
    }

    final web = Uri.parse(t);
    if (await canLaunchUrl(web)) {
      await launchUrl(web, mode: LaunchMode.externalApplication);
    }
  }

  void _openShowtimes(BuildContext context, String movieId, String title) {
    Navigator.push(context, MaterialPageRoute(builder: (_) => ShowtimePage(movieId: movieId, movieTitle: title)));
  }

  @override
  Widget build(BuildContext context) {
    const bg = Color(0xFFF5F5F5);

    if (_loading) {
      return const Scaffold(backgroundColor: bg, body: Center(child: CircularProgressIndicator()));
    }

    final d = _data ?? <String, dynamic>{};

    final title = _str(d['title']);
    final poster = _resolve(_str(d['posterUrl']));
    final banner = _resolve(_str(d['bannerUrl']));
    final trailerUrl = _str(d['trailerUrl']);

    final durationMin = _int(d['durationMin']);
    final releaseDate = _str(d['releaseDate']);

    final director = _str(d['director']);
    final cast = _str(d['cast']);
    final ageRating = _str(d['ageRating']);
    final genres = _str(d['genres']);
    final languages = _str(d['languages']);

    final description = _detailDescriptionOnly(d); // ✅ ONLY
    final heroImage = banner.isNotEmpty ? banner : poster;

    return Scaffold(
      backgroundColor: bg,
      body: SafeArea(
        child: ClipRect(
          child: Stack(
            children: [
              ListView(
                padding: const EdgeInsets.fromLTRB(12, 8, 12, 96),
                children: [
                  ClipRRect(
                    borderRadius: BorderRadius.circular(18),
                    child: Stack(
                      alignment: Alignment.center,
                      children: [
                        AspectRatio(
                          aspectRatio: 16 / 9,
                          child: heroImage.isEmpty
                              ? Container(color: Colors.black12)
                              : Image.network(
                            heroImage,
                            fit: BoxFit.cover,
                            errorBuilder: (_, __, ___) => Container(color: Colors.black12),
                          ),
                        ),
                        Positioned.fill(child: Container(color: Colors.black.withOpacity(0.20))),
                        Positioned(
                          top: 10,
                          left: 10,
                          child: InkWell(
                            onTap: () => Navigator.pop(context),
                            borderRadius: BorderRadius.circular(999),
                            child: Container(
                              padding: const EdgeInsets.all(8),
                              decoration: BoxDecoration(color: Colors.black.withOpacity(0.35), shape: BoxShape.circle),
                              child: const Icon(Icons.arrow_back, color: Colors.white),
                            ),
                          ),
                        ),
                        InkWell(
                          onTap: trailerUrl.trim().isEmpty ? null : () => _openTrailer(context, trailerUrl),
                          borderRadius: BorderRadius.circular(999),
                          child: Container(
                            width: 64,
                            height: 64,
                            decoration: BoxDecoration(color: Colors.white.withOpacity(0.92), shape: BoxShape.circle),
                            child: const Icon(Icons.play_arrow_rounded, size: 42, color: Colors.black87),
                          ),
                        ),
                      ],
                    ),
                  ),
                  const SizedBox(height: 12),

                  Container(
                    padding: const EdgeInsets.all(12),
                    decoration: BoxDecoration(
                      color: Colors.white,
                      borderRadius: BorderRadius.circular(18),
                      border: Border.all(color: Colors.black12),
                    ),
                    child: Row(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        ClipRRect(
                          borderRadius: BorderRadius.circular(14),
                          child: poster.isEmpty
                              ? Container(width: 110, height: 150, color: Colors.black12)
                              : Image.network(
                            poster,
                            width: 110,
                            height: 150,
                            fit: BoxFit.cover,
                            errorBuilder: (_, __, ___) =>
                                Container(width: 110, height: 150, color: Colors.black12),
                          ),
                        ),
                        const SizedBox(width: 12),
                        Expanded(
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text(
                                title,
                                maxLines: 2,
                                overflow: TextOverflow.ellipsis,
                                style: const TextStyle(fontSize: 18, fontWeight: FontWeight.w900),
                              ),
                              const SizedBox(height: 10),
                              Wrap(
                                spacing: 10,
                                runSpacing: 8,
                                children: [
                                  _infoChip(
                                    icon: Icons.calendar_month,
                                    text: releaseDate.isNotEmpty ? _fmtDate(releaseDate) : '—',
                                  ),
                                  _infoChip(
                                    icon: Icons.schedule,
                                    text: durationMin > 0 ? _fmtDuration(durationMin) : '—',
                                  ),
                                ],
                              ),
                              const SizedBox(height: 12),
                              if (ageRating.isNotEmpty) _kv('Kiểm duyệt', ageRating),
                              if (genres.isNotEmpty) ...[const SizedBox(height: 8), _kv('Thể loại', genres)],
                              if (director.isNotEmpty) ...[const SizedBox(height: 8), _kv('Đạo diễn', director)],
                              if (cast.isNotEmpty) ...[const SizedBox(height: 8), _kv('Diễn viên', cast)],
                              if (languages.isNotEmpty) ...[const SizedBox(height: 8), _kv('Ngôn ngữ', languages)],
                            ],
                          ),
                        ),
                      ],
                    ),
                  ),

                  const SizedBox(height: 12),

                  Container(
                    width: double.infinity,
                    padding: const EdgeInsets.all(14),
                    decoration: BoxDecoration(
                      color: Colors.white,
                      borderRadius: BorderRadius.circular(18),
                      border: Border.all(color: Colors.black12),
                    ),
                    child: _expandableText(description),
                  ),
                ],
              ),

              Align(
                alignment: Alignment.bottomCenter,
                child: SafeArea(
                  child: Padding(
                    padding: const EdgeInsets.fromLTRB(12, 8, 12, 12),
                    child: SizedBox(
                      width: double.infinity,
                      height: 54,
                      child: ElevatedButton(
                        onPressed: () => _openShowtimes(context, widget.movieId, title),
                        style: ElevatedButton.styleFrom(
                          backgroundColor: const Color(0xFF6B36C9),
                          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
                          elevation: 0,
                        ),
                        child: const Text(
                          'ĐẶT VÉ',
                          style: TextStyle(
                            fontWeight: FontWeight.w900,
                            fontSize: 16,
                            letterSpacing: 1,
                            color: Color(0xFFFFD54D),
                          ),
                        ),
                      ),
                    ),
                  ),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _expandableText(String text) {
    final has = text.trim().isNotEmpty;
    final t = has ? text.trim() : 'Đang cập nhật…';
    final maxLines = _expanded ? 1000 : 6;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const Text('Nội dung phim', style: TextStyle(fontSize: 16, fontWeight: FontWeight.w900)),
        const SizedBox(height: 8),
        Text(
          t,
          maxLines: maxLines,
          overflow: TextOverflow.ellipsis,
          style: const TextStyle(fontSize: 15, height: 1.5, color: Colors.black87),
        ),
        if (has) ...[
          const SizedBox(height: 8),
          InkWell(
            onTap: () => setState(() => _expanded = !_expanded),
            child: Text(
              _expanded ? 'Thu gọn' : 'Xem thêm',
              style: const TextStyle(color: Color(0xFFB71C1C), fontWeight: FontWeight.w900),
            ),
          ),
        ],
      ],
    );
  }

  static Widget _kv(String k, String v) {
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        SizedBox(
          width: 86,
          child: Text(k, style: const TextStyle(fontSize: 14, fontWeight: FontWeight.w800)),
        ),
        const SizedBox(width: 10),
        Expanded(
          child: Text(
            v,
            softWrap: true,
            maxLines: 4,
            overflow: TextOverflow.ellipsis,
            style: const TextStyle(fontSize: 14, color: Colors.black87),
          ),
        ),
      ],
    );
  }

  static Widget _infoChip({required IconData icon, required String text}) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 8),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: Colors.black12),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(icon, size: 18, color: Colors.black54),
          const SizedBox(width: 6),
          Text(text, style: const TextStyle(fontWeight: FontWeight.w800, color: Colors.black87)),
        ],
      ),
    );
  }

  static String _fmtDuration(int minutes) {
    if (minutes <= 0) return '—';
    if (minutes < 60) return '$minutes phút';
    final h = minutes ~/ 60;
    final m = minutes % 60;
    return m == 0 ? '${h}giờ' : '${h}giờ ${m}phút';
  }

  static String _fmtDate(String yyyyMmDd) {
    final s = yyyyMmDd.trim();
    final parts = s.split('-');
    if (parts.length == 3) return '${parts[2]}/${parts[1]}/${parts[0]}';
    return s;
  }
}
