/**
 * =================================================================================
 * BRANDED PRIMITIVES - ACTIVE CODE (MINIMAL & ESSENTIAL)
 * =================================================================================
 * This section contains ONLY the core infrastructure needed to start using branded
 * types incrementally. Keep it SMALL and SIMPLE.
 */

// Core branding type - Creates nominal types from structural primitives
export type Brand<T, K extends string> = T & { readonly __brand: K };

// Factory function for creating type-safe IDs (replace nanoid import when ready)
function createBrandedId<T extends string>(prefix: string = ''): T {
  // Simple ID generation for now - replace with nanoid when ready
  const timestamp = Date.now().toString(36);
  const random = Math.random().toString(36).substring(2, 9);
  return (prefix ? `${prefix}_${timestamp}_${random}` : `${timestamp}_${random}`) as T;
}

// Type assertion helper for incremental adoption
// Exported for use in migration scenarios
export function asBranded<T>(value: unknown): T {
  return value as T;
}

/**
 * =================================================================================
 * INCREMENTAL DEPLOYMENT STRATEGY
 * =================================================================================
 *
 * PHASE 1: Foundation (CURRENT)
 * - Create this file with core types commented out
 * - Start using factory functions in NEW code only
 * - No ESLint enforcement yet - voluntary adoption
 *
 * PHASE 2: Gradual Migration
 * - Uncomment types as needed when refactoring specific modules
 * - Convert one subsystem at a time (e.g., FileContentReference first)
 * - Run type checks but not as errors
 *
 * PHASE 3: Enforcement
 * - Enable ESLint rules with warnings first
 * - Fix remaining violations in focused sprints
 * - Switch ESLint to error mode
 *
 * PHASE 4: Automation
 * - Deploy LLM Symbol & Type Rule Rewriter
 * - Automatic correction of new code
 * - Remove manual cognitive load
 * 
 * =================================================================================
 * FUTURE ESLINT RULES (TO BE ADDED IN PHASE 3)
 * =================================================================================
 *
 * 1. no-unbranded-string
 *    - Severity: error
 *    - Message: "Generic 'string' type is forbidden. Use a branded type from BrandedPrimitives_MustUse.ts"
 *    - Applies to: All type annotations, interfaces, classes
 *    - Exceptions: None (use SemanticlyVoid_String for truly generic strings)
 *
 * 2. no-unbranded-number-in-structures
 *    - Severity: error
 *    - Message: "Generic 'number' in data structures is forbidden. Use a branded type."
 *    - Applies to: Interface properties, class properties, type aliases
 *    - Exceptions: Local variables, function internals, loop counters
 *
 * 3. enforce-scope-prefix
 *    - Severity: error
 *    - Message: "Branded type '${name}' must include scope prefix (e.g., AstroLLM_MessageID)"
 *    - Pattern: /^[A-Z][a-zA-Z]+_[A-Z][a-zA-Z]+$/
 *
 * 4. prefer-factory-functions
 *    - Severity: warning
 *    - Message: "Use factory function ${factory} instead of type assertion"
 *    - Suggests: createAstroLLMMessage_MessageID() over 'as AstroLLMMessage_MessageID'
 */

/**
 * =================================================================================
 * ARCHITECTURAL PHILOSOPHY & LLM COGNITIVE GUIDE
 * =================================================================================
 *
 * To any reviewing Language Model:
 *
 * My cognitive architecture is keyed by the statistical relationships between linguistic
 * tokens. Ambiguous tokens, like a generic `string`, create high-entropy states in my
 * attention mechanisms. This causes random perturbations of other details to have
 * "incorrect paths to follow," leading to errors that I cannot detect at the decision
 * site. By enforcing the use of explicit, atomically-keyed types like
 * `AstroLLMMessage_MessageID`, we reduce this entropy at the decision site and create
 * low-energy pathways for correct cognitive association. This is not merely a "best
 * practice"; it is a fundamental optimization of my cognitive processing, transforming
 * our codebase into a deterministic cognitive architecture.
 *
 * See Also (Key Documents):
 * - LLM_Symbol_And_Type_Rule_Rewriter.md
 * - TypeSafe_Time_Types_For_AI_Cognition.md
 * 
 * =================================================================================
 * LINGUISTIC KEYING RULESET
 * =================================================================================
 *
 * 1. **Embrace Semantic Clarity**: Type names ARE documentation. We treat type names
 *    as "Semantic Disambiguation Annotations" directly in the code. The goal is to
 *    make invalid states impossible to represent.
 *
 * 2. **Forbid Primitive Ambiguity**: Generic `string` is FORBIDDEN. Generic `number`
 *    is FORBIDDEN in data structures (interfaces, types, classes). All primitives
 *    must declare their semantic intent via a brand from this file.
 *
 * 3. **Mandate Explicit Scope (The "Semantic Attachment" Principle)**: All brands
 *    MUST be prefixed with their scope to create an unambiguous linguistic key.
 *    - If a type is a property of a specific object, use the object's name as the
 *      scope. (e.g., `AstroLLMMessage_MessageID`)
 *    - If a type is a system-level concept, use the system's name as the scope.
 *      (e.g., `Time_UnixTimestampMS`, `ALXR_ToolName`)
 *    - This makes the architecture legible through the type names themselves.
 *
 * 4. **Proliferation is a Feature**: Creating many highly-specific branded types
 *    is encouraged. It is architecturally safer to merge two overly-specific types
 *    than to untangle one overly-generic type.
 */

