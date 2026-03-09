#!/usr/bin/env dotnet run
//
// Code Duplication Detection Script
//
// Usage: dotnet run --file Scripts\CheckCodeDuplication.cs <path>
// Scans source files for duplicate code blocks across files
//
// SUPPORTED LANGUAGES:
//    C-family:    C .c .h | C++ .cpp .hpp | C# .cs | Java .java | Go .go | Rust .rs
//      WEB-JS:    TypeScript .ts .tsx | JavaScript .js .jsx
//      Markup:    HTML .html .htm | CSS .css .scss .less
//   Scripting:    Python .py | Ruby .rb
//
// Author: David W. Jeske <davidj@gm.il.com> (and Claude Sonnet 4.5, Claude Opus 4.5)
// LICENSE: MIT / public domain
//
// PURPOSE IN LLM CO-DEVELOPMENT:
//
// 1. Token Efficiency - Duplicated code wastes LLM context window. 200 lines × 3 files =
//    600 tokens for the same information. This identifies refactoring candidates.
//
// 2. Context Window Optimization - By eliminating duplication, codebase "information density"
//    increases. LLM gets unique insights per file vs re-reading identical patterns.
//
// 3. Consistent Edits - When LLM uses apply_diff on duplicated code, it must update all copies.
//    This script identifies those locations upfront, preventing bugs from partial patches.
//
// 4. Faster codebase_search - Semantic search benefits from less redundancy - surfaces diverse
//    implementations rather than multiple copies of same solution.
//
// 5. Architecture Signals - Large duplicate blocks suggest refactoring: shared utilities,
//    base classes, or interface abstractions that actually make sense.
//
// 6. LLM Vulnerability Defense - LLMs are notoriously vulnerable to accidental code duplication
//    when they change tracks or user switches prompts. This acts as an "injected linter"
//    (like VSCode linter hints) so an LLM can immediately see code match duplication across
//    the WHOLE codebase in an instant.
//
// ALGORITHM: 10-token phrase hashing with contiguous hit/miss tracking to find meaningful
// duplication while ignoring trivial boilerplate.
//
// ╔═══════════════════════════════════════════════════════════════════════════════════════╗
// ║  CRITICAL: THIS MUST BE A ONE-PASS ALGORITHM - DO NOT CONVERT TO TWO-PASS!           ║
// ╠═══════════════════════════════════════════════════════════════════════════════════════╣
// ║                                                                                       ║
// ║  WHY ONE-PASS IS REQUIRED:                                                            ║
// ║                                                                                       ║
// ║  A two-pass algorithm (1. build all hashes, 2. detect all fragments) will report     ║
// ║  the SAME DUPLICATE TWICE:                                                            ║
// ║                                                                                       ║
// ║    - When processing FileA: finds match with FileB → reports "FileA ↔ FileB"         ║
// ║    - When processing FileB: finds match with FileA → reports "FileB ↔ FileA"         ║
// ║                                                                                       ║
// ║  ONE-PASS SOLUTION:                                                                   ║
// ║                                                                                       ║
// ║  Process files sequentially. For each file:                                           ║
// ║    1. FIRST: Detect fragments using current hashMap (only has EARLIER files)         ║
// ║    2. THEN:  Add this file's hashes to hashMap                                        ║
// ║                                                                                       ║
// ║  This way, duplicates are only found when the LATER file is processed:               ║
// ║                                                                                       ║
// ║    - File1: hashMap empty → no matches, add File1 hashes                             ║
// ║    - File2: hashMap has File1 → finds File2↔File1 matches, add File2 hashes          ║
// ║    - File3: hashMap has File1,File2 → finds File3↔earlier matches, add File3 hashes  ║
// ║                                                                                       ║
// ║  Each duplicate pair is discovered exactly ONCE.                                      ║
// ║                                                                                       ║
// ╚═══════════════════════════════════════════════════════════════════════════════════════╝

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Text.RegularExpressions;

const bool DEBUG = false; // Set to true for debugging
const bool DEBUG_FRAGMENTS = false; // Set to true to show detailed fragment info
const int PHRASE_TOKEN_COUNT = 10;
const int MIN_FRAGMENT_TOKENS = 200;
const double MIN_HIT_RATIO = 0.60;
const int DEBUG_FRAGMENT_MAX = int.MaxValue; // Fast-fail for debugging: max fragments to try per file

// Repeated character skip threshold: skip phrases containing 4+ identical single-char tokens in a row
// This filters out comment separators like "// ========" which are not meaningful duplication
const int REPEATED_CHAR_THRESHOLD = 4;

// ============================================================================
// DIRECTORY EXCLUSION - Common junk/dependency directories to skip
// These are auto-generated or third-party code that shouldn't be scanned
// ============================================================================
var excludedDirectoryPatterns = new[] {
    // JavaScript/Node ecosystem
    "node_modules",
    "bower_components",
    
    // Python ecosystem
    "__pycache__",
    ".venv",
    "venv",
    ".env",
    "site-packages",
    
    // .NET ecosystem
    "bin",
    "obj",
    "packages",
    
    // Java/Rust/Go build outputs
    "target",
    
    // General build outputs
    "dist",
    "build",
    "out",
    ".build",
    
    // Version control and IDE
    ".git",
    ".svn",
    ".hg",
    ".vs",
    ".idea",
    
    // Vendor/third-party
    "vendor",
    "third_party",
    "external",
    "deps",
    "lib",        // Often contains vendored code
    "libs",
};

// Helper to check if a file path contains any excluded directory
bool IsExcludedPath(string filePath)
{
    var normalizedPath = filePath.Replace('\\', '/');
    foreach (var pattern in excludedDirectoryPatterns)
    {
        // Check for /pattern/ in the path (directory boundary match)
        if (normalizedPath.Contains($"/{pattern}/", StringComparison.OrdinalIgnoreCase))
            return true;
    }
    return false;
}

// Tracker thresholding: discard trackers below quality threshold
// The threshold is: hitRatio >= MIN_TRACKER_RATIO AND hits >= MinTokensForRatio(hitRatio)
// This creates a curve: high ratio can have fewer tokens, low ratio needs more tokens
const double MIN_TRACKER_RATIO = 0.50;  // Absolute minimum hit ratio to keep a tracker
const int MIN_TOKENS_AT_HIGH_RATIO = 50;  // Minimum tokens if ratio is 100%
const int MIN_TOKENS_AT_LOW_RATIO = 300;  // Minimum tokens if ratio is at MIN_TRACKER_RATIO

// ============================================================================
// ARGUMENT PARSING
// ============================================================================
bool jsonOutputMode = false;
string? sarifOutputPath = null;  // If set, write SARIF to this file
string? targetPath = null;

for (int argIdx = 0; argIdx < args.Length; argIdx++)
{
    var arg = args[argIdx];
    if (arg == "--json")
    {
        jsonOutputMode = true;
    }
    else if (arg == "--sarif")
    {
        // Next argument is the output filename
        if (argIdx + 1 < args.Length)
        {
            sarifOutputPath = args[++argIdx];
        }
        else
        {
            Console.WriteLine("ERROR: --sarif requires an output filename");
            Environment.Exit(1);
            return;
        }
    }
    else if (!arg.StartsWith("-"))
    {
        targetPath = arg;
    }
}

