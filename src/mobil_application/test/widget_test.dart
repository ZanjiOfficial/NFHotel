// Basic widget test for the NFHotel app.

import 'package:flutter_test/flutter_test.dart';

import 'package:mobil_application/main.dart';

void main() {
  testWidgets('RoomOverview renders the room grid', (WidgetTester tester) async {
    // Build the app and trigger a frame.
    await tester.pumpWidget(MyApp());

    // The overview screen shows its title.
    expect(find.text('Room Overview'), findsOneWidget);

    // The first room tile is visible.
    expect(find.text('Room 1'), findsOneWidget);
  });
}