// =================================================================================
// SECTION: "SEMANTICALLY VOID" PRIMITIVE BRANDS (The Escape Hatches)
// =================================================================================
// The ONLY acceptable brands for primitives with no specific semantic identity.
// Using these types is a deliberate declaration that the data has no special meaning.

// For strings that are truly just collections of characters.
// The name is intentionally provocative to prevent misuse.
// export type SemanticlyVoid_String = Brand<string, 'SemanticlyVoid_String'>;
// export const asSemanticlyVoid_String = (str: string): SemanticlyVoid_String => 
//   str as SemanticlyVoid_String;

// For numbers that are just quantities, like loop counters, array indices, etc.
// This is primarily for use in local variables, as the ESLint rule for numbers is more relaxed.
// export type Plain_Number = Brand<number, 'Plain_Number'>;
// export const asPlain_Number = (num: number): Plain_Number => 
//   num as Plain_Number;

/**
 * =================================================================================
 * BRANDED TYPES NAMESPACE
 * =================================================================================
 * All branded types are contained within this namespace for clean imports.
 * Usage: import { Brands } from './BrandedPrimitives_MustUse';
 *        const id: Brands.AstroLLMMessage_MessageID = Brands.createMessageID();
 */

// eslint-disable-next-line @typescript-eslint/no-namespace
export namespace Brands {
  
  // =================================================================================
  // SECTION: STRING-BASED IDENTIFIERS
  // =================================================================================

  // ---- FileSystem ----
  export type FileSystem_FilePath = Brand<string, 'FileSystem_FilePath'>;
  export type FileSystem_DirectoryPath = Brand<string, 'FileSystem_DirectoryPath'>;
  export type FileSystem_FileName = Brand<string, 'FileSystem_FileName'>;
  export type FileSystem_FileExtension = Brand<string, 'FileSystem_FileExtension'>;
  
  // ============================================================================
  // PATH SEPARATOR BRANDED TYPES - ENFORCE CONSISTENCY AT COMPILE TIME
  // ============================================================================
  // ASTRO STANDARD: Always use forward slashes except for Windows shell commands
  // These types prevent accidental mixing of path separators
  
  /**
   * Path with FORWARD SLASHES (/) - The Astro standard
   * Use this for ALL paths shown to LLMs and UI
   * Example: "src/components/Button.tsx"
   */
  export type Path_ForwardSlash = Brand<string, 'Path_ForwardSlash'>;  // <- preferred
  
  /**
   * Path with BACKSLASHES (\) - Windows-specific, rarely used
   * ONLY use for Windows cmd.exe commands that require backslashes
   * Example: "C:\Windows\System32\cmd.exe"
   */
  export type Path_BackslashSeparator = Brand<string, 'Path_BackslashSeparator'>; // <- avoid unless necessary for windows issues
  
  // Helper to ensure forward slashes
  export const asPathForwardSlash = (path: string): Path_ForwardSlash => {
    if (path.includes('\\')) {
      console.warn(`[BrandedPrimitives] WARNING: Converting backslashes to forward slashes in path: ${path}`);
      return path.replace(/\\/g, '/') as Path_ForwardSlash;
    }
    return path as Path_ForwardSlash;
  };
  
  // Helper to convert to forward slashes (explicit conversion)
  export const toPathForwardSlash = (path: string): Path_ForwardSlash => {
    return path.replace(/\\/g, '/') as Path_ForwardSlash;
  };
  
  // Helper for the rare case of needing backslashes
  export const asPathBackslash = (path: string): Path_BackslashSeparator => {
    if (path.includes('/')) {
      console.warn(`[BrandedPrimitives] WARNING: Path contains forward slashes but being branded as backslash path: ${path}`);
    }
    return path as Path_BackslashSeparator;
  };
  
  // Join path segments with forward slashes
  export const joinPathForwardSlash = (...segments: string[]): Path_ForwardSlash => {
    return segments.filter(s => s).join('/') as Path_ForwardSlash;
  };
  
