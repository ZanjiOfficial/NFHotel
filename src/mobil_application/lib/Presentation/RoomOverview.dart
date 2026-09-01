import 'package:flutter/material.dart';

class RoomOverview extends StatelessWidget {
  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: Text('Room Overview'),
      ),
      body: GridView.builder(
        gridDelegate: SliverGridDelegateWithFixedCrossAxisCount(
          crossAxisCount: 2,
          childAspectRatio: 1.0,
        ),
        itemCount: 12, // Replace with your actual room count
        itemBuilder: (context, index) {
          return Card(
            child: Center(
              child: Text('Room ${index + 1}'),
            ),
          );
        },
      ),
    );
  }
}