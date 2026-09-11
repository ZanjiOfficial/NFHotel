import 'package:mobil_application/Model/Enums/roomStatus.dart';

class Room {
  final int number;
  RoomStatus status;

  Room({required this.number, required this.status});
}