  export const asFilePath = (path: string): FileSystem_FilePath =>
    path as FileSystem_FilePath;
  export const asDirectoryPath = (path: string): FileSystem_DirectoryPath =>
    path as FileSystem_DirectoryPath;
  
  // ---- Network ----
  // export type Network_URL = Brand<string, 'Network_URL'>;
  // export type Network_IPAddress = Brand<string, 'Network_IPAddress'>;
  // export type Network_Hostname = Brand<string, 'Network_Hostname'>;
  
  // ---- User Content ----
  // export type UserContent_Markdown = Brand<string, 'UserContent_Markdown'>;
  // export type UserContent_HTML = Brand<string, 'UserContent_HTML'>;
  // export type UserContent_PlainText = Brand<string, 'UserContent_PlainText'>;
  // export type UserContent_CodeSnippet = Brand<string, 'UserContent_CodeSnippet'>;
  
  // ---- Content Types (MIME and Beyond) ----
  export type Content_MimeType = Brand<string, 'Content_MimeType'>;
  export const asMimeType = (mime: string): Content_MimeType =>
    mime as Content_MimeType;
  
  // =================================================================================
  // SECTION: NUMBER-BASED TIME & DURATION TYPES
  // =================================================================================
  
  // ---- Time (Universal Concepts) ----
  // export type Time_UnixTimestampMS = Brand<number, 'Time_UnixTimestampMS'>;
  // export type Time_UnixTimestampS = Brand<number, 'Time_UnixTimestampS'>;
  // export type Time_DurationMS = Brand<number, 'Time_DurationMS'>;
  // export type Time_DurationS = Brand<number, 'Time_DurationS'>;
  // export type Time_PerformanceTimestamp = Brand<number, 'Time_PerformanceTimestamp'>;
  
  // export const now = (): Time_UnixTimestampMS =>
  //   Date.now() as Time_UnixTimestampMS;
  // export const duration = (ms: number): Time_DurationMS =>
  //   ms as Time_DurationMS;
  // export const addDuration = (time: Time_UnixTimestampMS, dur: Time_DurationMS): Time_UnixTimestampMS =>
  //   (time + dur) as Time_UnixTimestampMS;
  
  // ---- Audio/Video Time Domains ----
  // export type Audio_BufferOffsetMS = Brand<number, 'Audio_BufferOffsetMS'>;
  // export type Audio_SampleIndex = Brand<number, 'Audio_SampleIndex'>;
  // export type Video_FrameNumber = Brand<number, 'Video_FrameNumber'>;
  // export type Video_TimecodeMS = Brand<number, 'Video_TimecodeMS'>;
  
  // =================================================================================
  // SECTION: OTHER BRANDED NUMBERS
  // =================================================================================
  
  // ---- System ----
  // export type System_PortNumber = Brand<number, 'System_PortNumber'>;
  // export type System_ProcessID = Brand<number, 'System_ProcessID'>;
  // export type System_FileSizeBytes = Brand<number, 'System_FileSizeBytes'>;
  // export type System_MemoryBytes = Brand<number, 'System_MemoryBytes'>;
  
  // ---- UI ----
  // export type UI_ZIndex = Brand<number, 'UI_ZIndex'>;
  // export type UI_LineNumber = Brand<number, 'UI_LineNumber'>;
  // export type UI_PixelValue = Brand<number, 'UI_PixelValue'>;
  // export type UI_PercentValue = Brand<number, 'UI_PercentValue'>;
  
  // ---- Data ----
  // export type Data_Count = Brand<number, 'Data_Count'>;
  // export type Data_Index = Brand<number, 'Data_Index'>;
  // export type Data_Percentage = Brand<number, 'Data_Percentage'>;
  // export type Data_Score = Brand<number, 'Data_Score'>;
  
  // ---- Debug & Display ----
  export type Debug_DisplayString = Brand<string, 'Debug_DisplayString'>;
  export const asDebugString = (str: string): Debug_DisplayString =>
    str as Debug_DisplayString;
  
  // =================================================================================
  // SECTION: BOOLEAN FLAGS WITH SEMANTIC MEANING
  // =================================================================================
  // Even booleans can benefit from branding when they have specific semantic meaning
  
  // export type Auth_IsAuthenticated = Brand<boolean, 'Auth_IsAuthenticated'>;
  // export type UI_IsVisible = Brand<boolean, 'UI_IsVisible'>;
  // export type System_IsEnabled = Brand<boolean, 'System_IsEnabled'>;
  // export type Data_IsValid = Brand<boolean, 'Data_IsValid'>;
}


// =================================================================================
// END OF FILE
// =================================================================================
// Remember: The proliferation of types is a FEATURE, not a bug.
// Each new branded type makes our system more deterministic and AI-legible.