// Show help if no path provided
if (targetPath == null)
{
    Console.WriteLine("Code Duplication Detector");
    Console.WriteLine("Usage: dotnet run --file Scripts\\CheckCodeDuplication.cs [options] <path>");
    Console.WriteLine();
    Console.WriteLine("Options:");
    Console.WriteLine("  --json           Output JSON instead of human-readable format");
    Console.WriteLine("  --sarif <file>   Write SARIF v2.1.0 output to file (for VSCode integration)");
    Console.WriteLine();
    Console.WriteLine("  <path> can be a directory (scans all source files) or a single file (self-dedup)");
    Console.WriteLine();
    Console.WriteLine("Examples:");
    Console.WriteLine("  dotnet run --file Scripts\\CheckCodeDuplication.cs EngineSrc");
    Console.WriteLine("  dotnet run --file Scripts\\CheckCodeDuplication.cs --json EngineSrc");
    Console.WriteLine("  dotnet run --file Scripts\\CheckCodeDuplication.cs --sarif duplication.sarif EngineSrc");
    Environment.Exit(1);
    return; // Unreachable, but tells compiler we're done
}

var inputPath = targetPath;
if (!Path.IsPathRooted(inputPath))
{
    inputPath = Path.Combine(Directory.GetCurrentDirectory(), inputPath);
}

string sourceRoot;
string[] csFiles;

if (File.Exists(inputPath))
{
    // Single file mode - self-deduplication
    sourceRoot = Path.GetDirectoryName(inputPath)!;
    csFiles = new[] { inputPath };
    if (!jsonOutputMode)
    {
        Console.WriteLine($"Single file mode: {Path.GetFileName(inputPath)}");
        Console.WriteLine($"(checking for internal duplication within the file)\n");
    }
}
else if (Directory.Exists(inputPath))
{
    // Directory mode - cross-file deduplication
    sourceRoot = inputPath;
    // Scan for all supported source file types
    var supportedExtensions = new[] {
        "*.cs", "*.ts", "*.tsx", "*.js", "*.jsx", "*.py",     // Original
        "*.go", "*.rs", "*.java", "*.c", "*.cpp", "*.h", "*.hpp",  // Go, Rust, Java, C/C++
        "*.css", "*.scss", "*.less", "*.rb", "*.html", "*.htm"     // CSS, Ruby, HTML
    };
    var allSourceFiles = new List<string>();
    int excludedCount = 0;
    foreach (var pattern in supportedExtensions)
    {
        foreach (var file in Directory.GetFiles(sourceRoot, pattern, SearchOption.AllDirectories))
        {
            if (IsExcludedPath(file))
            {
                excludedCount++;
            }
            else
            {
                allSourceFiles.Add(file);
            }
        }
    }
    csFiles = allSourceFiles.ToArray();
    if (!jsonOutputMode)
    {
        Console.WriteLine($"Scanning directory: {sourceRoot}");
        Console.WriteLine($"Found {csFiles.Length} source files ({excludedCount} excluded by directory pattern)\n");
    }
}
else
{
    if (!jsonOutputMode)
    {
        Console.WriteLine($"ERROR: Path not found: {inputPath}");
    }
    else
    {
        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver()
        };
        Console.WriteLine(JsonSerializer.Serialize(new { error = $"Path not found: {inputPath}" }, jsonOptions));
    }
    Environment.Exit(1);
    return; // Unreachable, but tells compiler we're done
}

// Helper to get relative path from source root
string GetRelativePath(string fullPath)
{
    return Path.GetRelativePath(sourceRoot, fullPath);
}

// Hash -> List of locations where this phrase appears
var phraseHashLocationMap = new Dictionary<string, List<CodeLocation>>();

// Debug mode: show whole line hashes
if (DEBUG && !jsonOutputMode)
{
    Console.WriteLine("=== DEBUG: WHOLE LINE HASH MODE ===\n");
    DebugWholeLineHashes(csFiles);
    Console.WriteLine("\n=== Continuing with token-based scanning ===\n");
}

// ONE-PASS ALGORITHM: For each file, detect fragments FIRST (against earlier files), THEN add hashes
var duplicateFragments = DetectDuplicateFragmentsOnePass(csFiles, phraseHashLocationMap, jsonOutputMode);

// Show hash histogram (now contains all files after one-pass completes)
if (!jsonOutputMode)
{
    DebugHashMatchHistogram(phraseHashLocationMap);
}

// ============================================================================
// OUTPUT - JSON or human-readable
// ============================================================================
if (jsonOutputMode)
{
    // JSON output for LLM/programmatic consumption
    var jsonOutput = new
    {
        description = "Code duplication detection results. Each fragment represents a block of code appearing in multiple locations. match_similarity: 1.0 = exact match. match_length: token count.",
        duplicates = duplicateFragments
            .OrderByDescending(f => f.TotalHits)
            .SelectMany(frag => frag.MatchLocations.Select(match => new
            {
                match_similarity = Math.Round((double)frag.TotalHits / (frag.TotalHits + frag.TotalMisses), 3),
                match_length = frag.EffectiveLength,
                source = $"{GetRelativePath(frag.SourceLocation.File)}:{frag.SourceLocation.LineNumber}",
                target = $"{GetRelativePath(match.File)}:{match.LineNumber}"
            }))
            .ToArray()
    };
    var jsonOptions = new JsonSerializerOptions
    {
        WriteIndented = true,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver()
    };
    Console.WriteLine(JsonSerializer.Serialize(jsonOutput, jsonOptions));
}
else
{
    // Human-readable output
    Console.WriteLine($"\n=== DUPLICATE CODE FRAGMENTS ===\n");
    Console.WriteLine($"Found {duplicateFragments.Count} duplicate fragments:\n");

    foreach (var frag in duplicateFragments.OrderByDescending(f => f.TotalHits))
    {
        var ratio = (double)frag.TotalHits / (frag.TotalHits + frag.TotalMisses);

        foreach (var match in frag.MatchLocations)
        {
            Console.WriteLine($"{ratio:P1} - {frag.EffectiveLength} tokens - {GetRelativePath(frag.SourceLocation.File)}:{frag.SourceLocation.LineNumber} ↔ {GetRelativePath(match.File)}:{match.LineNumber}");

            if (DEBUG_FRAGMENTS)
            {
                Console.WriteLine($"    DEBUG: TotalHits={frag.TotalHits}, TotalMisses={frag.TotalMisses}, EffectiveLength={frag.EffectiveLength}");
                Console.WriteLine($"    DEBUG: SourceSeq={frag.SourceLocation.TokenSequence}, MatchSeq={match.TokenSequence}");
                Console.WriteLine($"    DEBUG: Ratio calculation: {frag.TotalHits} / ({frag.TotalHits} + {frag.TotalMisses}) = {ratio:P4}");
            }
        }
    }
}

// ============================================================================
// SARIF OUTPUT - Write to file if --sarif was specified
// ============================================================================
if (sarifOutputPath != null)
{
    var sarifContent = GenerateSarifOutput(duplicateFragments, GetRelativePath, sourceRoot);
    File.WriteAllText(sarifOutputPath, sarifContent);
    Console.WriteLine($"\nSARIF output written to: {sarifOutputPath}");
    Console.WriteLine($"  ({duplicateFragments.Count} fragments → {duplicateFragments.Count * 2} bidirectional results)");
}

