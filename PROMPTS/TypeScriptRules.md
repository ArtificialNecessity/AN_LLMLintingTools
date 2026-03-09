# TypeScript ESLint Rules

Custom ESLint rules for TypeScript that enforce best practices for type safety and code organization in LLM-assisted development.

## match-namespace-import-to-filename

Ensures namespace imports match the filename or directory being imported from.

### Why This Matters

Consistent naming between imports and file structure creates clear cognitive pathways. When `import * as helpers from './utils/helpers'` matches the actual filename, LLMs can more reliably track dependencies and suggest correct imports.

### Examples

```typescript
// ❌ Bad - namespace doesn't match filename
import * as foo from './utils/helpers';

// ✅ Good - namespace matches filename
import * as helpers from './utils/helpers';

// ✅ Also good for index files - uses parent directory name
import * as utils from './utils/index';

// ✅ For non-relative imports with slashes - allows underscore-joined format
import * as fs_promises from 'fs/promises';  // or 'promises'
```

### Configuration

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

**Options:**
- `ignoreCase` (default: `false`) - Case-insensitive matching
- `allowIndexFiles` (default: `true`) - Use parent directory name for index files

### Auto-Fix

This rule includes an auto-fixer that will rename the namespace to match the filename.

---

## no-type-assertion-in-instanceof

Disallows type assertions in `instanceof` expressions since they're misleading and don't affect runtime behavior.

### Why This Matters

Type assertions in `instanceof` checks create a false sense of type safety. The assertion is compile-time only and doesn't affect the runtime check, leading to potential bugs.

### Examples

```typescript
// ❌ Bad - type assertion is misleading
if ((obj as MyClass) instanceof MyClass) {
  // The 'as MyClass' doesn't affect the instanceof check!
}

// ✅ Good - no type assertion
if (obj instanceof MyClass) {
  // TypeScript narrows the type automatically
}
```

### Configuration

```json
{
  "rules": {
    "no-type-assertion-in-instanceof": "error"
  }
}
```

This rule has no options and should always be an error since type assertions in `instanceof` are always incorrect.

---

## Related Files

- [`TypeScript_TSPatch/match-namespace-import-to-filename.js`](../TypeScript_TSPatch/match-namespace-import-to-filename.js)
- [`TypeScript_TSPatch/no-type-assertion-in-instanceof.js`](../TypeScript_TSPatch/no-type-assertion-in-instanceof.js)
- [`TypeScript_TSPatch/enforce-namespaced-brands.js`](../TypeScript_TSPatch/enforce-namespaced-brands.js) - See [BrandedTypes.md](./BrandedTypes.md) for details