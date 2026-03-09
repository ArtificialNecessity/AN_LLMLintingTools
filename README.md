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

**Location:** [`CodeDuplicationChecker/CheckCodeDuplication.cs`](CodeDuplicationChecker/CheckCodeDuplication.cs)

A sophisticated code duplication detector that uses phrase-based hashing to find duplicate code blocks across multiple files.

#### Why This Matters for LLM Development

1. **Token Efficiency** - Duplicated code wastes LLM context window space
2. **Context Window Optimization** - Increases codebase "information density" 
3. **Consistent Edits** - Identifies all locations that need updating when using `apply_diff`
4. **Faster Semantic Search** - Less redundancy means better search results
5. **Architecture Signals** - Large duplicate blocks suggest refactoring opportunities
6. **LLM Vulnerability Defense** - Acts as an "injected linter" to catch accidental duplication

#### Supported Languages

- **C-family:** C (`.c`, `.h`), C++ (`.cpp`, `.hpp`), C# (`.cs`), Java (`.java`), Go (`.go`), Rust (`.rs`)
- **Web/JS:** TypeScript (`.ts`, `.tsx`), JavaScript (`.js`, `.jsx`)
- **Markup:** HTML (`.html`, `.htm`), CSS (`.css`, `.scss`, `.less`)
- **Scripting:** Python (`.py`), Ruby (`.rb`)

#### Usage

```bash
# Scan a directory
dotnet run --file CodeDuplicationChecker/CheckCodeDuplication.cs <path>

# JSON output for programmatic consumption
dotnet run --file CodeDuplicationChecker/CheckCodeDuplication.cs --json <path>

# SARIF output for VSCode integration
dotnet run --file CodeDuplicationChecker/CheckCodeDuplication.cs --sarif duplication.sarif <path>

# Single file self-deduplication
dotnet run --file CodeDuplicationChecker/CheckCodeDuplication.cs myfile.ts
```

#### Algorithm

Uses a **one-pass phrase hashing algorithm** with:
- 10-token phrase windows
- Minimum 200 tokens for fragment detection
- 60% minimum hit ratio threshold
- Contiguous hit/miss tracking
- Smart filtering of boilerplate (comments, imports, method signatures)

#### Output Formats

- **Human-readable:** Percentage match, token count, file locations
- **JSON:** Structured data for LLM/programmatic consumption
- **SARIF v2.1.0:** VSCode Problems pane integration with bidirectional warnings

#### Exit Codes

- `0` - No duplicates found (clean)
- `1` - Duplicates found above threshold (for CI/CD integration)

### 2. TypeScript ESLint Rules

**Location:** [`TypeScript_TSPatch/`](TypeScript_TSPatch/)

Custom ESLint rules for TypeScript that enforce best practices for type safety and code organization.

#### Rules

##### `enforce-namespaced-brands.js`

Enforces that branded types are defined within namespaces to prevent top-level pollution.

```typescript
// ❌ Bad - top-level brand
export type UserId = Brand<string, "UserId">;

// ✅ Good - namespaced brand
export namespace User {
  export type Id = Brand<string, "UserId">;
}
```

**Configuration:**
```json
{
  "rules": {
    "enforce-namespaced-brands": "warn"
  }
}
```

##### `match-namespace-import-to-filename.js`

Ensures namespace imports match the filename or directory being imported from.

```typescript
// ❌ Bad
import * as foo from './utils/helpers';

// ✅ Good
import * as helpers from './utils/helpers';

// ✅ Also good for index files
import * as utils from './utils/index';
```

**Options:**
- `ignoreCase` (default: `false`) - Case-insensitive matching
- `allowIndexFiles` (default: `true`) - Use parent directory name for index files

**Configuration:**
```json
{
  "rules": {
    "match-namespace-import-to-filename": ["warn", {
      "ignoreCase": false,
      "allowIndexFiles": true
    }]
  }
}
```

##### `no-type-assertion-in-instanceof.js`

Disallows type assertions in `instanceof` expressions since they're misleading and don't affect runtime behavior.

```typescript
// ❌ Bad - type assertion is misleading
if ((obj as MyClass) instanceof MyClass) { }

// ✅ Good
if (obj instanceof MyClass) { }
```

**Configuration:**
```json
{
  "rules": {
    "no-type-assertion-in-instanceof": "error"
  }
}
```

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