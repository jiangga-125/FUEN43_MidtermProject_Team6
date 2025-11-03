/* eslint-env node */
// eslint-disable-next-line no-undef
module.exports = {
  root: true,
  env: { browser: true, es2021: true, node: true },
  parser: 'vue-eslint-parser',
  parserOptions: {
    parser: '@typescript-eslint/parser',
    ecmaVersion: 'latest',
    sourceType: 'module',
    extraFileExtensions: ['.vue'],
  },
  extends: ['eslint:recommended', 'plugin:vue/vue3-recommended', '@vue/eslint-config-typescript'],
  plugins: ['vue', '@typescript-eslint'],
}
