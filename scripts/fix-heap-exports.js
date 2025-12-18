#!/usr/bin/env node
/**
 * Fix Module.HEAP32 exports for NativeAOT-LLVM
 *
 * NativeAOT-LLVM generates code that expects Module.HEAP32 but Emscripten
 * doesn't expose it by default with MODULARIZE. This patches the generated
 * JS to export HEAP arrays on the Module object.
 */

const fs = require('fs');
const path = require('path');

const jsFile = process.argv[2] || 'bin/Release/net10.0/browser-wasm/publish/DotNetWasmDemo.js';

if (!fs.existsSync(jsFile)) {
    console.error(`Error: ${jsFile} not found`);
    process.exit(1);
}

console.log(`Patching ${jsFile} to export HEAP arrays on Module...`);

let content = fs.readFileSync(jsFile, 'utf8');

// Find and replace the updateMemoryViews function
const oldFunction = `function updateMemoryViews() {
  var b = wasmMemory.buffer;
  HEAP8 = new Int8Array(b);
  HEAP16 = new Int16Array(b);
  HEAPU8 = new Uint8Array(b);
  HEAPU16 = new Uint16Array(b);
  HEAP32 = new Int32Array(b);
  HEAPU32 = new Uint32Array(b);
  HEAPF32 = new Float32Array(b);
  HEAPF64 = new Float64Array(b);
  HEAP64 = new BigInt64Array(b);
  HEAPU64 = new BigUint64Array(b);
}`;

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

if (content.includes(newFunction)) {
    console.log('File already patched. Skipping.');
    process.exit(0);
}

if (!content.includes(oldFunction)) {
    console.error('Error: Could not find updateMemoryViews function to patch.');
    console.error('The Emscripten output format may have changed.');
    process.exit(1);
}

content = content.replace(oldFunction, newFunction);

fs.writeFileSync(jsFile, content, 'utf8');
console.log('Done. HEAP arrays are now exported on Module object.');
