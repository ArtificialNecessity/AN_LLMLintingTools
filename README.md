# LLM Linting Tools

A collection of specialized linting and code quality tools designed specifically for LLM-assisted development workflows.

## Quick Reference

| Tool | Description |
|------|-------------|
| **Code Duplication Checker** | Detects duplicate code blocks across multiple files using phrase-based hashing |
| **enforce-namespaced-brands** | Enforces that TypeScript branded types are defined within namespaces |
| **match-namespace-import-to-filename** | Ensures namespace imports match the filename or directory being imported from |
| **no-type-assertion-in-instanceof** | Disallows misleading type assertions in instanceof expressions |

## Overview

This repository contains tools that help optimize codebases for LLM (Large Language Model) co-development by detecting code duplication, enforcing TypeScript best practices, and improving code maintainability.

## Tools

### 1. Code Duplication Checker (C#)

**The #1 Problem:** LLMs naturally copy-paste and rewrite similar code segments because they have no inherent "cost" to duplicating code. This breaks the Single Responsibility Principle and creates maintenance nightmares.

A sophisticated code duplication detector that uses phrase-based hashing to find duplicate code blocks across multiple files, acting as a **cognitive guardrail** that makes duplication visible immediately.

**Quick Start:**
```bash
dotnet run --file CodeDuplicationChecker/CheckCodeDuplication.cs <path>
```

**📖 [Full Documentation](PROMPTS/CodeDuplication.md)** - Algorithm details, supported languages, output formats, CI/CD integration

---

### 2. TypeScript ESLint Rules

Custom ESLint rules that enforce **linguistic keying** - creating unambiguous cognitive pathways for LLMs through fully-qualified, semantically-explicit type names.

#### `enforce-namespaced-brands`

Enforces that branded types are defined within namespaces to enable deterministic LLM cognitive processing.

```typescript
// ❌ Bad - ambiguous, high-entropy
type UserId = string;

// ✅ Good - linguistically keyed
export namespace Brands {
  export type User_SessionID = Brand<string, 'User_SessionID'>;
}
```

**📖 [Full Documentation](PROMPTS/BrandedTypes.md)** - Linguistic keying philosophy, cognitive architecture, examples

---

#### `match-namespace-import-to-filename`

Ensures namespace imports match the filename being imported from.

```typescript
// ❌ Bad
import * as foo from './utils/helpers';

// ✅ Good
import * as helpers from './utils/helpers';
```

**📖 [Full Documentation](PROMPTS/TypeScriptRules.md#match-namespace-import-to-filename)** - Configuration options, auto-fix behavior

---

#### `no-type-assertion-in-instanceof`

Disallows misleading type assertions in `instanceof` expressions.

```typescript
// ❌ Bad
if ((obj as MyClass) instanceof MyClass) { }

// ✅ Good
if (obj instanceof MyClass) { }
```

**📖 [Full Documentation](PROMPTS/TypeScriptRules.md#no-type-assertion-in-instanceof)** - Why this matters, examples

## Installation

### Code Duplication Checker

Requires .NET SDK (6.0 or later). The script is self-contained and can be run directly:

```bash
dotnet run --file CodeDuplicationChecker/CheckCodeDuplication.cs <path>
```

### TypeScript ESLint Rules

1. Copy the rule files to your ESLint configuration directory
2. Add to your `.eslintrc.js`:

```javascript
module.exports = {
  rules: {
    'enforce-namespaced-brands': 'warn',
    'match-namespace-import-to-filename': 'warn',
    'no-type-assertion-in-instanceof': 'error'
  }
};
```

## CI/CD Integration

### Code Duplication Checker

The duplication checker exits with code 1 when duplicates are found, making it perfect for CI/CD:

```yaml
# GitHub Actions example
- name: Check for code duplication
  run: dotnet run --file CodeDuplicationChecker/CheckCodeDuplication.cs src/
```

### ESLint Rules

Standard ESLint integration works with these custom rules.

## Authors

- **David W. Jeske** - <davidj@gmail.com>
- **Claude Sonnet 4.5** - AI pair programming assistant
- **Claude Opus 4.5** - AI pair programming assistant
- **Gemini** - AI pair programming assistant (running as Kilo Code)

## License

MIT / Public Domain

## Contributing

This project is designed for LLM-assisted development workflows. Contributions that improve code quality detection or TypeScript best practices are welcome.

## Related Resources

- [Refactoring Guru - Duplicate Code](https://refactoring.guru/smells/duplicate-code)
- [SARIF Specification](https://docs.oasis-open.org/sarif/sarif/v2.1.0/sarif-v2.1.0.html)
- [ESLint Custom Rules](https://eslint.org/docs/latest/extend/custom-rules)


## See Also: Reflex Engine - VSCode Extension

**[Install from VSCode Marketplace](https://marketplace.visualstudio.com/items?itemName=artificial-necessity.astrodev)**

The Reflex Engine is a VSCode extension that provides an **always-on incremental update on save** version of the Code Duplication Checker, plus a framework for writing other automation scripts. It runs the duplication checker automatically whenever you save a file, providing immediate feedback in the VSCode Problems pane.

**Features:**
- Automatic code duplication detection on save
- SARIF integration for VSCode Problems pane
- Framework for custom automation scripts
- Designed for LLM-assisted development workflows

---