// ============================================================================
// EXIT CODE - For CI/CD integration
// Exit 0 = No duplicates found (clean)
// Exit 1 = Duplicates found above threshold
// ============================================================================
if (duplicateFragments.Count > 0)
{
    Environment.Exit(1);  // Duplicates found
}
// Exit 0 is implicit if we reach here (no duplicates)

// ============================================================================
// FUNCTIONS
// ============================================================================

List<string> TokenizeLine(string line)
{
    // Split on whitespace and punctuation but KEEP the delimiters
    var pattern = @"(\s+|[{}()\[\];,.<>!&|=+\-*/])";
    var parts = Regex.Split(line, pattern);

    // Unify all whitespace to single space, keep other tokens
    var tokens = new List<string>();
    foreach (var part in parts)
    {
        if (string.IsNullOrEmpty(part)) continue;

        if (string.IsNullOrWhiteSpace(part))
        {
            tokens.Add(" "); // Unified whitespace
        }
        else
        {
            tokens.Add(part);
        }
    }

    return tokens;
}

string ComputePhraseHash(List<string> tokens)
{
    var combined = string.Join("", tokens);
    using var md5 = MD5.Create();
    var hashBytes = md5.ComputeHash(Encoding.UTF8.GetBytes(combined));
    return Convert.ToHexString(hashBytes);
}

/// <summary>
/// Check if a phrase contains a run of repeated single-character tokens.
/// Returns true if there are REPEATED_CHAR_THRESHOLD or more identical single-char tokens in a row.
/// This filters out comment separators like "// ========" which create false positives.
/// </summary>
bool PhraseContainsRepeatedChars(List<string> tokens)
{
    if (tokens.Count < REPEATED_CHAR_THRESHOLD) return false;

    int repeatCount = 1;
    string? lastToken = null;

    foreach (var token in tokens)
    {
        // Only count single-character tokens (punctuation, operators)
        if (token.Length == 1)
        {
            if (token == lastToken)
            {
                repeatCount++;
                if (repeatCount >= REPEATED_CHAR_THRESHOLD)
                    return true;
            }
            else
            {
                repeatCount = 1;
                lastToken = token;
            }
        }
        else
        {
            // Multi-char token breaks the run
            repeatCount = 1;
            lastToken = null;
        }
    }

    return false;
}

/// <summary>
/// Threshold function: determines minimum token count based on hit ratio.
/// High ratio (100%) → fewer tokens needed (MIN_TOKENS_AT_HIGH_RATIO)
/// Low ratio (MIN_TRACKER_RATIO) → more tokens needed (MIN_TOKENS_AT_LOW_RATIO)
/// Linear interpolation between these extremes.
/// </summary>
int GetMinTokensForRatio(double hitRatio)
{
    // Clamp ratio to valid range
    var clampedRatio = Math.Clamp(hitRatio, MIN_TRACKER_RATIO, 1.0);

    // Linear interpolation: high ratio = low threshold, low ratio = high threshold
    var ratioRange = 1.0 - MIN_TRACKER_RATIO;
    var normalizedRatio = (clampedRatio - MIN_TRACKER_RATIO) / ratioRange; // 0 at min, 1 at max

    // Invert: high ratio should need fewer tokens
    var tokenRange = MIN_TOKENS_AT_LOW_RATIO - MIN_TOKENS_AT_HIGH_RATIO;
    return MIN_TOKENS_AT_HIGH_RATIO + (int)((1.0 - normalizedRatio) * tokenRange);
}

/// <summary>
/// Check if a tracker meets the quality threshold.
/// </summary>
bool TrackerMeetsThreshold(int hits, int misses)
{
    var totalTokens = hits + misses;
    if (totalTokens == 0) return false;

    var hitRatio = (double)hits / totalTokens;
    if (hitRatio < MIN_TRACKER_RATIO) return false;

    var minTokensRequired = GetMinTokensForRatio(hitRatio);
    return hits >= minTokensRequired;
}

