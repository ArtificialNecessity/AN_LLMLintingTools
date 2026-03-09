# Code Duplication Checker

A sophisticated code duplication detector that uses phrase-based hashing to find duplicate code blocks across multiple files.

## The #1 Problem: LLMs Have No Cost for Duplication

**LLMs naturally copy-paste and rewrite similar code segments because they have no inherent "cost" to duplicating code.** This breaks the Single Responsibility Principle and creates maintenance nightmares:

- Change a bug fix in one location → same bug still exists in 3 other files
- Update an algorithm → now have inconsistent implementations
- Refactor a pattern → miss half the occurrences

This tool acts as a **cognitive guardrail** that makes duplication visible immediately, forcing proper refactoring instead of copy-paste.

## Additional Benefits for LLM Development

1. **Token Efficiency** - Duplicated code wastes LLM context window space. 200 lines × 3 files = 600 tokens for the same information.
2. **Context Window Optimization** - Increases codebase "information density" - LLM gets unique insights per file vs re-reading identical patterns.
3. **Consistent Edits** - When LLM uses `apply_diff` on duplicated code, it must update all copies. This tool identifies those locations upfront.
4. **Faster Semantic Search** - Less redundancy means better `codebase_search` results - surfaces diverse implementations rather than multiple copies.
5. **Architecture Signals** - Large duplicate blocks suggest refactoring opportunities: shared utilities, base classes, or interface abstractions.

## Supported Languages

- **C-family:** C (`.c`, `.h`), C++ (`.cpp`, `.hpp`), C# (`.cs`), Java (`.java`), Go (`.go`), Rust (`.rs`)
- **Web/JS:** TypeScript (`.ts`, `.tsx`), JavaScript (`.js`, `.jsx`)
- **Markup:** HTML (`.html`, `.htm`), CSS (`.css`, `.scss`, `.less`)
- **Scripting:** Python (`.py`), Ruby (`.rb`)

## Usage

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

## Algorithm

Uses a **one-pass phrase hashing algorithm** with:
- **10-token phrase windows** - Sliding window for pattern detection
- **Minimum 200 tokens** for fragment detection - Filters out trivial matches
- **60% minimum hit ratio** threshold - Ensures meaningful similarity
- **Contiguous hit/miss tracking** - Tracks match quality across the fragment
- **Smart filtering** of boilerplate (comments, imports, method signatures)

### Why One-Pass?

A two-pass algorithm would report the SAME duplicate TWICE:
- When processing FileA: finds match with FileB → reports "FileA ↔ FileB"
- When processing FileB: finds match with FileA → reports "FileB ↔ FileA"

The one-pass solution processes files sequentially:
1. **FIRST:** Detect fragments using current hashMap (only has EARLIER files)
2. **THEN:** Add this file's hashes to hashMap

This way, duplicates are only found when the LATER file is processed.

## Output Formats

### Human-Readable
```
75.2% - 450 tokens - src/utils/helpers.ts:42 ↔ src/lib/tools.ts:156
```

### JSON
```json
{
  "description": "Code duplication detection results...",
  "duplicates": [
    {
      "match_similarity": 0.752,
      "match_length": 450,
      "source": "src/utils/helpers.ts:42",
      "target": "src/lib/tools.ts:156"
    }
  ]
}
```

### SARIF v2.1.0
Integrates with VSCode Problems pane with bidirectional warnings - warnings appear in BOTH files when either is opened.

## Exit Codes

- `0` - No duplicates found (clean)
- `1` - Duplicates found above threshold (for CI/CD integration)

## CI/CD Integration

```yaml
# GitHub Actions example
- name: Check for code duplication
  run: dotnet run --file CodeDuplicationChecker/CheckCodeDuplication.cs src/
```

## Implementation

See [`CodeDuplicationChecker/CheckCodeDuplication.cs`](../CodeDuplicationChecker/CheckCodeDuplication.cs) for the complete implementation with detailed algorithm documentation.