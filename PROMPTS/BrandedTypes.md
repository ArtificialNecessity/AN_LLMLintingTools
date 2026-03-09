# Branded Types & Linguistic Keying

## The Core Problem: Ambiguous Identifiers Create Cognitive Entropy

LLMs operate on statistical relationships between linguistic tokens. Generic identifiers like `id`, `name`, or `string` create **high-entropy states** in attention mechanisms, causing random perturbations that lead to errors the LLM cannot detect at the decision site.

```typescript
// ❌ FORBIDDEN - Ambiguous, high-entropy identifier
type UserId = string;
type ProductId = string;
function getUser(id: UserId) { /* ... */ }
getUser(productId); // TypeScript allows this! Both are just 'string'.
```

## The Solution: Linguistic Keying via Branded Types

By enforcing explicit, atomically-keyed types, we reduce entropy at decision sites and create **low-energy pathways for correct cognitive association**.

```typescript
// Core branding mechanism
type Brand<T, K extends string> = T & { readonly __brand: K };

// ✅ CORRECT - Linguistically keyed, unambiguous
export namespace Brands {
  export type FileSystem_FilePath = Brand<string, 'FileSystem_FilePath'>;
  export type User_SessionID = Brand<string, 'User_SessionID'>;
}

// Usage creates deterministic cognitive pathways
function loadFile(path: Brands.FileSystem_FilePath) { /* ... */ }
loadFile(sessionId); // ❌ Type error! Cognitive architecture prevents the mistake.
```

## Why Namespaces? Linguistic Organization, Not Just Pollution Prevention

The namespace requirement isn't about avoiding "top-level pollution" - it's about **semantic attachment**. Each brand must be linguistically keyed to its domain:

```typescript
// ❌ Bad - Generic 'Id' has no linguistic key
export type Id = Brand<string, "Id">;

// ✅ Good - Fully qualified, linguistically keyed
export namespace Brands {
  export type FileSystem_FilePath = Brand<string, 'FileSystem_FilePath'>;
  export type Audio_BufferOffsetMS = Brand<number, 'Audio_BufferOffsetMS'>;
  export type Debug_DisplayString = Brand<string, 'Debug_DisplayString'>;
}
```

## The "Why" Test

Before creating any identifier, ask: "What, specifically, does this identify?"
- If the answer is "an item" or "a record" → **WRONG**
- If the answer is "a file system path" or "an audio buffer offset" → **CORRECT** - the name must reflect that

## Benefits for LLM Co-Development

1. **Deterministic Cognitive Architecture** - Type names create unambiguous attention pathways
2. **Self-Correcting Code** - The type system becomes a "cognitive guardrail" preventing LLM generative habits from overriding reasoning
3. **Reduced Context Window Waste** - No need to re-explain what `id` means in each context
4. **Impossible Invalid States** - Semantic mismatches become compile-time errors

## ESLint Rule: `enforce-namespaced-brands`

This rule enforces that all branded types are defined within namespaces.

**Configuration:**
```json
{
  "rules": {
    "enforce-namespaced-brands": "warn"
  }
}
```

**Examples:**

```typescript
// ❌ Bad - top-level brand (rule violation)
export type UserId = Brand<string, "UserId">;

// ✅ Good - namespaced brand
export namespace Brands {
  export type User_SessionID = Brand<string, "User_SessionID">;
}
```

## Related Files

- [`TypeScript/BrandedTypes_MustUse.ts`](../TypeScript/BrandedTypes_MustUse.ts) - Complete branded types infrastructure
- [`TypeScript_TSPatch/enforce-namespaced-brands.js`](../TypeScript_TSPatch/enforce-namespaced-brands.js) - ESLint rule implementation

## Further Reading

The "Linguistic Keying Ruleset" in BrandedTypes_MustUse.ts contains the full cognitive architecture philosophy and incremental deployment strategy.