// Member-level constructs: constructors, the getter/setter pair, operators, static and
// instance fields, top-level declarations.

import 'types.dart';

/// Top-level function -> Method below the library namespace.
Color pickFirst(ColorPicker picker) => picker(0);

/// Top-level variable -> Field below the library namespace.
const defaultWidth = 80;


class Account {
  /// The unnamed constructor is named "new", matching Dart's own "Account.new" syntax.
  Account(this._balance);

  /// A named constructor keeps its own name.
  Account.empty() : _balance = 0;

  /// A factory is a constructor too.
  factory Account.copy(Account other) => Account(other._balance);

  static int instances = 0;

  int _balance;

  /// Getter and setter of the same name must collapse into a single Property element.
  int get balance => _balance;

  set balance(int value) => _balance = value;

  /// A field access resolves to a synthetic accessor and must point at the field.
  bool get isEmpty => _balance == 0;

  /// An operator is an ordinary method in Dart.
  Account operator +(Account other) => Account(_balance + other._balance);
}

/// A class whose members are only reachable through the part file below.
class Ledger {
  final List<Account> accounts = [];
}
