import 'package:flutter/material.dart';

import 'package:mobil_application/Model/user_signup_view_model.dart';
import 'package:mobil_application/components/emaill_form_field.dart';

class SignupForm extends StatefulWidget {
  final GlobalKey<FormState> _formKey;
  final ValueChanged<UserSignupViewModel> _onChanged;
  final UserSignupViewModel _viewModel;

  SignupForm({
    Key? key,
    required this._formKey,
    required this._onChanged,
    required this._viewModel,
  }) : super(key: key);
  @override
  State<SignupForm> createState() => SignupFormState();
}

class SignupFormState extends State<SignupForm> {
  final emailFocus = FocusNode();
  final passwordFocus = FocusNode();
  final confirmPasswordFocus = FocusNode();

  @override
  void dispose() {
    emailFocus.dispose();
    passwordFocus.dispose();
    confirmPasswordFocus.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Form(
      key: widget._formKey,
      child: Column(
        children: [
          EmailFormField(
            focusNode: emailFocus,
            nextFocusNode: passwordFocus,
            onChanged: (s) =>
                widget._onChanged(widget._viewModel.copyWith(email: s)),
            currentValue: widget._viewModel.email,
            validator: widget._viewModel.emailValidator,
          ),
          PasswordFormField(
            focusNode: passwordFocus,
            nextFocusNode: confirmPasswordFocus,
            onChanged: (s) =>
                widget._onChanged(widget._viewModel.copyWith(password: s)),
            currentValue: widget._viewModel.password,
            validator: widget._viewModel.passwordValidator,
          ),
          ConfirmPasswordFormField(
            focusNode: confirmPasswordFocus,
            onChanged: (s) => widget._onChanged(
              widget._viewModel.copyWith(confirmPassword: s),
            ),
            currentValue: widget._viewModel.confirmPassword,
            validator: widget._viewModel.confirmPasswordValidator,
          ),
        ],
      ),
    );
  }
}
