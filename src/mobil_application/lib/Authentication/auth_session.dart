import 'dart:convert';

class AuthSession {
  static String? token;

  static const _roleClaim =
      'http://schemas.microsoft.com/ws/2008/06/identity/claims/role';

  static String? get role {
    final t = token;
    if (t == null) return null;
    final parts = t.split('.');
    if (parts.length != 3) return null;
    final payload = jsonDecode(
      utf8.decode(base64Url.decode(base64Url.normalize(parts[1]))),
    );
    return payload[_roleClaim] as String?;
  }

  static bool get isAdmin => role == 'admin';
}
