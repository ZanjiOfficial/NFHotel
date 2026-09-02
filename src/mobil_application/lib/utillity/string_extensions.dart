//code "stolen" from https://stackoverflow.com/questions/16800540/how-should-i-check-if-the-input-is-an-email-address-in-flutter

import 'package:flutter/material.dart';

extension StringExtensions on String {
  bool isValidEmail() {
    return RegExp(r'^[\w-]+(\.[\w-]+)*@[\w-]+(\.[\w-]+)+$').hasMatch(this);
  }

  bool isWhiteSpace() {
    return this.trim().isEmpty;
  }

  bool isValidPassword() => this.length >= 8;
}
