#!/usr/bin/env node
/**
 * fix-heap-exports.js
 *
 * Patches the Emscripten-generated DotNetWasmDemo.js to expose HEAP arrays
 * on the Module object. NativeAOT-LLVM runtime expects Module.HEAP32 etc.
 * to exist, but Emscripten only creates local variables.
 */

const fs = require('fs');
const path = require('path');

// Path to the generated JS file
const jsFilePath = path.join(__dirname, '..', 'bin', 'Release', 'net10.0', 'browser-wasm', 'publish', 'DotNetWasmDemo.js');

console.log('Patching HEAP exports in:', jsFilePath);

if (!fs.existsSync(jsFilePath)) {
    console.error('Error: File not found:', jsFilePath);
    process.exit(1);
}

let content = fs.readFileSync(jsFilePath, 'utf8');

// Pattern to find the updateMemoryViews function
// The function creates local HEAP variables but doesn't expose them on Module
const oldPattern = /function updateMemoryViews\(\) \{[\s\S]*?var b = wasmMemory\.buffer;[\s\S]*?HEAP8 = new Int8Array\(b\);[\s\S]*?HEAP16 = new Int16Array\(b\);[\s\S]*?HEAPU8 = new Uint8Array\(b\);[\s\S]*?HEAPU16 = new Uint16Array\(b\);[\s\S]*?HEAP32 = new Int32Array\(b\);[\s\S]*?HEAPU32 = new Uint32Array\(b\);[\s\S]*?HEAPF32 = new Float32Array\(b\);[\s\S]*?HEAPF64 = new Float64Array\(b\);[\s\S]*?HEAP64 = new BigInt64Array\(b\);[\s\S]*?HEAPU64 = new BigUint64Array\(b\);[\s\S]*?\}/;

const newFunction = `function updateMemoryViews() {
  var b = wasmMemory.buffer;
  Module['HEAP8'] = HEAP8 = new Int8Array(b);
  Module['HEAP16'] = HEAP16 = new Int16Array(b);
  Module['HEAPU8'] = HEAPU8 = new Uint8Array(b);
  Module['HEAPU16'] = HEAPU16 = new Uint16Array(b);
  Module['HEAP32'] = HEAP32 = new Int32Array(b);
  Module['HEAPU32'] = HEAPU32 = new Uint32Array(b);
  Module['HEAPF32'] = HEAPF32 = new Float32Array(b);
  Module['HEAPF64'] = HEAPF64 = new Float64Array(b);
  Module['HEAP64'] = HEAP64 = new BigInt64Array(b);
  Module['HEAPU64'] = HEAPU64 = new BigUint64Array(b);
}`;

if (oldPattern.test(content)) {
    content = content.replace(oldPattern, newFunction);
    fs.writeFileSync(jsFilePath, content);
    console.log('Successfully patched updateMemoryViews() to expose HEAP arrays on Module');
} else {
    // Try a simpler approach - just find and replace the assignments
    console.log('Complex pattern not found, trying simple replacement...');

    let modified = false;
    const heapTypes = ['HEAP8', 'HEAP16', 'HEAPU8', 'HEAPU16', 'HEAP32', 'HEAPU32', 'HEAPF32', 'HEAPF64', 'HEAP64', 'HEAPU64'];

    for (const heapType of heapTypes) {
        // Match patterns like "HEAP32 = new Int32Array(b);" that aren't already prefixed with Module
        const simplePattern = new RegExp(`(?<!Module\\['${heapType}'\\] = )${heapType} = new `, 'g');
        if (simplePattern.test(content)) {
            content = content.replace(
                new RegExp(`(?<!Module\\['${heapType}'\\] = )(${heapType} = new )`, 'g'),
                `Module['${heapType}'] = $1`
            );
            modified = true;
        }
    }

    if (modified) {
        fs.writeFileSync(jsFilePath, content);
        console.log('Successfully patched HEAP exports using simple replacement');
    } else {
        console.warn('Warning: Could not find HEAP assignments to patch. The file may already be patched or have a different format.');

        // Check if Module.HEAP32 is already present
        if (content.includes("Module['HEAP32']") || content.includes('Module["HEAP32"]')) {
            console.log('Module.HEAP32 already exists in the file - no patch needed');
        } else {
            console.error('Error: Could not patch and Module.HEAP32 is not present');
            process.exit(1);
        }
    }
}

console.log('Done!');
