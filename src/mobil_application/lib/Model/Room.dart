class Room {
  final String id;
  final String note;
  final int capacity;
  final bool isCleaned;
  final bool needsService;

  Room({
    required this.id,
    required this.note,
    required this.capacity,
    required this.isCleaned,
    required this.needsService,

  });

  factory Room.fromJson(Map<String, dynamic> json) {
    return Room(
      id: json['id'],
      note: json['note'],
      capacity: json['capacity'],
      isCleaned: json['isCleaned'],
      needsService: json['needsService']
    );
  }
}