/// <summary>
/// ONE-PASS ALGORITHM:
///
/// For each file:
///   1. Tokenize file → token sequence
///   2. Iterate token sequence: for each position, check hash → track if hit → then add hash
///
/// This ensures duplicates are only found when the LATER file is processed.
/// </summary>
List<DuplicateFragment> DetectDuplicateFragmentsOnePass(
    string[] allFiles,
    Dictionary<string, List<CodeLocation>> hashMap,
    bool suppressOutput)
{
    var reportedFragments = new List<DuplicateFragment>();
    var processedFragments = new HashSet<string>();

    if (!suppressOutput)
    {
        Console.WriteLine("Processing files:");
    }
    for (int fileIdx = 0; fileIdx < allFiles.Length; fileIdx++)
    {
        var filePath = allFiles[fileIdx];

        // STEP 1: Read file and apply content pre-filter (blanks comments, signatures, etc.)
        var rawContent = File.ReadAllText(filePath);
        var (filteredContent, languageTag) = ContentFilterEngine.ApplyFilter(rawContent, filePath);
        var lines = filteredContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

        // Format language tag for display: "[tsx]" or "[??]" for unknown
        var displayTag = languageTag ?? "??";

        // Print file header with language tag (3 chars max like DOS!)
        if (!suppressOutput)
        {
            Console.WriteLine($"  {fileIdx + 1,3}/{allFiles.Length} [{displayTag,-3}] {GetRelativePath(filePath)}");
        }
        var allTokens = new List<(string token, int lineNum)>();
        for (int lineIdx = 0; lineIdx < lines.Length; lineIdx++)
        {
            var lineTokens = TokenizeLine(lines[lineIdx]);
            foreach (var tok in lineTokens)
            {
                allTokens.Add((tok, lineIdx + 1));
            }
        }

        // Per-file stats (updated during processing)
        int fileHashHits = 0;  // number of times a hash matched something in the map
        int fileUniqueTrackers = 0;  // Running count of trackers created
        int fileLongestTracker = 0;
        int fileShortestTracker = int.MaxValue;
        const int PROGRESS_UPDATE_INTERVAL = 200;  // Update progress every N tokens
        int totalTokenCount = Math.Max(1, allTokens.Count - PHRASE_TOKEN_COUNT + 1);

        // Track active fragment trackers (keyed by target file)
        var activeTrackersPerTargetFile = new Dictionary<string, List<FileMatchTracker>>();
        var trackersHitThisIteration = new HashSet<FileMatchTracker>();

        // Helper to print ephemeral progress stats
        void PrintProgressStats(int tokenIdx, bool includePercent)
        {
            if (suppressOutput) return;
            var percent = (tokenIdx * 100) / totalTokenCount;
            var progressSuffix = includePercent ? $"  [{percent,3}% complete...]" : "";
            var statsLine = $"        lines:{lines.Length,5} hits:{fileHashHits,5} trackers:{fileUniqueTrackers,4} longest:{fileLongestTracker,4} shortest:{(fileShortestTracker == int.MaxValue ? 0 : fileShortestTracker),4}{progressSuffix}";
            Console.Write($"\r{statsLine.PadRight(100)}");
        }

        // STEP 2: Iterate token sequence - check hash, track if hit, then add hash
        for (int tokenIdx = 0; tokenIdx <= allTokens.Count - PHRASE_TOKEN_COUNT; tokenIdx++)
        {
            // Update progress every N tokens
            if (tokenIdx % PROGRESS_UPDATE_INTERVAL == 0)
            {
                PrintProgressStats(tokenIdx, includePercent: true);
            }

            var phraseTokens = allTokens.Skip(tokenIdx).Take(PHRASE_TOKEN_COUNT)
                .Select(t => t.token).ToList();
            var windowStartLine = allTokens[tokenIdx].lineNum;

            // Skip phrases with repeated characters (comment separators like "// ========")
            if (PhraseContainsRepeatedChars(phraseTokens))
            {
                // Still update miss counts for active trackers, but don't hash or check
                foreach (var trackerList in activeTrackersPerTargetFile.Values)
                {
                    foreach (var tracker in trackerList)
                    {
                        tracker.ConsecutiveMisses++;
                        tracker.LastTargetSequence++;
                    }
                }
                continue;
            }

            var hash = ComputePhraseHash(phraseTokens);

            trackersHitThisIteration.Clear();

            // CHECK: Does hash exist in map? (from EARLIER positions - including same file!)
            if (hashMap.TryGetValue(hash, out var matchingLocations))
            {
                fileHashHits++;
                foreach (var targetLoc in matchingLocations)
                {
                    // Get or create tracker list for this target file
                    if (!activeTrackersPerTargetFile.TryGetValue(targetLoc.File, out var trackersForFile))
                    {
                        trackersForFile = new List<FileMatchTracker>();
                        activeTrackersPerTargetFile[targetLoc.File] = trackersForFile;
                    }

                    // Try to find an existing tracker expecting this sequence
                    FileMatchTracker? matchingTracker = null;
                    foreach (var tracker in trackersForFile)
                    {
                        if (targetLoc.TokenSequence == tracker.LastTargetSequence + 1)
                        {
                            matchingTracker = tracker;
                            break;
                        }
                    }

                    if (matchingTracker != null)
                    {
                        // Continue existing tracker - commit any pending misses first
                        if (matchingTracker.ConsecutiveMisses > 0)
                        {
                            matchingTracker.Misses += matchingTracker.ConsecutiveMisses;
                            matchingTracker.ConsecutiveMisses = 0;
                        }
                        matchingTracker.Hits++;
                        matchingTracker.LastTargetSequence = targetLoc.TokenSequence;
                        matchingTracker.LastMatchLine = targetLoc.LineNumber;
                        trackersHitThisIteration.Add(matchingTracker);
                    }
                    else
                    {
                        // Start new tracker for this match
                        var newTracker = new FileMatchTracker
                        {
                            TargetFile = targetLoc.File,
                            FirstMatchLine = targetLoc.LineNumber,
                            FirstSourceTokenIdx = tokenIdx,
                            FirstSourceLine = windowStartLine,
                            LastTargetSequence = targetLoc.TokenSequence
                        };
                        newTracker.Hits = 1;
                        newTracker.LastMatchLine = targetLoc.LineNumber;
                        trackersForFile.Add(newTracker);
                        trackersHitThisIteration.Add(newTracker);
                        fileUniqueTrackers++;
                        // Update longest/shortest as trackers are created (they start with 1 hit)
                        if (1 > fileLongestTracker) fileLongestTracker = 1;
                        if (1 < fileShortestTracker) fileShortestTracker = 1;
                    }
                }
            }

            // Update tracker stats on hits
            foreach (var tracker in trackersHitThisIteration)
            {
                if (tracker.Hits > fileLongestTracker) fileLongestTracker = tracker.Hits;
            }

            // Update miss counts for trackers that didn't get a hit this iteration
            foreach (var trackerList in activeTrackersPerTargetFile.Values)
            {
                foreach (var tracker in trackerList)
                {
                    if (!trackersHitThisIteration.Contains(tracker))
                    {
                        tracker.ConsecutiveMisses++;
                        tracker.LastTargetSequence++;
                    }
                }
            }

            // ADD: Add this hash to the map (for FUTURE positions to find - including same file!)
            if (!hashMap.ContainsKey(hash))
            {
                hashMap[hash] = new List<CodeLocation>();
            }
            hashMap[hash].Add(new CodeLocation
            {
                File = filePath,
                LineNumber = windowStartLine,
                TokenSequence = tokenIdx
            });
        }

        // Harvest completed fragments from trackers
        int fileFragmentsKept = 0;

        foreach (var trackerList in activeTrackersPerTargetFile.Values)
        {
            foreach (var tracker in trackerList)
            {
                // Apply thresholding
                if (!TrackerMeetsThreshold(tracker.Hits, tracker.Misses))
                    continue;

                fileFragmentsKept++;
                {
                    // Key by BOTH source AND target to allow same source matching multiple targets
                    var fragKey = $"{filePath}:{tracker.FirstSourceLine}→{tracker.TargetFile}:{tracker.FirstMatchLine}";

                    if (!processedFragments.Contains(fragKey))
                    {
                        var fragment = new DuplicateFragment
                        {
                            SourceLocation = new CodeLocation
                            {
                                File = filePath,
                                LineNumber = tracker.FirstSourceLine,
                                TokenSequence = tracker.FirstSourceTokenIdx
                            },
                            TotalHits = tracker.Hits,
                            TotalMisses = tracker.Misses,
                            MatchLocations = new List<CodeLocation>
                            {
                                new CodeLocation
                                {
                                    File = tracker.TargetFile,
                                    LineNumber = tracker.FirstMatchLine,
                                    TokenSequence = 0
                                }
                            }
                        };

                        reportedFragments.Add(fragment);
                        processedFragments.Add(fragKey);
                    }
                }
            }
        }

        // Print final stats (without percentage, but with padding to overwrite old text) and newline
        if (!suppressOutput)
        {
            if (fileShortestTracker == int.MaxValue) fileShortestTracker = 0;
            var finalStatsLine = $"        lines:{lines.Length,5} hits:{fileHashHits,5} trackers:{fileUniqueTrackers,4} longest:{fileLongestTracker,4} shortest:{fileShortestTracker,4} kept:{fileFragmentsKept,3}";
            Console.WriteLine($"\r{finalStatsLine.PadRight(100)}");
        }
    }
    if (!suppressOutput)
    {
        Console.WriteLine("Processing complete!");
    }

    return reportedFragments;
}

// ============================================================================
// FUNCTIONS - DEBUG
// ============================================================================

void DebugWholeLineHashes(string[] files)
{
    foreach (var file in files)
    {
        Console.WriteLine($"\n{Path.GetFileName(file)}:");
        var lines = File.ReadAllLines(file);
        for (int i = 0; i < Math.Min(10, lines.Length); i++)
        {
            var normalized = Regex.Replace(lines[i].Trim(), @"\s+", " ");
            using var md5 = MD5.Create();
            var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(normalized));
            var hashStr = Convert.ToHexString(hash).Substring(0, 8);
            Console.WriteLine($"  Line {i + 1}: {hashStr} | {normalized.Substring(0, Math.Min(60, normalized.Length))}");
        }
    }
}

