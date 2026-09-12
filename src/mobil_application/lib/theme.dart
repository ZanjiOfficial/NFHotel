import 'package:flutter/material.dart';
import 'package:google_fonts/google_fonts.dart';

/// Brand palette: dark navy + amber accent on a light, card-based layout.
class AppColors {
  AppColors._();

  static const navy = Color(0xFF16233F);
  static const navyDark = Color(0xFF0B1220);
  static const amber = Color(0xFFF2A73B);
  static const background = Color(0xFFF3F5F8);
  static const textMuted = Color(0xFF6B7280);
  static const border = Color(0xFFE2E8F0);
}

ThemeData buildAppTheme() {
  final base = ThemeData(useMaterial3: true, brightness: Brightness.light);
  final serif = GoogleFonts.playfairDisplayTextTheme(base.textTheme);

  TextStyle heading(TextStyle? style, {FontWeight weight = FontWeight.bold}) =>
      (style ?? const TextStyle()).copyWith(
        fontWeight: weight,
        color: AppColors.navyDark,
      );

  final roundedField = OutlineInputBorder(
    borderRadius: BorderRadius.circular(8),
    borderSide: const BorderSide(color: AppColors.border),
  );

  return base.copyWith(
    scaffoldBackgroundColor: AppColors.background,
    colorScheme: base.colorScheme.copyWith(
      primary: AppColors.navy,
      secondary: AppColors.amber,
      surface: Colors.white,
      error: const Color(0xFFB91C1C),
    ),
    textTheme: base.textTheme.copyWith(
      displayLarge: heading(serif.displayLarge),
      displayMedium: heading(serif.displayMedium),
      headlineLarge: heading(serif.headlineLarge),
      headlineMedium: heading(serif.headlineMedium),
      headlineSmall: heading(serif.headlineSmall),
      titleLarge: heading(serif.titleLarge, weight: FontWeight.w700),
      titleMedium: heading(serif.titleMedium, weight: FontWeight.w600),
      bodyLarge: GoogleFonts.inter(color: const Color(0xFF334155)),
      bodyMedium: GoogleFonts.inter(color: AppColors.textMuted),
      labelLarge: GoogleFonts.inter(
        fontWeight: FontWeight.w600,
        letterSpacing: 0.4,
      ),
    ),
    appBarTheme: AppBarTheme(
      backgroundColor: Colors.white,
      foregroundColor: AppColors.navyDark,
      elevation: 0,
      scrolledUnderElevation: 0,
      centerTitle: false,
      titleTextStyle: GoogleFonts.playfairDisplay(
        fontSize: 20,
        fontWeight: FontWeight.bold,
        color: AppColors.navyDark,
      ),
      surfaceTintColor: Colors.transparent,
    ),
    cardTheme: CardThemeData(
      color: Colors.white,
      elevation: 0,
      margin: EdgeInsets.zero,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(16),
        side: const BorderSide(color: AppColors.border),
      ),
    ),
    elevatedButtonTheme: ElevatedButtonThemeData(
      style: ElevatedButton.styleFrom(
        backgroundColor: AppColors.navy,
        foregroundColor: Colors.white,
        disabledBackgroundColor: AppColors.navy.withValues(alpha: 0.4),
        padding: const EdgeInsets.symmetric(horizontal: 22, vertical: 14),
        textStyle: GoogleFonts.inter(fontWeight: FontWeight.w600),
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(8)),
        elevation: 0,
      ),
    ),
    outlinedButtonTheme: OutlinedButtonThemeData(
      style: OutlinedButton.styleFrom(
        foregroundColor: AppColors.navy,
        side: const BorderSide(color: AppColors.navy),
        padding: const EdgeInsets.symmetric(horizontal: 22, vertical: 14),
        textStyle: GoogleFonts.inter(fontWeight: FontWeight.w600),
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(8)),
      ),
    ),
    textButtonTheme: TextButtonThemeData(
      style: TextButton.styleFrom(
        foregroundColor: AppColors.navy,
        textStyle: GoogleFonts.inter(fontWeight: FontWeight.w600),
      ),
    ),
    inputDecorationTheme: InputDecorationTheme(
      filled: true,
      fillColor: Colors.white,
      contentPadding: const EdgeInsets.symmetric(
        horizontal: 14,
        vertical: 14,
      ),
      border: roundedField,
      enabledBorder: roundedField,
      focusedBorder: roundedField.copyWith(
        borderSide: const BorderSide(color: AppColors.navy, width: 2),
      ),
      labelStyle: GoogleFonts.inter(
        color: AppColors.textMuted,
        fontWeight: FontWeight.w600,
      ),
    ),
    listTileTheme: ListTileThemeData(
      selectedTileColor: AppColors.navy.withValues(alpha: 0.06),
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
    ),
    dividerTheme: const DividerThemeData(color: AppColors.border),
  );
}
