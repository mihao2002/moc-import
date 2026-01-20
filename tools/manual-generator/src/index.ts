
import * as fs from 'fs-extra';
import * as path from 'path';
import yargs = require('yargs');
import { exec } from 'child_process';
import puppeteer from 'puppeteer';
import { promisify } from 'util';

const execAsync = promisify(exec);

// Types matching ldraw-converter output
interface Vector3 { x: number; y: number; z: number; }
interface Part {
    partId: string;
    color: number;
    position: Vector3;
    rotation: any;
    isSubmodel: boolean;
    imageName: string;
}
interface Step {
    parts: Part[];
    imageName: string;
}
interface ModelData {
    modelName: string;
    steps: Step[];
}
interface LDrawOutput {
    name: string;
    data: ModelData;
}

// Helper to find model by name (normalized)
function findModel(models: LDrawOutput[], name: string): LDrawOutput | undefined {
    return models.find(m => m.name.toLowerCase() === name.toLowerCase());
}

// Helper to find absolute last step image for a model (proxy for "Finished Model" thumbnail)
function getModelThumbnail(models: LDrawOutput[], modelName: string): string | null {
    const m = findModel(models, modelName);
    if (!m || m.data.steps.length === 0) return null;
    // Return the image of the last step
    return m.data.steps[m.data.steps.length - 1]!.imageName;
}

// Recursive function to generate linear print order
// Returns array of "RenderOps": { model: ModelData, stepIndex: number, hierarchy: string[] }
interface PageOp {
    type: 'step';
    modelName: string;
    stepIndex: number;
    stepImage: string;
    parts: Part[];
    hierarchy: string[]; // e.g. ["Main", "Engine", "Piston"]
}

const visitedModels = new Set<string>();
const printQueue: PageOp[] = [];