void DebugHashMatchHistogram(Dictionary<string, List<CodeLocation>> hashMap)
{
    Console.WriteLine("\n=== HASH MATCH FREQUENCY HISTOGRAM ===");

    // Group hashes by how many locations they have (skip unique phrases)
    var rawHistogram = hashMap
        .Where(kvp => kvp.Value.Count > 1)  // Only show duplicated phrases
        .GroupBy(kvp => kvp.Value.Count)
        .OrderBy(g => g.Key)
        .Select(g => new { Count = g.Key, Frequency = g.Count() })
        .ToList();

    if (rawHistogram.Count == 0)
    {
        Console.WriteLine("No duplicated phrases found.\n");
        return;
    }

    var totalHashes = hashMap.Count;
    var duplicatedHashes = hashMap.Count(kvp => kvp.Value.Count > 1);
    Console.WriteLine($"Total phrase hashes: {totalHashes:N0} ({duplicatedHashes:N0} duplicated)");

    // === TWO-PASS ALGORITHM ===
    // Pass 1: Find max frequency to calculate proper scale
    var maxFrequency = rawHistogram.Max(e => e.Frequency);
    const int MAX_BAR_WIDTH = 60;
    double scalePerHash = (double)MAX_BAR_WIDTH / maxFrequency;

    // Pass 2: Group consecutive buckets that wouldn't reach 1 hash mark
    var groupedHistogram = new List<(string Label, int CombinedFrequency)>();
    int rangeStart = -1;
    int rangeEnd = -1;
    int accumulatedFrequency = 0;

    for (int i = 0; i < rawHistogram.Count; i++)
    {
        var entry = rawHistogram[i];
        int entryBarWidth = (int)(entry.Frequency * scalePerHash);

        if (entryBarWidth < 1)
        {
            // This bucket would be invisible - accumulate into a range
            if (rangeStart < 0)
            {
                rangeStart = entry.Count;
            }
            rangeEnd = entry.Count;
            accumulatedFrequency += entry.Frequency;

            // Check if the accumulated range now reaches 1 hash mark
            int accumulatedBarWidth = (int)(accumulatedFrequency * scalePerHash);
            if (accumulatedBarWidth >= 1)
            {
                // Emit the grouped range
                string label = rangeStart == rangeEnd
                    ? $"{rangeStart,4}x"
                    : $"{rangeStart,3}-{rangeEnd}x";
                groupedHistogram.Add((label, accumulatedFrequency));
                // Reset accumulator
                rangeStart = -1;
                rangeEnd = -1;
                accumulatedFrequency = 0;
            }
        }
        else
        {
            // This bucket has at least 1 hash mark - flush any accumulated range first
            if (rangeStart >= 0)
            {
                string rangeLabel = rangeStart == rangeEnd
                    ? $"{rangeStart,4}x"
                    : $"{rangeStart,3}-{rangeEnd}x";
                groupedHistogram.Add((rangeLabel, accumulatedFrequency));
                rangeStart = -1;
                rangeEnd = -1;
                accumulatedFrequency = 0;
            }
            // Add this entry as its own row
            groupedHistogram.Add(($"{entry.Count,4}x", entry.Frequency));
        }
    }

    // Flush any remaining accumulated range at the end
    if (rangeStart >= 0 && accumulatedFrequency > 0)
    {
        string rangeLabel = rangeStart == rangeEnd
            ? $"{rangeStart,4}x"
            : $"{rangeStart,3}-{rangeEnd}x";
        groupedHistogram.Add((rangeLabel, accumulatedFrequency));
    }

    // Find max label width for right-justification
    int maxLabelWidth = groupedHistogram.Count > 0
        ? groupedHistogram.Max(g => g.Label.Length)
        : 0;

    // Print the grouped histogram
    Console.WriteLine($"\nOccurrence count → How many ({PHRASE_TOKEN_COUNT}-token) phrases:");
    foreach (var (label, frequency) in groupedHistogram)
    {
        int barWidth = Math.Max(1, (int)(frequency * scalePerHash));  // At least 1 now
        var bar = new string('#', Math.Min(MAX_BAR_WIDTH, barWidth));
        // Right-justify label by padding left to max width
        Console.WriteLine($"  {label.PadLeft(maxLabelWidth)}: {frequency,6:N0} {bar}");
    }
    Console.WriteLine();
}

// ============================================================================
// SARIF v2.1.0 OUTPUT GENERATION
// Per spec: _SPECS/TOOLING/CodeDuplicationSARIF.md
// Uses Dictionary + System.Text.Json (no SARIF NuGet dependency)
// ============================================================================

/// <summary>
/// Generate SARIF v2.1.0 JSON for duplicate fragments.
///
/// CRITICAL: Bidirectional reporting - each fragment creates TWO results:
///   1. Report on Source (referencing Target)
///   2. Report on Target (referencing Source)
/// This ensures warnings appear in BOTH files when the user opens either one.
/// </summary>
string GenerateSarifOutput(
    List<DuplicateFragment> fragments,
    Func<string, string> getRelativePath,
    string rootPath)
{
    var sarifResults = new List<object>();
    
    foreach (var frag in fragments.OrderByDescending(f => f.TotalHits))
    {
        var ratio = (double)frag.TotalHits / (frag.TotalHits + frag.TotalMisses);
        
        // Get BOTH Absolute (for URI) and Relative (for display text) paths
        var sourceAbsPath = frag.SourceLocation.File;
        var sourceRelPath = getRelativePath(sourceAbsPath);
        
        foreach (var targetLoc in frag.MatchLocations)
        {
            var targetAbsPath = targetLoc.File;
            var targetRelPath = getRelativePath(targetAbsPath);
            
            // Skip self-duplicates (same file) in SARIF - these cause VSCode Problems pane bugs
            // Console output and JSON still show them, but SARIF viewer can't handle self-refs
            if (sourceAbsPath == targetAbsPath) continue;
            
            // RESULT 1: Warning on Source file, referencing Target
            sarifResults.Add(CreateSarifResult(
                primaryAbsPath: sourceAbsPath,
                primaryRelPath: sourceRelPath,
                primaryLine: frag.SourceLocation.LineNumber,
                relatedAbsPath: targetAbsPath,
                relatedRelPath: targetRelPath,
                relatedLine: targetLoc.LineNumber,
                tokenCount: frag.EffectiveLength,
                similarityPercent: ratio * 100
            ));
            
            // RESULT 2: Warning on Target file, referencing Source
            // (Ensures user sees warning regardless of which file they open)
            sarifResults.Add(CreateSarifResult(
                primaryAbsPath: targetAbsPath,
                primaryRelPath: targetRelPath,
                primaryLine: targetLoc.LineNumber,
                relatedAbsPath: sourceAbsPath,
                relatedRelPath: sourceRelPath,
                relatedLine: frag.SourceLocation.LineNumber,
                tokenCount: frag.EffectiveLength,
                similarityPercent: ratio * 100
            ));
        }
    }
    
    // Build the full SARIF v2.1.0 document using Dictionary for $schema field
    // NOTE: Using absolute file:/// URIs - no originalUriBaseIds needed
    var sarifDocument = new Dictionary<string, object>
    {
        ["version"] = "2.1.0",
        ["$schema"] = "https://json.schemastore.org/sarif-2.1.0-rtm.5.json",
        ["runs"] = new[]
        {
            new Dictionary<string, object>
            {
                ["tool"] = new Dictionary<string, object>
                {
                    ["driver"] = new Dictionary<string, object>
                    {
                        ["name"] = "Context-Window-Guard",
                        ["informationUri"] = "https://github.com/your-repo/code-duplication-detector",
                        ["rules"] = new[]
                        {
                            new Dictionary<string, object>
                            {
                                ["id"] = "DUP001",
                                ["name"] = "CodeDuplication",
                                ["shortDescription"] = new Dictionary<string, object> { ["text"] = "Code duplication detected" },
                                ["fullDescription"] = new Dictionary<string, object> { ["text"] = "A block of code appears in multiple locations. Consider refactoring to reduce duplication and improve maintainability." },
                                ["helpUri"] = "https://refactoring.guru/smells/duplicate-code",
                                ["defaultConfiguration"] = new Dictionary<string, object> { ["level"] = "warning" }
                            }
                        }
                    }
                },
                // No originalUriBaseIds - using absolute file:/// URIs instead
                ["results"] = sarifResults
            }
        }
    };
    
    var jsonOptions = new JsonSerializerOptions
    {
        WriteIndented = true,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver()
    };
    
    return JsonSerializer.Serialize(sarifDocument, jsonOptions);
}

