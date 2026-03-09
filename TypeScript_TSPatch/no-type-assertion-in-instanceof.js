'use strict';

/**
 * ESLint rule to disallow type assertions in instanceof expressions.
 * Type assertions in instanceof checks are misleading and don't affect runtime behavior.
 */
module.exports = {
  meta: {
    type: 'problem',
    docs: {
      description: 'Disallow type assertions in instanceof expressions',
      category: 'Type Safety',
      recommended: true
    },
    messages: {
      noTypeAssertion: 'Avoid type assertions in instanceof expressions. The type assertion is misleading.'
    },
    schema: []
  },
  
  create(context) {
    return {
      BinaryExpression(node) {
        if (node.operator !== 'instanceof') {
          return;
        }
        
        // Check if left side has a type assertion (as expression)
        if (node.left.type === 'TSAsExpression' || node.left.type === 'TSTypeAssertion') {
          context.report({
            node: node.left,
            messageId: 'noTypeAssertion'
          });
        }
      }
    };
  }
};