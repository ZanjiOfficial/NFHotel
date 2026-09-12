import 'package:flutter/material.dart';
import 'package:flutter_login/flutter_login.dart';
import 'package:google_fonts/google_fonts.dart';

import '../theme.dart';
import 'RoomList.dart';

//Fandt flutter_login https://pub.dev/packages/flutter_login#-installing-tab-
//

final users = {'user@example.com': '12345', 'user2@example.com': '54321'};

class LoginScreen extends StatelessWidget {
  const LoginScreen({super.key});

  Duration get loginTime => const Duration(milliseconds: 300);

  Future<String?> _authUser(LoginData data) {
    return Future.delayed(loginTime).then((_) {
      if (!users.containsKey(data.name)) {
        return 'User not found';
      }
      if (users[data.name] != data.password) {
        return 'Invalid password';
      }
      return null;
    });
  }

  Future<String?> _signupUser(SignupData data) {
    debugPrint('Signup data: $data');
    return Future.delayed(loginTime).then((_) {
      return null;
    });
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