/// <summary>
/// Create a single SARIF result object with location and related location.
/// Uses absolute file:/// URIs which auto-encode spaces and special characters.
/// </summary>
object CreateSarifResult(
    string primaryAbsPath,
    string primaryRelPath,
    int primaryLine,
    string relatedAbsPath,
    string relatedRelPath,
    int relatedLine,
    int tokenCount,
    double similarityPercent)
{
    // Generate absolute file:/// URIs - Uri class handles encoding (spaces -> %20)
    var primaryFileUri = new Uri(primaryAbsPath).AbsoluteUri;
    var relatedFileUri = new Uri(relatedAbsPath).AbsoluteUri;
    
    return new Dictionary<string, object>
    {
        ["ruleId"] = "DUP001",
        ["level"] = "warning",
        ["message"] = new Dictionary<string, object>
        {
            // Use RELATIVE path for readable text, [text](id) links to relatedLocation id:1
            ["text"] = $"Code duplication detected ({tokenCount} tokens, {similarityPercent:F1}% similar). Matches: [{relatedRelPath}:{relatedLine}](1)"
        },
        ["locations"] = new[]
        {
            new Dictionary<string, object>
            {
                ["physicalLocation"] = new Dictionary<string, object>
                {
                    ["artifactLocation"] = new Dictionary<string, object>
                    {
                        ["uri"] = primaryFileUri  // Absolute file:/// URI
                    },
                    ["region"] = new Dictionary<string, object>
                    {
                        ["startLine"] = primaryLine
                    }
                }
            }
        },
        ["relatedLocations"] = new[]
        {
            new Dictionary<string, object>
            {
                ["id"] = 1,
                ["physicalLocation"] = new Dictionary<string, object>
                {
                    ["artifactLocation"] = new Dictionary<string, object>
                    {
                        ["uri"] = relatedFileUri  // Absolute file:/// URI
                    },
                    ["region"] = new Dictionary<string, object>
                    {
                        ["startLine"] = relatedLine
                    }
                },
                ["message"] = new Dictionary<string, object>
                {
                    ["text"] = "Other location of duplicated code"
                }
            }
        }
    };
}

// ============================================================================
// DATA STRUCTURES
// ============================================================================

record struct CodeLocation
{
    public string File;
    public int LineNumber;
    public int TokenSequence; // Position in file's token stream for adjacency checking
}

class FileMatchTracker
{
    public required string TargetFile { get; init; }
    public int FirstMatchLine { get; init; }
    public int FirstSourceTokenIdx { get; init; }
    public int FirstSourceLine { get; init; }
    public int LastMatchLine { get; set; }
    public int LastTargetSequence { get; set; }
    public int Hits { get; set; }
    public int Misses { get; set; }
    public int ConsecutiveMisses { get; set; } // Current miss streak - only committed to Misses on hit transition
}

record class DuplicateFragment
{
    public required CodeLocation SourceLocation { get; init; }
    public required int TotalHits { get; init; }
    public required int TotalMisses { get; init; }
    public required List<CodeLocation> MatchLocations { get; init; }

    // Effective length is just the hits (misses are gaps in the match)
    public int EffectiveLength => TotalHits;
}


// ============================================================================
// CONTENT PRE-FILTER - DATA-DRIVEN PATTERN MATCHING
// Blanks out expected patterns (comments, method signatures) before tokenization
// This reduces false positives from structurally-required code duplication
// ============================================================================

/// <summary>
/// SHARED PATTERN LIBRARY - Reusable regex patterns across languages.
/// Kept separate from execution logic for clarity.
/// </summary>
/// <remarks>
/// ╔══════════════════════════════════════════════════════════════════════════╗
/// ║  CRITICAL: Every pattern MUST have a LITERAL KEYWORD ANCHOR!             ║
/// ╠══════════════════════════════════════════════════════════════════════════╣
/// ║                                                                          ║
/// ║  Without a syntax-aware parser, these regex patterns are "dumb" - they   ║
/// ║  will match ANYTHING that fits the pattern. If you make keywords         ║
/// ║  optional with `?`, the pattern will match way too much!                 ║
/// ║                                                                          ║
/// ║  BAD:  `(public|private)?` - matches EVERYTHING, keyword is optional     ║
/// ║  GOOD: `(public|private)` - REQUIRES the keyword to match                ║
/// ║                                                                          ║
/// ║  Each pattern MUST require at least one literal keyword like:            ║
/// ║    - `using` for C# imports                                              ║
/// ║    - `import` or `export` for TS/JS                                      ║
/// ║    - `function` for JS function declarations                             ║
/// ║    - `public|private|protected` for method signatures                    ║
/// ║    - `<svg` for inline SVG elements                                      ║
/// ║                                                                          ║
/// ╚══════════════════════════════════════════════════════════════════════════╝
/// </remarks>
static class ContentFilterPatterns
{
    // ========================================================================
    // COMMENT PATTERNS
    // ========================================================================

    /// <summary>C-style block comments: /* ... */</summary>
    public static readonly Regex CStyleBlockComment = new Regex(
        @"/\*.*?\*/",
        RegexOptions.Singleline | RegexOptions.Compiled);

    /// <summary>C-style line comments: // ...</summary>
    public static readonly Regex CStyleLineComment = new Regex(
        @"//[^\r\n]*",
        RegexOptions.Compiled);

    /// <summary>Python/Shell hash comments: # ...</summary>
    public static readonly Regex HashComment = new Regex(
        @"#[^\r\n]*",
        RegexOptions.Compiled);

