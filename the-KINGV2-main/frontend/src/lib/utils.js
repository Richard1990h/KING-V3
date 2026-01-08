import { clsx } from "clsx";
import { twMerge } from "tailwind-merge";

export function cn(...inputs) {
    return twMerge(clsx(inputs));
}

// Language configurations
export const LANGUAGES = {
    Python: { extension: '.py', icon: 'Code', color: '#3776AB' },
    JavaScript: { extension: '.js', icon: 'Braces', color: '#F7DF1E' },
    TypeScript: { extension: '.ts', icon: 'Braces', color: '#3178C6' },
    Java: { extension: '.java', icon: 'Coffee', color: '#ED8B00' },
    'C#': { extension: '.cs', icon: 'Hash', color: '#68217A' },
    Go: { extension: '.go', icon: 'Zap', color: '#00ADD8' }
};

// Get file icon based on extension
export function getFileIcon(filename) {
    const ext = filename.split('.').pop()?.toLowerCase();
    const icons = {
        py: 'FileCode',
        js: 'FileJson',
        jsx: 'FileJson',
        ts: 'FileCode',
        tsx: 'FileCode',
        java: 'Coffee',
        cs: 'Hash',
        go: 'Zap',
        json: 'FileJson',
        md: 'FileText',
        txt: 'FileText',
        html: 'Globe',
        css: 'Palette',
        default: 'File'
    };
    return icons[ext] || icons.default;
}

// Format date
export function formatDate(dateString) {
    const date = new Date(dateString);
    return date.toLocaleDateString('en-US', {
        year: 'numeric',
        month: 'short',
        day: 'numeric'
    });
}

// Format relative time
export function formatRelativeTime(dateString) {
    const date = new Date(dateString);
    const now = new Date();
    const diff = now - date;
    
    const minutes = Math.floor(diff / 60000);
    const hours = Math.floor(diff / 3600000);
    const days = Math.floor(diff / 86400000);
    
    if (minutes < 1) return 'just now';
    if (minutes < 60) return `${minutes}m ago`;
    if (hours < 24) return `${hours}h ago`;
    if (days < 7) return `${days}d ago`;
    return formatDate(dateString);
}

// Format credits
export function formatCredits(credits) {
    return new Intl.NumberFormat('en-US', {
        minimumFractionDigits: 0,
        maximumFractionDigits: 2
    }).format(credits);
}

// Format currency
export function formatCurrency(amount) {
    return new Intl.NumberFormat('en-US', {
        style: 'currency',
        currency: 'USD'
    }).format(amount);
}

// Debounce function
export function debounce(func, wait) {
    let timeout;
    return function executedFunction(...args) {
        const later = () => {
            clearTimeout(timeout);
            func(...args);
        };
        clearTimeout(timeout);
        timeout = setTimeout(later, wait);
    };
}

// Agent colors mapping
export const AGENT_COLORS = {
    planner: '#D946EF',
    researcher: '#06B6D4',
    developer: '#10B981',
    test_designer: '#F59E0B',
    executor: '#3B82F6',
    debugger: '#EF4444',
    verifier: '#8B5CF6'
};

// Get syntax highlighting language
export function getSyntaxLanguage(filename) {
    const ext = filename.split('.').pop()?.toLowerCase();
    const languages = {
        py: 'python',
        js: 'javascript',
        jsx: 'jsx',
        ts: 'typescript',
        tsx: 'tsx',
        java: 'java',
        cs: 'csharp',
        go: 'go',
        json: 'json',
        md: 'markdown',
        html: 'html',
        css: 'css'
    };
    return languages[ext] || 'text';
}

// Translations
export const TRANSLATIONS = {
    en: {
        welcome: 'Welcome',
        projects: 'Projects',
        newProject: 'New Project',
        credits: 'Credits',
        settings: 'Settings',
        logout: 'Logout',
        build: 'Build',
        run: 'Run',
        save: 'Save',
        export: 'Export',
        chat: 'Chat',
        output: 'Output',
        files: 'Files',
        noFiles: 'No files yet',
        createFile: 'Create file',
        deleteFile: 'Delete file',
        send: 'Send',
        typeMessage: 'Describe what you want to build...'
    },
    es: {
        welcome: 'Bienvenido',
        projects: 'Proyectos',
        newProject: 'Nuevo Proyecto',
        credits: 'Créditos',
        settings: 'Configuración',
        logout: 'Cerrar sesión',
        build: 'Compilar',
        run: 'Ejecutar',
        save: 'Guardar',
        export: 'Exportar',
        chat: 'Chat',
        output: 'Salida',
        files: 'Archivos',
        noFiles: 'Sin archivos',
        createFile: 'Crear archivo',
        deleteFile: 'Eliminar archivo',
        send: 'Enviar',
        typeMessage: 'Describe lo que quieres construir...'
    },
    fr: {
        welcome: 'Bienvenue',
        projects: 'Projets',
        newProject: 'Nouveau Projet',
        credits: 'Crédits',
        settings: 'Paramètres',
        logout: 'Déconnexion',
        build: 'Compiler',
        run: 'Exécuter',
        save: 'Sauvegarder',
        export: 'Exporter',
        chat: 'Chat',
        output: 'Sortie',
        files: 'Fichiers',
        noFiles: 'Pas de fichiers',
        createFile: 'Créer un fichier',
        deleteFile: 'Supprimer le fichier',
        send: 'Envoyer',
        typeMessage: 'Décrivez ce que vous voulez construire...'
    },
    de: {
        welcome: 'Willkommen',
        projects: 'Projekte',
        newProject: 'Neues Projekt',
        credits: 'Guthaben',
        settings: 'Einstellungen',
        logout: 'Abmelden',
        build: 'Bauen',
        run: 'Ausführen',
        save: 'Speichern',
        export: 'Exportieren',
        chat: 'Chat',
        output: 'Ausgabe',
        files: 'Dateien',
        noFiles: 'Keine Dateien',
        createFile: 'Datei erstellen',
        deleteFile: 'Datei löschen',
        send: 'Senden',
        typeMessage: 'Beschreiben Sie, was Sie bauen möchten...'
    },
    zh: {
        welcome: '欢迎',
        projects: '项目',
        newProject: '新项目',
        credits: '积分',
        settings: '设置',
        logout: '登出',
        build: '构建',
        run: '运行',
        save: '保存',
        export: '导出',
        chat: '聊天',
        output: '输出',
        files: '文件',
        noFiles: '暂无文件',
        createFile: '创建文件',
        deleteFile: '删除文件',
        send: '发送',
        typeMessage: '描述你想构建的内容...'
    },
    ja: {
        welcome: 'ようこそ',
        projects: 'プロジェクト',
        newProject: '新規プロジェクト',
        credits: 'クレジット',
        settings: '設定',
        logout: 'ログアウト',
        build: 'ビルド',
        run: '実行',
        save: '保存',
        export: 'エクスポート',
        chat: 'チャット',
        output: '出力',
        files: 'ファイル',
        noFiles: 'ファイルがありません',
        createFile: 'ファイルを作成',
        deleteFile: 'ファイルを削除',
        send: '送信',
        typeMessage: '作りたいものを説明してください...'
    }
};

export function t(key, language = 'en') {
    return TRANSLATIONS[language]?.[key] || TRANSLATIONS.en[key] || key;
}
