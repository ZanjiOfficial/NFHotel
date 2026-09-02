import 'package:flutter/material.dart';
import 'package:mobil_application/utillity/string_extensions.dart';

//https://www.youtube.com/watch?v=osZ0cm9nvxM

class UserSignupViewModel {
  final String? email;
  final String? password;
  final String? confirmPassword;

  UserSignupViewModel({this.email, this.password, this.confirmPassword});

  factory UserSignupViewModel.newUser() {
    return UserSignupViewModel(
      email: null,
      password: null,
      confirmPassword: null,
    );
  }

  bool get emailIsValid => emailValidator(email) == null;
  bool get passwordIsValid => passwordValidator(password) == null;
  bool get confirmPasswordIsValid =>
      confirmPasswordValidator(confirmPassword, password) == null;
  bool get isFormValid =>
      emailIsValid && passwordIsValid && confirmPasswordIsValid;

  // validators
  String? emailValidator(String? email) {
    if (email == null || email.isWhiteSpace()) {
      return 'Email is required';
    }
    if (!email.contains('@')) {
      return 'Invalid email';
    }
    return null;
  }

  String? passwordValidator(String? password) {
    if (password == null || password.isWhiteSpace()) {
      return 'Password is required';
    }
    if (!password.isValidPassword()) {
      return 'Password must be at least 8 characters';
    }
    return null;
  }

  String? confirmPasswordValidator(String? confirmPassword, String? password) {
    if (confirmPassword == null || confirmPassword.isWhiteSpace()) {
      return 'Confirm password is required';
    }
    if (confirmPassword != password) {
      return 'Passwords do not match';
    }
    return null;
  }

  UserSignupViewModel copyWith({
    String? email,
    String? password,
    String? confirmPassword,
  }) {
    return UserSignupViewModel(
      email: email ?? this.email,
      password: password ?? this.password,
      confirmPassword: confirmPassword ?? this.confirmPassword,
    );
  }

  // DO NOT PRINT REAL PASSWORDS TO CONSOLE
  // or .. use them whenever not needed
  @override
  String toString() {
    return 'UserSignupViewModel(email: $email, password: $password, confirmPassword: $confirmPassword)';
  }
}
