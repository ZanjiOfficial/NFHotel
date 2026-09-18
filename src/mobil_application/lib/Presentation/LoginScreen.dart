import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;
import '../Authentication/auth_session.dart';
import 'package:flutter_login/flutter_login.dart';
import 'package:google_fonts/google_fonts.dart';

import '../theme.dart';
import 'RoomList.dart';

//Fandt flutter_login https://pub.dev/packages/flutter_login#-installing-tab-
//
// Android emulator -> host machine is 10.0.2.2; iOS sim / desktop / web -> localhost/127.0.0.1
const _apiBase = 'http://127.0.0.1:5142'; //port 5142 is the default port for the LoginApi project; use 10.0.2.2 instead if running on an Android emulator

final users = {'user@example.com': '12345', 'user2@example.com': '54321'};


//login logic
class LoginScreen extends StatelessWidget {
  const LoginScreen({super.key});

  Duration get loginTime => const Duration(milliseconds: 300);

  Future<String?> _authUser(LoginData data) async {
    try {
      final res = await http.post(
        Uri.parse('$_apiBase/auth/login'),
        headers: {'Content-Type': 'application/json'},
        body: jsonEncode({'email': data.name, 'password': data.password}),
      );
      if (res.statusCode == 200) {
        AuthSession.token = jsonDecode(res.body)['accessToken'] as String;
        return null; // Login successful
      }
      if (res.statusCode == 401) {
        return 'Invalid username or password'; // Unauthorized
      }
      return 'An error occurred'; // Other error
    } catch (e) {
      return 'Could not reach server';
    }
  }

  Future<String?> _signupUser(SignupData data) async {
    try {
      final res = await http.post(
        Uri.parse('$_apiBase/auth/register'),
        headers: {'Content-Type': 'application/json'},
        body: jsonEncode({'email': data.name, 'password': data.password}),
      );
      if (res.statusCode == 200) return null; // Signup successful
      if (res.statusCode == 409) return 'Email already registered';
      return 'An error occurred';
    } catch (e) {
      return 'Could not reach server';
    }
  }

  Future<String?> _recoverPassword(String name) {
    debugPrint('name: $name');
    return Future.delayed(loginTime).then((_) {
      if (!users.containsKey(name)) {
        return 'User not found';
      }
      return null;
    });
  }

  @override
  Widget build(BuildContext context) {
    return FlutterLogin(
      title: 'NFHotel',
      logo: AssetImage('assets/logo.png'),
      onLogin: _authUser,
      onSignup: _signupUser,
      onSubmitAnimationCompleted: () {
        Navigator.of(
          context,
        ).pushReplacement(
          MaterialPageRoute(builder: (context) => const RoomOverview()),
        );
      },
      onRecoverPassword: _recoverPassword,
      theme: LoginTheme(
        primaryColor: AppColors.navy,
        accentColor: AppColors.amber,
        pageColorLight: AppColors.navy,
        pageColorDark: AppColors.navyDark,
        titleStyle: GoogleFonts.playfairDisplay(
          fontSize: 32,
          fontWeight: FontWeight.bold,
          color: Colors.white,
        ),
        bodyStyle: GoogleFonts.inter(color: AppColors.textMuted),
        buttonStyle: GoogleFonts.inter(
          fontWeight: FontWeight.w600,
          color: AppColors.navyDark,
        ),
        cardTheme: CardTheme(
          color: Colors.white,
          elevation: 0,
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(16),
          ),
        ),
        buttonTheme: LoginButtonTheme(
          backgroundColor: AppColors.amber,
          elevation: 0,
          highlightElevation: 0,
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(8),
          ),
        ),
      ),
    );
  }
}
