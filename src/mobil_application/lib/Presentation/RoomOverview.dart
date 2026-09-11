import 'package:flutter/material.dart';

class Room {
  final int roomNumber;
  final String status;
  final String name;

  Room({required this.roomNumber, required this.status, required this.name});
}

final rooms = List.generate(
  12,
  (i) => Room(roomNumber: i + 1, name: 'Room ${i + 1}', status: 'available'),
);
