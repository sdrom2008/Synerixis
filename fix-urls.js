const fs = require('fs');
const path = require('path');

const configPath = path.join(__dirname, 'frontend/src/utils/config.js');
fs.writeFileSync(configPath, "export const BASE_URL = 'https://api.synerixis.com';\n");

function replaceInFile(filePath) {
    let content = fs.readFileSync(filePath, 'utf8');
    let original = content;

    // Pattern for testbase
    content = content.replace(/const testbase\s*=\s*['"]http:\/\/[0-9\.]+:\d+['"];?/g, "import { BASE_URL as testbase } from '@/utils/config.js';");
    
    // Pattern for BASE_URL declaration
    content = content.replace(/const BASE_URL\s*=\s*['"]http:\/\/[0-9\.]+:\d+['"](;|\s*\/\/.*)?/g, "import { BASE_URL } from '@/utils/config.js';");

    // Replace direct http://localhost:7092 occurrences to use BASE_URL, but this needs import.
    if (content.includes("'http://localhost:7092")) {
        if (!content.includes("@/utils/config.js")) {
            content = content.replace(/<script[^>]*>/, "$&\nimport { BASE_URL } from '@/utils/config.js';");
        }
        content = content.replace(/'http:\/\/localhost:7092([^']*)'/g, "`${BASE_URL}$1`");
    }

    if (content !== original) {
        fs.writeFileSync(filePath, content, 'utf8');
        console.log(`Updated: ${filePath}`);
    }
}

function walk(dir) {
    const list = fs.readdirSync(dir);
    list.forEach(file => {
        const fullPath = path.join(dir, file);
        const stat = fs.statSync(fullPath);
        if (stat && stat.isDirectory()) {
            walk(fullPath);
        } else if (fullPath.endsWith('.vue') || fullPath.endsWith('.ts') || fullPath.endsWith('.js')) {
            replaceInFile(fullPath);
        }
    });
}

walk(path.join(__dirname, 'frontend/src'));
