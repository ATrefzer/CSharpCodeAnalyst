# Consistency Rules Format

This document describes the text format for consistency rules.

## Basic Syntax

Rules are written one per line. Each rule has the following format:

```
RULE_TYPE: <source_pattern> OPERATION <target_pattern> [COMMENT]
```

## Rule Types

- `ALLOW`: Allows dependencies between source and target
- `DENY`: Denies dependencies between source and target
- `ISOLATE`: Source may not depend on anything outside of specified targets

## Operations

- `->`: "may depend on" / "may access"
- `!->`: "may not depend on" / "may not access"
- `<->`: "may have bidirectional dependencies with"
- `!<->`: "may not have bidirectional dependencies with"

## Pattern Syntax

Patterns can match namespaces, classes, or methods using wildcards:

- `*` matches any single namespace/class/method name
- `**` matches any nested structure
- `MyNamespace.*` matches direct children of MyNamespace
- `MyNamespace.**` matches all descendants of MyNamespace

## Examples

```
// Business layer may not access Data layer directly
DENY: Business.** !-> Data.**

// Controllers may only access Services
ISOLATE: Controllers.** -> Services.**

// Core components may not depend on UI
DENY: Core.** !-> UI.**

// Allow specific exceptions
ALLOW: Core.Logging.** -> UI.Controls.MessageBox

// Bidirectional isolation
DENY: ModuleA.** !<-> ModuleB.**
```

## Comments

Lines starting with `//` are comments and are ignored.