import 'package:analyzer/dart/ast/ast.dart';
import 'package:analyzer/dart/ast/token.dart';
import 'package:analyzer/dart/ast/visitor.dart';
import 'package:analyzer/source/line_info.dart';

import 'model.dart';

/// Source-level metrics for a single member, computed from its declaration syntax.
///
/// The counting rules deliberately mirror the C# SourceMetricsCollector so that the numbers of a
/// Dart and a C# project mean the same thing:
///  - a physical line is "code" when a real token touches it, comment-only lines are counted
///    separately, and a line carrying both stays code;
///  - logical lines are executable statements, block wrappers excluded, and an expression body
///    ("=> x * 2") counts as one;
///  - complexity is McCabe: one plus the decision points.
class MetricsCollector {
  MetricsCollector(this._lineInfo);

  final LineInfo _lineInfo;

  /// Whether the declaration has an implementation to measure. False for abstract and external
  /// members, and for the signature half of a redirecting constructor - measuring those would
  /// report a body of zero lines and dilute every average.
  static bool hasBody(AstNode declaration) {
    final body = switch (declaration) {
      MethodDeclaration() => declaration.body,
      FunctionDeclaration() => declaration.functionExpression.body,
      ConstructorDeclaration() => declaration.body,
      _ => null,
    };
    return body is BlockFunctionBody || body is ExpressionFunctionBody;
  }

  MemberMetrics compute(AstNode declaration) {
    final (codeLines, commentLines) = _countLines(declaration);
    return MemberMetrics(
      codeLines: codeLines.length,
      commentLines: commentLines.length,
      logicalLinesOfCode: _countLogicalLines(declaration),
      cyclomaticComplexity: 1 + _countDecisionPoints(declaration),
    );
  }

  (Set<int>, Set<int>) _countLines(AstNode declaration) {
    final codeLines = <int>{};
    final commentLines = <int>{};

    // Unlike Roslyn, the Dart scanner does not put comments in the token stream: they hang off the
    // following token as precedingComments. They are picked up from there below.
    Token? token = _firstTokenInStream(declaration);
    final end = declaration.endToken;
    while (token != null) {
      _addLines(codeLines, token.offset, token.end);

      Token? comment = token.precedingComments;
      while (comment != null) {
        _addLines(commentLines, comment.offset, comment.end);
        comment = comment.next;
      }

      if (token == end) {
        break;
      }
      token = token.next;
    }

    commentLines.removeAll(codeLines);
    return (codeLines, commentLines);
  }

  /// The token to start walking from.
  ///
  /// beginToken is a trap here: for a declaration carrying a documentation comment it returns the
  /// comment's token, which lives in the precedingComments chain and *not* in the main token
  /// stream - following its "next" leaves the declaration immediately and the whole body goes
  /// uncounted. Annotations, on the other hand, are regular tokens and are part of the code.
  static Token _firstTokenInStream(AstNode declaration) {
    if (declaration is AnnotatedNode) {
      final metadata = declaration.metadata;
      if (metadata.isNotEmpty) {
        return metadata.first.beginToken;
      }
      return declaration.firstTokenAfterCommentAndMetadata;
    }
    return declaration.beginToken;
  }

  void _addLines(Set<int> lines, int startOffset, int endOffset) {
    final first = _lineInfo.getLocation(startOffset).lineNumber;
    final last = _lineInfo.getLocation(endOffset).lineNumber;
    for (var line = first; line <= last; line++) {
      lines.add(line);
    }
  }

  /// Executable statements, block wrappers excluded, so "if (x) { y(); }" counts as one.
  int _countLogicalLines(AstNode declaration) {
    final counter = _StatementCounter();
    declaration.visitChildren(counter);
    if (counter.statements == 0 && counter.hasExpressionBody) {
      return 1;
    }
    return counter.statements;
  }

  int _countDecisionPoints(AstNode declaration) {
    final counter = _DecisionPointCounter();
    declaration.visitChildren(counter);
    return counter.count;
  }
}

/// Generalizing rather than recursive: it dispatches every statement to visitStatement, so the
/// count does not have to enumerate the ~30 statement types by hand.
class _StatementCounter extends GeneralizingAstVisitor<void> {
  int statements = 0;
  bool hasExpressionBody = false;

  @override
  void visitExpressionFunctionBody(ExpressionFunctionBody node) {
    hasExpressionBody = true;
    super.visitExpressionFunctionBody(node);
  }

  @override
  void visitStatement(Statement node) {
    // The wrapping braces of a block are not a statement of their own.
    if (node is! Block) {
      statements++;
    }
    super.visitStatement(node);
  }
}

/// McCabe decision points. The set mirrors the C# collector, plus the two Dart constructs that
/// have no C# equivalent: "if" and "for" inside a collection literal are real branches.
class _DecisionPointCounter extends RecursiveAstVisitor<void> {
  int count = 0;

  @override
  void visitIfStatement(IfStatement node) {
    count++;
    super.visitIfStatement(node);
  }

  @override
  void visitWhileStatement(WhileStatement node) {
    count++;
    super.visitWhileStatement(node);
  }

  @override
  void visitDoStatement(DoStatement node) {
    count++;
    super.visitDoStatement(node);
  }

  @override
  void visitForStatement(ForStatement node) {
    count++;
    super.visitForStatement(node);
  }

  @override
  void visitSwitchCase(SwitchCase node) {
    count++;
    super.visitSwitchCase(node);
  }

  @override
  void visitSwitchPatternCase(SwitchPatternCase node) {
    count++;
    super.visitSwitchPatternCase(node);
  }

  @override
  void visitSwitchExpressionCase(SwitchExpressionCase node) {
    // A bare "_ => ..." is the switch-expression equivalent of "default:" and is not counted;
    // a guarded wildcard ("_ when ...") is a real condition.
    final pattern = node.guardedPattern;
    final isCatchAll = pattern.pattern is WildcardPattern && pattern.whenClause == null;
    if (!isCatchAll) {
      count++;
    }
    super.visitSwitchExpressionCase(node);
  }

  @override
  void visitCatchClause(CatchClause node) {
    count++;
    super.visitCatchClause(node);
  }

  @override
  void visitConditionalExpression(ConditionalExpression node) {
    count++;
    super.visitConditionalExpression(node);
  }

  @override
  void visitIfElement(IfElement node) {
    count++;
    super.visitIfElement(node);
  }

  @override
  void visitForElement(ForElement node) {
    count++;
    super.visitForElement(node);
  }

  @override
  void visitBinaryExpression(BinaryExpression node) {
    final type = node.operator.type;
    if (type == TokenType.AMPERSAND_AMPERSAND || type == TokenType.BAR_BAR || type == TokenType.QUESTION_QUESTION) {
      count++;
    }
    super.visitBinaryExpression(node);
  }

  @override
  void visitAssignmentExpression(AssignmentExpression node) {
    // "x ??= y" carries the same branch as "x = x ?? y".
    if (node.operator.type == TokenType.QUESTION_QUESTION_EQ) {
      count++;
    }
    super.visitAssignmentExpression(node);
  }
}
