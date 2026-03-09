/**
 * @fileoverview Enforces that namespace imports match the filename/directory being imported from
 * @description When using `import * as foo from './path/foo'`, ensures the namespace name matches the file/directory name
 * @author David Jeske and Claude Opus 4.1 as Kilo Code
 */
"use strict";

const path = require('path');

module.exports = {
  meta: {
    type: "suggestion",
    docs: {
      description: "Enforces that namespace imports match the filename or directory name being imported from",
      category: "Best Practices",
      recommended: true,
    },
    fixable: "code",
    schema: [
      {
        type: "object",
        properties: {
          ignoreCase: {
            type: "boolean",
            default: false
          },
          allowIndexFiles: {
            type: "boolean", 
            default: true
          }
        },
        additionalProperties: false
      }
    ],
    messages: {
      mismatchedNamespace: "Namespace import '{{namespace}}' should match filename '{{expected}}' from '{{importPath}}'",
      mismatchedNamespaceWithAlternative: "Namespace import '{{namespace}}' should match '{{expected}}' or '{{alternative}}' from '{{importPath}}'",
      indexFileHint: "Namespace import '{{namespace}}' from index file should match parent directory '{{expected}}'"
    }
  },

  create: function(context) {
    const options = context.options[0] || {};
    const ignoreCase = options.ignoreCase || false;
    const allowIndexFiles = options.allowIndexFiles !== false;

    return {
      ImportDeclaration(node) {
        // Only check namespace imports (import * as X from 'path')
        if (node.specifiers.length !== 1 || 
            node.specifiers[0].type !== 'ImportNamespaceSpecifier') {
          return;
        }

        const namespaceSpecifier = node.specifiers[0];
        const namespaceName = namespaceSpecifier.local.name;
        const importPath = node.source.value;

        // Parse the import path to get the filename/directory name
        const pathParts = importPath.split('/');
        let expectedName = pathParts[pathParts.length - 1];

        // Remove file extensions
        expectedName = expectedName.replace(/\.(ts|tsx|js|jsx|mjs|cjs)$/, '');
        
        // For non-relative imports (like 'fs/promises'), also allow underscore-joined format
        let alternativeExpectedName = null;
        if (!importPath.startsWith('.') && !importPath.startsWith('/') && pathParts.length > 1) {
          // Create underscore-joined version: 'fs/promises' -> 'fs_promises'
          alternativeExpectedName = pathParts.join('_').replace(/\.(ts|tsx|js|jsx|mjs|cjs)$/, '');
        }

        // Skip external packages that don't have slashes (single-part module names)
        if (!importPath.startsWith('.') && !importPath.startsWith('/') && pathParts.length === 1) {
          return;
        }

        // Handle index files - use parent directory name
        if (allowIndexFiles && expectedName === 'index') {
          if (pathParts.length > 1) {
            expectedName = pathParts[pathParts.length - 2];
            
            // Report with special message for index files
            if (!compareNames(namespaceName, expectedName, ignoreCase)) {
              context.report({
                node: namespaceSpecifier,
                messageId: "indexFileHint",
                data: {
                  namespace: namespaceName,
                  expected: expectedName
                },
                fix(fixer) {
                  return fixer.replaceText(namespaceSpecifier.local, expectedName);
                }
              });
            }
            return;
          }
        }

        // Check if namespace matches the expected name or alternative name
        const matchesExpected = compareNames(namespaceName, expectedName, ignoreCase);
        const matchesAlternative = alternativeExpectedName && compareNames(namespaceName, alternativeExpectedName, ignoreCase);
        
        if (!matchesExpected && !matchesAlternative) {
          // Use different message if there's an alternative
          const messageId = alternativeExpectedName ? "mismatchedNamespaceWithAlternative" : "mismatchedNamespace";
          const data = {
            namespace: namespaceName,
            expected: expectedName,
            importPath: importPath
          };
          
          if (alternativeExpectedName) {
            data.alternative = alternativeExpectedName;
          }
          
          context.report({
            node: namespaceSpecifier,
            messageId: messageId,
            data: data,
            fix(fixer) {
              // Prefer the underscore version for non-relative imports when available
              const fixName = alternativeExpectedName || expectedName;
              return fixer.replaceText(namespaceSpecifier.local, fixName);
            }
          });
        }
      }
    };
  }
};

/**
 * Compare two names with optional case-insensitive comparison
 */
function compareNames(name1, name2, ignoreCase) {
  if (ignoreCase) {
    return name1.toLowerCase() === name2.toLowerCase();
  }
  return name1 === name2;
}