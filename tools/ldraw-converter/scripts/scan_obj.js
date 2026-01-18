const fs = require('fs');

const content = fs.readFileSync('output_test2_anglefix/temp/main.ldr_step_1.obj', 'utf-8');
let minX = Infinity, maxX = -Infinity;
let minY = Infinity, maxY = -Infinity;
let minZ = Infinity, maxZ = -Infinity;

const lines = content.split('\n');
for (const line of lines) {
    if (line.startsWith('v ')) {
        const parts = line.trim().split(/\s+/);
        const x = parseFloat(parts[1]);
        const y = parseFloat(parts[2]);
        const z = parseFloat(parts[3]);

        if (x < minX) minX = x;
        if (x > maxX) maxX = x;
        if (y < minY) minY = y;
        if (y > maxY) maxY = y;
        if (z < minZ) minZ = z;
        if (z > maxZ) maxZ = z;
    }
}

console.log(`X: [${minX}, ${maxX}] Size: ${maxX - minX}`);
console.log(`Y: [${minY}, ${maxY}] Size: ${maxY - minY}`);
console.log(`Z: [${minZ}, ${maxZ}] Size: ${maxZ - minZ}`);
