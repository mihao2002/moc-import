"use strict";
var __createBinding = (this && this.__createBinding) || (Object.create ? (function(o, m, k, k2) {
    if (k2 === undefined) k2 = k;
    var desc = Object.getOwnPropertyDescriptor(m, k);
    if (!desc || ("get" in desc ? !m.__esModule : desc.writable || desc.configurable)) {
      desc = { enumerable: true, get: function() { return m[k]; } };
    }
    Object.defineProperty(o, k2, desc);
}) : (function(o, m, k, k2) {
    if (k2 === undefined) k2 = k;
    o[k2] = m[k];
}));
var __setModuleDefault = (this && this.__setModuleDefault) || (Object.create ? (function(o, v) {
    Object.defineProperty(o, "default", { enumerable: true, value: v });
}) : function(o, v) {
    o["default"] = v;
});
var __importStar = (this && this.__importStar) || (function () {
    var ownKeys = function(o) {
        ownKeys = Object.getOwnPropertyNames || function (o) {
            var ar = [];
            for (var k in o) if (Object.prototype.hasOwnProperty.call(o, k)) ar[ar.length] = k;
            return ar;
        };
        return ownKeys(o);
    };
    return function (mod) {
        if (mod && mod.__esModule) return mod;
        var result = {};
        if (mod != null) for (var k = ownKeys(mod), i = 0; i < k.length; i++) if (k[i] !== "default") __createBinding(result, mod, k[i]);
        __setModuleDefault(result, mod);
        return result;
    };
})();
var __importDefault = (this && this.__importDefault) || function (mod) {
    return (mod && mod.__esModule) ? mod : { "default": mod };
};
Object.defineProperty(exports, "__esModule", { value: true });
const fs = __importStar(require("fs-extra"));
const path = __importStar(require("path"));
const yargs = require("yargs");
const child_process_1 = require("child_process");
const puppeteer_1 = __importDefault(require("puppeteer"));
const util_1 = require("util");
const execAsync = (0, util_1.promisify)(child_process_1.exec);
// Helper to find model by name (normalized)
function findModel(models, name) {
    return models.find(m => m.name.toLowerCase() === name.toLowerCase());
}
// Helper to find absolute last step image for a model (proxy for "Finished Model" thumbnail)
function getModelThumbnail(models, modelName) {
    const m = findModel(models, modelName);
    if (!m || m.data.steps.length === 0)
        return null;
    // Return the image of the last step
    return m.data.steps[m.data.steps.length - 1].imageName;
}
const visitedModels = new Set();
const printQueue = [];
function traverseModel(models, modelName, hierarchy) {
    // Note: We track visited/printed models globally to avoid re-printing instructions for reused subassemblies.
    // However, the hierarchy passed down must always be the *current* traversal path for valid breadcrumbs.
    // Check if we already printed instructions for this model?
    // If we did, we don't need to dive into it again.
    if (visitedModels.has(modelName.toLowerCase()))
        return;
    const model = findModel(models, modelName);
    if (!model)
        return;
    visitedModels.add(modelName.toLowerCase());
    // Iterate Steps
    for (let i = 0; i < model.data.steps.length; i++) {
        const step = model.data.steps[i];
        if (!step)
            continue;
        // check for submodels in this step
        if (step.parts) {
            for (const part of step.parts) {
                if (part.isSubmodel) {
                    const subName = part.partId;
                    // Recurse: Print submodel instructions first
                    traverseModel(models, subName, [...hierarchy, subName]);
                }
            }
        }
        // Now print THIS step
        printQueue.push({
            type: 'step',
            modelName: model.name,
            stepIndex: i + 1,
            stepImage: step.imageName,
            parts: step.parts || [],
            hierarchy: hierarchy // The hierarchy leading to THIS model
        });
    }
}
async function main() {
    const argv = await yargs(process.argv.slice(2))
        .option('input', { alias: 'i', type: 'string', demandOption: true })
        .option('output', { alias: 'o', type: 'string', default: 'instructions.pdf' })
        .parse();
    const inputPath = path.resolve(argv.input);
    const outputPdf = path.resolve(argv.output);
    const tempDir = path.resolve('temp_build');
    console.log(`Processing: ${inputPath}`);
    // Clean temp dir to ensure no stale geometry (crucial for accurate global centering alignment)
    await fs.emptyDir(tempDir);
    // 1. Run Converter
    const converterDir = path.resolve('../ldraw-converter');
    const converterCmd = `npx ts-node src/main.ts --input "${inputPath}" --output "${tempDir}" --width 2048 --height 1536 --library "C:\\Users\\mihao\\LDraw" --blender "C:\\Program Files\\Blender Foundation\\Blender 5.0\\blender.exe" --quiet`; // Removed --quiet to debug? No keep it.
    console.log(`Running LDraw Converter in ${converterDir}...`);
    try {
        await execAsync(converterCmd, { cwd: converterDir });
    }
    catch (e) {
        console.error("Converter failed:", e);
        process.exit(1);
    }
    // 2. Read Data
    const stepsJsonPath = path.join(tempDir, 'data', 'steps.json');
    const stepsData = await fs.readJson(stepsJsonPath);
    // 3. Build Queue
    const inputBase = path.basename(inputPath);
    let mainModel = stepsData.find(m => m.name.toLowerCase() === inputBase.toLowerCase());
    if (!mainModel && stepsData.length > 0)
        mainModel = stepsData[0];
    if (!mainModel)
        throw new Error("Could not determine main model");
    console.log(`Generating instructions used Main: ${mainModel.name}`);
    traverseModel(stepsData, mainModel.name, [mainModel.name]);
    console.log(`Generated ${printQueue.length} pages of instructions.`);
    // 4. Generate HTML
    const htmlContent = generateHtml(printQueue, tempDir, stepsData);
    const htmlPath = path.join(tempDir, 'instructions.html');
    await fs.writeFile(htmlPath, htmlContent);
    // 5. Render PDF
    console.log("Rendering PDF...");
    const browser = await puppeteer_1.default.launch();
    const page = await browser.newPage();
    await page.goto(`file:${htmlPath}`, { waitUntil: 'networkidle0' });
    await page.pdf({
        path: outputPdf,
        format: 'A2',
        printBackground: true,
        margin: { top: '20px', bottom: '20px' }
    });
    await browser.close();
    console.log(`PDF saved to: ${outputPdf}`);
}
function generateHtml(ops, assetBase, models) {
    const assetUri = (rel) => `file:${path.join(assetBase, rel).replace(/\\/g, '/')}`;
    return `
    <!DOCTYPE html>
    <html>
    <head>
        <style>
            @page { size: A2 landscape; margin: 0; }
            body { font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; margin: 0; padding: 0; background: #fff; }
            .page { 
                page-break-after: always; 
                height: 100vh; 
                width: 100vw;
                display: grid; 
                grid-template-rows: 140px 1fr 100px;
                box-sizing: border-box;
                padding: 40px;
            }
            .header { display: flex; align-items: center; border-bottom: 2px solid #ddd; padding-bottom: 10px; }
            .breadcrumb { display: flex; align-items: center; }
            .crumb-item { display: flex; flex-direction: column; align-items: center; opacity: 0.5; transition: opacity 0.3s; }
            .crumb-item.active { opacity: 1.0; font-weight: bold; }
            .crumb-item img { width: 80px; height: 80px; object-fit: contain; border: 1px solid #eee; border-radius: 8px; padding: 5px; background: #f9f9f9; }
            .crumb-item div { font-size: 16px; margin-top: 5px; color: #555; max-width: 150px; text-align: center; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
            .arrow { margin: 0 20px; font-size: 30px; color: #ccc; }
            
            .content { display: flex; margin-top: 20px; overflow: hidden; }
            .parts-list { 
                width: 350px; 
                border-right: 2px solid #eee; 
                padding-right: 30px; 
                display: flex; 
                flex-direction: column; 
                gap: 20px; 
                overflow-y: auto;
            }
            .part-item { border: 1px solid #eee; border-radius: 8px; padding: 15px; text-align: center; background: #fafafa; position: relative; }
            .part-item img { width: 120px; height: 120px; object-fit: contain; }
            .part-item .pid { font-size: 16px; margin-top: 10px; color: #888; font-family: monospace; }
            .badge { 
                position: absolute; 
                top: 5px; 
                right: 5px; 
                background: #e74c3c; 
                color: white; 
                font-weight: bold; 
                border-radius: 50%; 
                width: 30px; 
                height: 30px; 
                display: flex; 
                align-items: center; 
                justify-content: center; 
                font-size: 14px;
                box-shadow: 0 2px 4px rgba(0,0,0,0.2);
            }
            
            .step-image { flex: 1; display: flex; justify-content: center; align-items: center; padding: 20px; }
            .step-image img { max-width: 100%; max-height: 100%; object-fit: contain; filter: drop-shadow(0 10px 20px rgba(0,0,0,0.1)); }
            
            .footer { 
                display: flex; 
                justify-content: space-between; 
                align-items: center; 
                font-size: 32px; 
                color: #444;
                border-top: 2px solid #ddd;
                padding-top: 20px;
            }
            .step-num { font-size: 64px; font-weight: bold; color: #222; }
        </style>
    </head>
    <body>
        ${ops.map((op, index) => {
        const hierarchyHtml = op.hierarchy.map((modelName, i) => {
            const isLast = i === op.hierarchy.length - 1;
            // Try to find a good thumbnail
            // 1. Check if 'submodels/NAME.png' exists (Generated by converter for parts)
            // 2. Fallback: Use last step image of that model
            let thumbSrc = assetUri(`images/submodels/${modelName}.png`);
            // Heuristic: If it looks like the main model (first in hierarchy), use last step
            const mThumb = getModelThumbnail(models, modelName);
            if (mThumb) {
                // Prefer the last step image as it represents the "Finished" state of that stage
                // BUT, last step image is huge? CSS will resize it.
                // Main model doesn't have submodels/NAME.png usually.
                if (i === 0) {
                    thumbSrc = assetUri(`images/${mThumb}`);
                }
            }
            return `
                    <div class="crumb-item ${isLast ? 'active' : ''}">
                        <img src="${thumbSrc}" onerror="this.src='${assetUri(`images/${mThumb}`)}'"/>
                        <div>${modelName.replace('.ldr', '').replace('.mpd', '')}</div>
                    </div>
                    ${!isLast ? '<div class="arrow">&rarr;</div>' : ''}
                `;
        }).join('');
        const groupedParts = new Map();
        op.parts.forEach(p => {
            const key = `${p.partId}_${p.color}`;
            if (groupedParts.has(key)) {
                groupedParts.get(key).count++;
            }
            else {
                groupedParts.set(key, { part: p, count: 1 });
            }
        });
        const partsHtml = Array.from(groupedParts.values()).map(({ part: p, count }) => {
            const img = assetUri(`images/${p.imageName}`);
            return `
                    <div class="part-item">
                        <img src="${img}" />
                        <div class="pid">${p.partId.replace('.dat', '')}</div>
                        ${count > 1 ? `<div class="badge">x${count}</div>` : ''}
                    </div>
                `;
        }).join('');
        return `
            <div class="page">
                <div class="header">
                    <div class="breadcrumb">
                        ${hierarchyHtml}
                    </div>
                </div>
                <div class="content">
                    <div class="parts-list">
                        ${partsHtml}
                    </div>
                    <div class="step-image">
                        <img src="${assetUri(`images/${op.stepImage}`)}" />
                    </div>
                </div>
                <div class="footer">
                    <div class="step-num">${op.stepIndex}</div>
                    <div>Page ${index + 1}</div>
                </div>
            </div>
            `;
    }).join('')}
    </body>
    </html>
    `;
}
main().catch(console.error);
//# sourceMappingURL=index.js.map