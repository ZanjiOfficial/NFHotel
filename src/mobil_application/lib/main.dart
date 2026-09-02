import 'package:flutter/material.dart';
import 'Presentation/RoomOverview.dart';

void main() {
  runApp(MyApp());
}

class MyApp extends StatelessWidget {
  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      home: RoomOverview(),
    );
  }
}