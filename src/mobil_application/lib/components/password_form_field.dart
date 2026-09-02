import 'package:flutter/material.dart';

class PasswordFormField extends StatefulWidget {
  final ValueChanged<String>? onChanged;
  final FormFieldValidator<String> validator;
  final String labelText;
  final String? currentValue;
  final FocusNode? focusNode;
  final FocusNode? nextFocusNode;

  const PasswordFormField({
    Key? key,
    required this.onChanged,
    required this.validator,
    required this.labelText,
    this.currentValue,
    this.focusNode,
    this.nextFocusNode,
  }) : super(key: key);

  @override
  State<PasswordFormField> createState() => PasswordFormFieldState();
}

class PasswordFormFieldState extends State<PasswordFormField> {
  bool _obscureText = true;

  @override
  Widget build(BuildContext context) {
    return TextFormField(
      keyboardType: TextInputType.visiblePassword,
      obscureText: _obscureText,
      onChanged: widget.onChanged,
      validator: widget.validator,
      decoration: InputDecoration(
        labelText: widget.labelText,
        helperText: "",
        suffixIcon: IconButton(
          icon: Icon(_obscureText ? Icons.visibility : Icons.visibility_off),
          onPressed: () {
            setState(() {
              _obscureText = !_obscureText;
            });
          },
        ),
      ),
      initialValue: widget.currentValue,
      focusNode: widget.focusNode,
      onFieldSubmitted: (value) {
        if (widget.nextFocusNode != null) {
          FocusScope.of(context).requestFocus(widget.nextFocusNode);
        }
      },
    );
  }
}