    /// <summary>Python docstrings: """ ... """ or ''' ... '''</summary>
    public static readonly Regex PythonDocstring = new Regex(
        @"("""""".*?""""""|'''.*?''')",
        RegexOptions.Singleline | RegexOptions.Compiled);

    // ========================================================================
    // IMPORT/USING PATTERNS
    // ========================================================================

    /// <summary>C# using directives: using System; using Foo.Bar;</summary>
    public static readonly Regex CSharpUsing = new Regex(
        @"^\s*using\s+[^;]+;",
        RegexOptions.Multiline | RegexOptions.Compiled);

    /// <summary>TypeScript/JavaScript import/export: import { x } from 'y'</summary>
    public static readonly Regex TSImportExport = new Regex(
        @"^\s*(import|export)\s+.*?(?:from\s+['""][^'""]+['""])?;?",
        RegexOptions.Multiline | RegexOptions.Compiled);

    /// <summary>Python import: import x, from x import y</summary>
    public static readonly Regex PythonImport = new Regex(
        @"^\s*(import\s+\S+|from\s+\S+\s+import\s+[^\r\n]+)",
        RegexOptions.Multiline | RegexOptions.Compiled);

    // ========================================================================
    // METHOD/FUNCTION SIGNATURE PATTERNS
    // ========================================================================

    /// <summary>C# method signatures: public void Foo(...)</summary>
    public static readonly Regex CSharpMethodSignature = new Regex(
        @"(public|private|protected|internal)\s+" +
        @"[\w<>\[\],\?\(\)\s]+" +
        @"\w+\s*" +
        @"\([^)]*\)",
        RegexOptions.Compiled);

    /// <summary>Python def/class: def foo(...): or class Foo(...):</summary>
    public static readonly Regex PythonDefinition = new Regex(
        @"^\s*(def|class)\s+\w+\s*\([^)]*\)\s*:",
        RegexOptions.Multiline | RegexOptions.Compiled);

    /// <summary>TypeScript function declarations: function name(...) - REQUIRES 'function' keyword</summary>
    public static readonly Regex TSFunctionDeclaration = new Regex(
        @"(async\s+)?function\s+\w+\s*\([^)]*\)\s*(:\s*[\w<>\[\]|&\s]+)?",
        RegexOptions.Compiled);

    /// <summary>TypeScript method signatures: public/private/protected methodName(...) - REQUIRES visibility keyword</summary>
    public static readonly Regex TSMethodSignature = new Regex(
        @"(public|private|protected)\s+(async\s+)?\w+\s*\([^)]*\)\s*(:\s*[\w<>\[\]|&\s]+)?",
        RegexOptions.Compiled);

    // ========================================================================
    // HTML/JSX INLINE DATA PATTERNS
    // ========================================================================

    /// <summary>Inline SVG - Attempt 1: Simple greedy</summary>
    public static readonly Regex InlineSvg_v1 = new Regex(
        @"<svg\b.*</svg>",
        RegexOptions.Singleline | RegexOptions.Compiled);

    /// <summary>Inline SVG - Attempt 2: Non-greedy with [\s\S]</summary>
    public static readonly Regex InlineSvg_v2 = new Regex(
        @"<svg\b[\s\S]*?</svg>",
        RegexOptions.Compiled);

    /// <summary>Inline SVG - Attempt 3: Simple greedy with Singleline</summary>
    public static readonly Regex InlineSvg_v3 = new Regex(
        @"<svg.*?</svg>",
        RegexOptions.Singleline | RegexOptions.Compiled);

    /// <summary>CDATA sections: &lt;![CDATA[...]]&gt;</summary>
    public static readonly Regex CDataSection = new Regex(
        @"<!\[CDATA\[.*?\]\]>",
        RegexOptions.Singleline | RegexOptions.Compiled);

    // ========================================================================
    // GO PATTERNS
    // ========================================================================

    /// <summary>Go single import: import "fmt"</summary>
    public static readonly Regex GoImportSingle = new Regex(
        @"import\s+""[^""]+""",
        RegexOptions.Compiled);

    /// <summary>Go grouped import: import ( ... )</summary>
    public static readonly Regex GoImportGroup = new Regex(
        @"import\s*\([^)]*\)",
        RegexOptions.Singleline | RegexOptions.Compiled);

    /// <summary>Go function: func name(...) or func (receiver) name(...)</summary>
    public static readonly Regex GoFunction = new Regex(
        @"func\s+(\([^)]+\)\s*)?\w+\s*\([^)]*\)",
        RegexOptions.Compiled);

    // ========================================================================
    // RUST PATTERNS
    // ========================================================================

    /// <summary>Rust use statement: use std::io;</summary>
    public static readonly Regex RustUse = new Regex(
        @"use\s+[\w:]+(\s*::\s*\{[^}]*\})?;",
        RegexOptions.Compiled);

    /// <summary>Rust function: fn name(...) or pub fn name(...)</summary>
    public static readonly Regex RustFunction = new Regex(
        @"(pub\s+)?fn\s+\w+\s*(<[^>]*>)?\s*\([^)]*\)",
        RegexOptions.Compiled);

    // ========================================================================
    // JAVA PATTERNS
    // ========================================================================

    /// <summary>Java import: import java.util.*;</summary>
    public static readonly Regex JavaImport = new Regex(
        @"import\s+(static\s+)?[\w.]+(\.\*)?;",
        RegexOptions.Compiled);

    /// <summary>Java package: package com.example;</summary>
    public static readonly Regex JavaPackage = new Regex(
        @"package\s+[\w.]+;",
        RegexOptions.Compiled);

    // ========================================================================
    // CSS PATTERNS
    // ========================================================================

    /// <summary>CSS @import: @import "file.css"; or @import url(...)</summary>
    public static readonly Regex CSSImport = new Regex(
        @"@import\s+[^;]+;",
        RegexOptions.Compiled);

    // ========================================================================
    // C/C++ PATTERNS
    // ========================================================================

    /// <summary>C/C++ #include: #include &lt;header&gt; or #include "header"</summary>
    public static readonly Regex CppInclude = new Regex(
        @"#include\s*[<""][^>""]+[>""]",
        RegexOptions.Compiled);

    /// <summary>C/C++ #define: #define MACRO</summary>
    public static readonly Regex CppDefine = new Regex(
        @"#define\s+\w+[^\r\n]*",
        RegexOptions.Compiled);

    // ========================================================================
    // RUBY PATTERNS
    // ========================================================================

    /// <summary>Ruby block comment: =begin ... =end</summary>
    public static readonly Regex RubyBlockComment = new Regex(
        @"=begin.*?=end",
        RegexOptions.Singleline | RegexOptions.Compiled);

    /// <summary>Ruby require: require 'foo' or require_relative 'foo'</summary>
    public static readonly Regex RubyRequire = new Regex(
        @"require(_relative)?\s+['""][^'""]+['""]",
        RegexOptions.Compiled);

    /// <summary>Ruby def: def method_name(...)</summary>
    public static readonly Regex RubyDef = new Regex(
        @"def\s+\w+(\s*\([^)]*\))?",
        RegexOptions.Compiled);

    /// <summary>Ruby class: class ClassName</summary>
    public static readonly Regex RubyClass = new Regex(
        @"class\s+\w+(\s*<\s*\w+)?",
        RegexOptions.Compiled);
}

