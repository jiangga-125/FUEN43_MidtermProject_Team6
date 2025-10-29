const path = require('path');
const { styles } = require('@ckeditor/ckeditor5-dev-utils');
const { CKEditorTranslationsPlugin } = require('@ckeditor/ckeditor5-dev-translations');

module.exports = {
    mode: 'production',
    entry: './src/editor.js',
    output: {
        path: path.resolve(__dirname, 'build'),
        filename: 'ckeditor.js',
        library: {
            type: 'umd',
            name: 'ClassicEditor',
            export: 'default' // 讓全域直接是 default，頁面可直接 ClassicEditor.create(...)
        }
    },
    module: {
        rules: [
            // CKEditor 樣式
            {
                test: /\.css$/i,
                use: [
                    { loader: 'style-loader' },
                    { loader: 'css-loader', options: { importLoaders: 1 } },
                    {
                        loader: 'postcss-loader',
                        options: {
                            postcssOptions: styles.getPostCssConfig({
                                themeImporter: { themePath: require.resolve('@ckeditor/ckeditor5-theme-lark') },
                                minify: true
                            })
                        }
                    }
                ]
            },
            // ✅ CKEditor 的 icon 必須吃「原始 SVG 文字」
            { test: /\.svg$/, use: ['raw-loader'] },

            // 其他資源：圖片 / 字型用 asset
            { test: /\.(png|jpg|gif|ttf|woff2?)$/, type: 'asset' }
        ]
    },
    plugins: [
        new CKEditorTranslationsPlugin({
            language: 'zh',                 // 想要繁中改 'zh'
            additionalLanguages: 'all',
            addMainLanguageTranslationsToAllAssets: true
        })
    ]
};
