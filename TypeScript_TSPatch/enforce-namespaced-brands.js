/**
 * @fileoverview Enforces that branded types are defined within a namespace.
 * @author Gemini (running as Kilo Code)
 */
"use strict";

module.exports = {
  meta: {
    type: "suggestion",
    docs: {
      description: "Enforces that branded types are defined within a namespace to improve clarity and prevent top-level pollution.",
      category: "Best Practices",
      recommended: true,
    },
    fixable: null,
    schema: [],
    messages: {
      brandInNamespace: "WARNING: type brands are essential, but top level type brands are discouraged, please place the type brand in a namespace that matches the primary user of this type brand"
    }
  },

  create: function(context) {
    return {
      TSTypeAliasDeclaration(node) {
        const isBrand = node.typeAnnotation &&
          node.typeAnnotation.type === 'TSTypeReference' &&
          node.typeAnnotation.typeName &&
          node.typeAnnotation.typeName.name === 'Brand';

        if (!isBrand) {
          return;
        }

        let inNamespace = false;
        let parent = node.parent;
        while (parent) {
          if (parent.type === 'TSModuleDeclaration') { // TSModuleDeclaration is a namespace
            inNamespace = true;
            break;
          }
          parent = parent.parent;
        }

        if (!inNamespace) {
            // Report if the brand is defined at the top level.
            // This happens if its parent is the Program, or an ExportNamedDeclaration
            // whose parent is the Program.
            if(node.parent.type === 'Program' || (node.parent.type === 'ExportNamedDeclaration' && node.parent.parent.type === 'Program')) {
                 context.report({
                    node: node,
                    messageId: "brandInNamespace",
                });
            }
        }
      },
    };
  },
};