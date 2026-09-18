import 'dart:convert';

import 'package:http/http.dart' as http;

import '../Authentication/auth_session.dart';

const _apiBase = 'http://127.0.0.1:5142';

class AdminUser {
  final int id;
  final String email;
  final String role;

  AdminUser({required this.id, required this.email, required this.role});

  factory AdminUser.fromJson(Map<String, dynamic> json) => AdminUser(
        id: json['id'] as int,
        email: json['email'] as String,
        role: json['role'] as String,
      );
}

class UserRepository {
  Map<String, String> get _authHeaders => {
        'Content-Type': 'application/json',
        'Authorization': 'Bearer ${AuthSession.token}',
      };

  Future<http.Response> register(String email, String password) => http.post(
        Uri.parse('$_apiBase/auth/register'),
        headers: const {'Content-Type': 'application/json'},
        body: jsonEncode({'email': email, 'password': password}),
      );

  Future<http.Response> fetchUsers() =>
      http.get(Uri.parse('$_apiBase/admin/users'), headers: _authHeaders);

  List<AdminUser> parseUsers(String body) =>
      (jsonDecode(body) as List).map((e) => AdminUser.fromJson(e)).toList();

  Future<http.Response> updateEmail(int id, String email) => http.put(
        Uri.parse('$_apiBase/admin/users/$id/email'),
        headers: _authHeaders,
        body: jsonEncode({'email': email}),
      );

  Future<http.Response> updatePassword(int id, String password) => http.put(
        Uri.parse('$_apiBase/admin/users/$id/password'),
        headers: _authHeaders,
        body: jsonEncode({'password': password}),
      );

  Future<http.Response> updateRole(int id, String role) => http.put(
        Uri.parse('$_apiBase/admin/users/$id/role'),
        headers: _authHeaders,
        body: jsonEncode({'role': role}),
      );

  Future<http.Response> deleteUser(int id) =>
      http.delete(Uri.parse('$_apiBase/admin/users/$id'), headers: _authHeaders);
}
