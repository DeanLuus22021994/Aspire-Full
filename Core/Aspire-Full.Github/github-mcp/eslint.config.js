import js from "@eslint/js";
import tsPlugin from "@typescript-eslint/eslint-plugin";
import tsParser from "@typescript-eslint/parser";
import globals from "globals";

const restrictedMessage = "Use src/mcp-support exports instead of importing from @modelcontextprotocol/sdk directly.";

export default [
  {
    ignores: ["dist/**"]
  },
  {
    files: ["src/**/*.ts"],
    languageOptions: {
      ...js.configs.recommended.languageOptions,
      ecmaVersion: "latest",
      sourceType: "module",
      parser: tsParser,
      globals: {
        ...globals.node,
        ...globals.browser
      }
    },
    plugins: {
      "@typescript-eslint": tsPlugin
    },
    rules: {
      ...js.configs.recommended.rules,
      "no-unused-vars": "off",
      "@typescript-eslint/no-unused-vars": [
        "warn",
        { argsIgnorePattern: "^_", varsIgnorePattern: "^_" }
      ],
      "no-restricted-imports": [
        "error",
        {
          paths: [
            {
              name: "@modelcontextprotocol/sdk",
              message: restrictedMessage
            }
          ],
          patterns: [
            {
              group: ["@modelcontextprotocol/sdk/*"],
              message: restrictedMessage
            }
          ]
        }
      ]
    }
  },
  {
    files: ["src/mcp-support/**/*.{ts,tsx}"],
    rules: {
      "no-restricted-imports": "off"
    }
  }
];