function traverseModel(models: LDrawOutput[], modelName: string, hierarchy: string[]) {
    // Note: We track visited/printed models globally to avoid re-printing instructions for reused subassemblies.
    // However, the hierarchy passed down must always be the *current* traversal path for valid breadcrumbs.

    // Check if we already printed instructions for this model?
    // If we did, we don't need to dive into it again.
    if (visitedModels.has(modelName.toLowerCase())) return;

    const model = findModel(models, modelName);
    if (!model) return;

    visitedModels.add(modelName.toLowerCase());

    // Iterate Steps
    for (let i = 0; i < model.data.steps.length; i++) {
        const step = model.data.steps[i];
        if (!step) continue;

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
        .option('library', { type: 'string', description: 'Path to LDraw library' })
        .option('blender', { type: 'string', description: 'Path to Blender executable' })
        .option('width', { type: 'number', default: 2048 })
        .option('height', { type: 'number', default: 1536 })
        .option('save-blend', { type: 'boolean', description: 'Save Blender project files' })
        .parse();

    const inputPath = path.resolve(argv.input);
    const outputPdf = path.resolve(argv.output);
    const tempDir = path.resolve('temp_build');

    console.log(`Processing: ${inputPath}`);

    // Clean temp dir to ensure no stale geometry (crucial for accurate global centering alignment)
    await fs.emptyDir(tempDir);

    // 1. Run Converter
    // Resolve relative to this file (src/index.ts) -> ../../ldraw-converter
    const converterDir = path.resolve(__dirname, '../../ldraw-converter');

    // Construct Arguments
    let converterArgs = [
        `--input "${inputPath}"`,
        `--output "${tempDir}"`,
        `--width ${argv.width}`,
        `--height ${argv.height}`,
        `--quiet`
    ];

    if (argv.library) converterArgs.push(`--library "${argv.library}"`);
    else converterArgs.push(`--library "C:\\Users\\mihao\\LDraw"`); // Default Fallback

    if (argv.blender) converterArgs.push(`--blender "${argv.blender}"`);
    else converterArgs.push(`--blender "C:\\Program Files\\Blender Foundation\\Blender 5.0\\blender.exe"`); // Default Fallback

    if (argv['save-blend']) converterArgs.push('--save-blend');

    const converterCmd = `npx ts-node src/main.ts ${converterArgs.join(' ')}`;

    console.log(`Running LDraw Converter in ${converterDir}...`);
    console.log(`Command: ${converterCmd}`);
    try {
        await execAsync(converterCmd, { cwd: converterDir });
    } catch (e) {
        console.error("Converter failed:", e);
        process.exit(1);
    }

    // 2. Read Data
    const stepsJsonPath = path.join(tempDir, 'data', 'steps.json');
    const stepsData: LDrawOutput[] = await fs.readJson(stepsJsonPath);

    // 3. Build Queue
    const inputBase = path.basename(inputPath);
    let mainModel = stepsData.find(m => m.name.toLowerCase() === inputBase.toLowerCase());
    if (!mainModel && stepsData.length > 0) mainModel = stepsData[0];

    if (!mainModel) throw new Error("Could not determine main model");

    console.log(`Generating instructions used Main: ${mainModel.name}`);

    traverseModel(stepsData, mainModel.name, [mainModel.name]);

    console.log(`Generated ${printQueue.length} pages of instructions.`);

    // 4. Generate HTML
    const htmlContent = generateHtml(printQueue, tempDir, stepsData);
    const htmlPath = path.join(tempDir, 'instructions.html');
    await fs.writeFile(htmlPath, htmlContent);

    // 5. Render PDF
    console.log("Rendering PDF...");
    const browser = await puppeteer.launch();
    const page = await browser.newPage();
    await page.goto(`file:${htmlPath}`, { waitUntil: 'networkidle0' });
    await page.pdf({
        path: outputPdf,
        format: 'A2',
        landscape: true, // EXPLICITLY set landscape
        printBackground: true,
        margin: { top: '0', bottom: '0', left: '0', right: '0' } // Maximize space
    });
    await browser.close();

    console.log(`PDF saved to: ${outputPdf}`);
}

function generateHtml(ops: PageOp[], assetBase: string, models: LDrawOutput[]): string {
    const assetUri = (rel: string) => `file:${path.join(assetBase, rel).replace(/\\/g, '/')}`;

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
                grid-template-rows: min-content 1fr 60px; /* Header adjusts to content */
                box-sizing: border-box;
                padding: 40px;
                overflow: hidden; 
            }
            .header { 
                display: flex; 
                align-items: center; 
                justify-content: space-between; /* Space out Breadcrumb and Step Num */
                border-bottom: 2px solid #ddd; 
                padding-bottom: 20px; 
                margin-bottom: 20px;
            }
            
            /* Breadcrumbs */
            .breadcrumb { display: flex; align-items: center; gap: 20px; }
            .crumb-item { display: flex; flex-direction: column; align-items: center; opacity: 0.5; transition: opacity 0.3s; }
            .crumb-item img { width: 80px; height: 80px; object-fit: contain; border: 1px solid #eee; border-radius: 8px; padding: 5px; background: #f9f9f9; }
            .crumb-item div { font-size: 16px; margin-top: 5px; color: #555; max-width: 150px; text-align: center; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
            
            /* Active Submodel Indicator - 2x Size */
            .crumb-item.active { opacity: 1.0; font-weight: bold; }
            .crumb-item.active img { width: 160px; height: 160px; border: 2px solid #666; background: #fff; box-shadow: 0 4px 10px rgba(0,0,0,0.1); }
            .crumb-item.active div { font-size: 24px; margin-top: 10px; color: #333; max-width: 300px; }
            
            .arrow { font-size: 30px; color: #ccc; }
            
            /* Step Number in Header */
            .step-num-container { text-align: right; }
            .step-num { font-size: 120px; font-weight: 800; color: #222; line-height: 1; }
            .step-label { font-size: 24px; color: #666; text-transform: uppercase; letter-spacing: 2px; }

            /* Content Area */
            .content { display: flex; overflow: hidden; height: 100%; gap: 40px; }
            
            /* Parts List - Vertical Column that Wraps */
            .parts-list { 
                display: flex; 
                flex-direction: column; 
                flex-wrap: wrap; /* Wrap to next column if vertical space runs out */
                align-content: flex-start;
                height: 100%; 
                min-width: 200px;
                max-width: 50%; /* Don't take more than half the page */
                gap: 20px; 
                padding-right: 20px;
                border-right: 2px solid #eee;
            }
            
            .part-item { 
                border: 1px solid #eee; 
                border-radius: 8px; 
                padding: 10px; 
                text-align: center; 
                background: #fafafa; 
                position: relative; 
                display: flex; 
                flex-direction: column; 
                align-items: center; 
                page-break-inside: avoid;
                margin-right: 20px; /* Spacing between columns */
            }
            
            /* 1:1 Size Approximation (Large) */
            .part-item img { 
                width: auto; 
                height: 160px; /* ~1:1 for A2 */
                object-fit: contain; 
            }
            
            /* Submodel Parts - scaled down or kept distinct */
            .part-item.is-submodel img {
                height: 120px; /* Smaller for submodels */
            }
            
            .part-item .pid { font-size: 14px; margin-top: 5px; color: #888; font-family: monospace; }
            .badge { 
                position: absolute; 
                top: 5px; 
                right: 5px; 
                background: #e74c3c; 
                color: white; 
                font-weight: bold; 
                border-radius: 50%; 
                width: 36px; 
                height: 36px; 
                display: flex; 
                align-items: center; 
                justify-content: center; 
                font-size: 18px;
                box-shadow: 0 2px 4px rgba(0,0,0,0.2);
                border: 2px solid white;
            }
            
            .step-image { flex: 1; display: flex; justify-content: center; align-items: center; height: 100%; overflow: hidden; }
            .step-image img { 
                width: 100%; 
                height: 100%; 
                object-fit: contain; 
                filter: drop-shadow(0 10px 30px rgba(0,0,0,0.15)); 
            }
            
            .footer { 
                display: flex; 
                justify-content: flex-end; 
                align-items: center; 
                font-size: 24px; 
                color: #888;
                border-top: 1px solid #eee;
                padding-top: 10px;
            }
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

        const groupedParts = new Map<string, { part: Part, count: number }>();
        op.parts.forEach(p => {
            const key = `${p.partId}_${p.color}`;
            if (groupedParts.has(key)) {
                groupedParts.get(key)!.count++;
            } else {
                groupedParts.set(key, { part: p, count: 1 });
            }
        });

        const partsHtml = Array.from(groupedParts.values()).map(({ part: p, count }) => {
            const img = assetUri(`images/${p.imageName}`);
            const isSub = p.isSubmodel ? 'is-submodel' : '';
            return `
                    <div class="part-item ${isSub}">
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
                    <div class="step-num-container">
                        <div class="step-num">${op.stepIndex}</div>
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
                    <div style="font-weight: bold; color: #333;">${index + 1}</div>
                </div>
            </div>
            `;
    }).join('')}
    </body>
    </html>
    `;
}

main().catch(console.error);