/// <summary>
/// Language filter configuration - pure data, no behavior.
/// </summary>
record LanguageFilterConfig(
    string DisplayTag,
    string[] Extensions,
    Regex[] PatternsToBlank
);

/// <summary>
/// LANGUAGE CONFIGURATIONS - All registered languages and their patterns.
/// Kept separate from execution logic for clarity.
/// </summary>
static class ContentFilterConfigs
{
    // Shared pattern sets for reuse
    // ORDER MATTERS! SVG must run before TSMethodSignature which incorrectly matches "return (...)"
    private static readonly Regex[] TSPatterns = {
        ContentFilterPatterns.CStyleBlockComment,
        ContentFilterPatterns.CStyleLineComment,
        ContentFilterPatterns.TSImportExport,
        ContentFilterPatterns.InlineSvg_v1,  // SVG MUST BE EARLY - before method sig patterns
        ContentFilterPatterns.InlineSvg_v2,
        ContentFilterPatterns.InlineSvg_v3,
        ContentFilterPatterns.TSFunctionDeclaration,
        ContentFilterPatterns.TSMethodSignature,
        ContentFilterPatterns.CDataSection
    };

    private static readonly Regex[] PythonPatterns = {
        ContentFilterPatterns.PythonDocstring,  // Must come before hash comments!
        ContentFilterPatterns.HashComment,
        ContentFilterPatterns.PythonImport,
        ContentFilterPatterns.PythonDefinition
    };

    public static readonly LanguageFilterConfig[] AllConfigs =
    {
        // C# configuration
        new("cs", new[] { ".cs" }, new[] {
            ContentFilterPatterns.CStyleBlockComment,
            ContentFilterPatterns.CStyleLineComment,
            ContentFilterPatterns.CSharpUsing,
            ContentFilterPatterns.CSharpMethodSignature
        }),
        
        // TypeScript family - each extension gets its own 3-char tag
        new("ts",  new[] { ".ts" },  TSPatterns),
        new("tsx", new[] { ".tsx" }, TSPatterns),
        new("js",  new[] { ".js" },  TSPatterns),
        new("jsx", new[] { ".jsx" }, TSPatterns),
        
        // HTML files with inline content
        new("htm", new[] { ".html", ".htm" }, new[] {
            ContentFilterPatterns.InlineSvg_v1,
            ContentFilterPatterns.InlineSvg_v2,
            ContentFilterPatterns.InlineSvg_v3,
            ContentFilterPatterns.CDataSection
        }),
        
        // Python configuration
        new("py",  new[] { ".py" }, PythonPatterns),
        
        // Go configuration
        new("go", new[] { ".go" }, new[] {
            ContentFilterPatterns.CStyleBlockComment,
            ContentFilterPatterns.CStyleLineComment,
            ContentFilterPatterns.GoImportSingle,
            ContentFilterPatterns.GoImportGroup,
            ContentFilterPatterns.GoFunction
        }),
        
        // Rust configuration
        new("rs", new[] { ".rs" }, new[] {
            ContentFilterPatterns.CStyleBlockComment,
            ContentFilterPatterns.CStyleLineComment,
            ContentFilterPatterns.RustUse,
            ContentFilterPatterns.RustFunction
        }),
        
        // Java configuration (method signatures share C# pattern)
        new("jav", new[] { ".java" }, new[] {
            ContentFilterPatterns.CStyleBlockComment,
            ContentFilterPatterns.CStyleLineComment,
            ContentFilterPatterns.JavaPackage,
            ContentFilterPatterns.JavaImport,
            ContentFilterPatterns.CSharpMethodSignature  // Reuse - same visibility keywords
        }),
        
        // C/C++ configuration
        new("c", new[] { ".c", ".h" }, new[] {
            ContentFilterPatterns.CStyleBlockComment,
            ContentFilterPatterns.CStyleLineComment,
            ContentFilterPatterns.CppInclude,
            ContentFilterPatterns.CppDefine
        }),
        new("cpp", new[] { ".cpp", ".hpp" }, new[] {
            ContentFilterPatterns.CStyleBlockComment,
            ContentFilterPatterns.CStyleLineComment,
            ContentFilterPatterns.CppInclude,
            ContentFilterPatterns.CppDefine
        }),
        
        // CSS family configuration
        new("css", new[] { ".css", ".scss", ".less" }, new[] {
            ContentFilterPatterns.CStyleBlockComment,
            ContentFilterPatterns.CSSImport
        }),
        
        // Ruby configuration
        new("rb", new[] { ".rb" }, new[] {
            ContentFilterPatterns.RubyBlockComment,  // =begin/=end - must be before # comments
            ContentFilterPatterns.HashComment,
            ContentFilterPatterns.RubyRequire,
            ContentFilterPatterns.RubyDef,
            ContentFilterPatterns.RubyClass
        })
    };
}

/// <summary>
/// CONTENT FILTER ENGINE - Execution logic only.
/// Applies pattern configs to content, completely data-driven.
/// </summary>
static class ContentFilterEngine
{
    // Debug flag for filter pattern matching - set to true to see when patterns match
    private const bool DEBUG_FILTERS = false;

    /// <summary>
    /// Replace all regex matches with spaces (preserving string length and newlines).
    /// </summary>
    private static string BlankMatches(string content, Regex pattern, string? patternName = null)
    {
        int matchCountForPattern = 0;
        var result = pattern.Replace(content, match =>
        {
            matchCountForPattern++;
            if (DEBUG_FILTERS)
            {
                var preview = match.Value.Length > 60
                    ? match.Value.Substring(0, 60).Replace("\n", "\\n").Replace("\r", "") + "..."
                    : match.Value.Replace("\n", "\\n").Replace("\r", "");
                Console.WriteLine($"    [FILTER] {patternName ?? pattern.ToString()} matched: {match.Value.Length} chars: {preview}");
            }
            var sb = new StringBuilder(match.Length);
            foreach (char c in match.Value)
            {
                sb.Append(c == '\n' || c == '\r' ? c : ' ');
            }
            return sb.ToString();
        });

        if (DEBUG_FILTERS && matchCountForPattern > 0)
        {
            Console.WriteLine($"    [FILTER] {patternName ?? pattern.ToString()}: {matchCountForPattern} total matches");
        }

        return result;
    }

    /// <summary>
    /// Apply filter if a matching config exists, otherwise return content unchanged.
    /// Returns filtered content and display tag (or null if no filter).
    /// </summary>
    public static (string FilteredContent, string? DisplayTag) ApplyFilter(string content, string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        foreach (var config in ContentFilterConfigs.AllConfigs)
        {
            if (config.Extensions.Contains(extension))
            {
                // Apply all patterns for this language
                if (DEBUG_FILTERS)
                {
                    Console.WriteLine($"    [FILTER] Applying {config.PatternsToBlank.Length} patterns for [{config.DisplayTag}]");
                }
                foreach (var pattern in config.PatternsToBlank)
                {
                    content = BlankMatches(content, pattern, pattern.ToString().Substring(0, Math.Min(40, pattern.ToString().Length)));
                }
                return (content, config.DisplayTag);
            }
        }

        return (content, null);
    }
}