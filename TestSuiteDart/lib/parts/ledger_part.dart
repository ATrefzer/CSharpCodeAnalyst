// Declared in a part file, but belongs to library_with_part.dart. Both the function and the
// class below must appear in the "library_with_part" namespace, NOT in a "parts.ledger_part" one.

part of '../library_with_part.dart';

int _sumBalances(Ledger ledger) {
  var sum = 0;
  for (final account in ledger.accounts) {
    sum += account.balance;
  }
  return sum;
}

class PartLocalHelper {
  const PartLocalHelper();
}
