// D:\Ryhs-Huang_FUEN43_MidtermProject_Team6\BookLoop\ckeditor5-oss-build\src\editor.js

// 直接用「套件根」的具名匯入（新版 CKEditor 5 做法）
import { ClassicEditor } from '@ckeditor/ckeditor5-editor-classic';

import { Essentials } from '@ckeditor/ckeditor5-essentials';
import { Paragraph } from '@ckeditor/ckeditor5-paragraph';
import { Heading } from '@ckeditor/ckeditor5-heading';

import { Bold, Italic } from '@ckeditor/ckeditor5-basic-styles';
import { Link } from '@ckeditor/ckeditor5-link';
import { List } from '@ckeditor/ckeditor5-list';
import { BlockQuote } from '@ckeditor/ckeditor5-block-quote';

import { Table, TableToolbar } from '@ckeditor/ckeditor5-table';

import {
    Image,
    ImageCaption,
    ImageStyle,
    ImageToolbar,
    ImageResize,
    ImageUpload
} from '@ckeditor/ckeditor5-image';

import { SimpleUploadAdapter } from '@ckeditor/ckeditor5-upload';
import { PasteFromOffice } from '@ckeditor/ckeditor5-paste-from-office';

// 這裡不再繼承；直接調整套件自帶的 ClassicEditor
ClassicEditor.builtinPlugins = [
    Essentials, Paragraph, Heading,
    Bold, Italic, Link, List, BlockQuote,
    Table, TableToolbar,
    Image, ImageCaption, ImageStyle, ImageToolbar, ImageResize, ImageUpload,
    SimpleUploadAdapter,
    PasteFromOffice
];

// 基本設定（語系在 webpack 由 CKEditorTranslationsPlugin 打進去）
ClassicEditor.defaultConfig = {
    language: 'zh-cn', // 要繁中改 'zh'
    toolbar: {
        items: [
            'heading', '|',
            'bold', 'italic', 'link', '|',
            'bulletedList', 'numberedList', 'blockQuote', '|',
            'insertTable', '|',
            'imageUpload', '|',
            'undo', 'redo'
        ]
    },
    table: { contentToolbar: ['tableColumn', 'tableRow', 'mergeTableCells'] }
};

// 仍然輸出預設的 ClassicEditor（供 UMD 全域使用，也可被 ESM import）
export default ClassicEditor;
