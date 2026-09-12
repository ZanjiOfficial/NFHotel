import 'package:flutter/material.dart';

import 'Presentation/LoginScreen.dart';
import 'theme.dart';

void main() => runApp(MyApp());

class MyApp extends StatelessWidget {
  const MyApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(theme: buildAppTheme(), home: LoginScreen());
  }
}
