
import yargs from 'yargs';
import * as path from 'path';
import * as fs from 'fs-extra';
import { LDrawParser, RuntimeModelData, LDrawStep, LDrawPart } from './parser';
import { LDConfigParser } from './ldconfig';
import { LDViewService } from './ldview';
import { ObjExporter } from './obj-exporter';
import { BlenderService, CameraTransform } from './blender-service';
import { Logger } from './logger';

interface Args {
    input: string;
    output: string;
    library: string;
    ldview?: string;
    blender?: string;
    ldConfig?: string;
}

// Helper to find part file in library
function findPartFile(partId: string, libraryPaths: string[]): string | null {
    for (const libraryPath of libraryPaths) {
        // Normal parts
        let p = path.join(libraryPath, 'parts', partId);
        if (fs.existsSync(p)) return p;
        p = path.join(libraryPath, 'parts', partId + ".dat");
        if (fs.existsSync(p)) return p;

        // Subparts
        p = path.join(libraryPath, 'p', partId);
        if (fs.existsSync(p)) return p;
        if (fs.existsSync(p)) return p;
        p = path.join(libraryPath, 'p', partId + ".dat");
        if (fs.existsSync(p)) return p;

        // Unofficial Parts
        p = path.join(libraryPath, 'UnOfficial', 'parts', partId);
        if (fs.existsSync(p)) return p;
        p = path.join(libraryPath, 'UnOfficial', 'parts', partId + ".dat");
        if (fs.existsSync(p)) return p;

        p = path.join(libraryPath, 'UnOfficial', 'p', partId);
        if (fs.existsSync(p)) return p;
        p = path.join(libraryPath, 'UnOfficial', 'p', partId + ".dat");
        if (fs.existsSync(p)) return p;
    }
    return null;
}

// Normalize key for map lookups
function normalizeKey(id: string): string {
    return id.toLowerCase().replace(/[\\/]/g, '/');
}

async function main() {
    const startTime = Date.now();
    const argv = await yargs(process.argv.slice(2))
        .option('input', { alias: 'i', type: 'string', description: 'Input .ldr file', demandOption: true })
        .option('output', { alias: 'o', type: 'string', description: 'Output directory', demandOption: true })
        .option('ldview', { type: 'string', description: 'Path to LDView.exe', default: 'LDView' })
        .option('library', { type: 'string', description: 'LDraw Library Path', demandOption: true })
        .option('blender', { type: 'string', description: 'Path to Blender executable' })
        .option('ldConfig', { type: 'string', description: 'Path to LDConfig.ldr' })
        .option('width', { type: 'number', description: 'Output image width', default: 512 })
        .option('height', { type: 'number', description: 'Output image height', default: 512 })
        .option('quiet', { type: 'boolean', description: 'Suppress console output', default: false })
        .option('save-blend', { type: 'boolean', description: 'Save Blender project files' })
        .help()
        .argv;

    Logger.init(argv.quiet || false);

    const inputPath = path.resolve(argv.input);
    const outputDir = path.resolve(argv.output);
    const libraryPaths = argv.library.split(';').map(p => p.trim()).filter(p => p.length > 0);
    const mainLibraryPath = libraryPaths[0];
    const ldviewPath = argv.ldview;

    await fs.ensureDir(outputDir);
    await fs.ensureDir(path.join(outputDir, 'models'));
    await fs.ensureDir(path.join(outputDir, 'images', 'parts'));
    await fs.ensureDir(path.join(outputDir, 'images', 'steps'));
    await fs.ensureDir(path.join(outputDir, 'images', 'submodels'));
    await fs.ensureDir(path.join(outputDir, 'temp')); // Ensure temp dir exists
    await fs.ensureDir(path.join(outputDir, 'data'));

    Logger.log(`Parsing ${inputPath}...`);
    const parser = new LDrawParser();
    const models = await parser.parseModels(inputPath);

    // Recursive parsing for external references
    const modelQueue = [...models.values()];
    const processedFiles = new Set<string>();
    processedFiles.add(path.resolve(inputPath).toLowerCase());

    while (modelQueue.length > 0) {
        const currentModel = modelQueue.shift();
        if (!currentModel) continue;

        for (const step of currentModel.steps) {
            for (const part of step.parts) {
                if (!models.has(part.partId)) {
                    const possiblePaths = [
                        path.resolve(path.dirname(inputPath), part.partId),
                        path.resolve(path.dirname(inputPath), part.partId + '.ldr'),
                        path.resolve(path.dirname(inputPath), part.partId + '.mpd')
                    ];

                    for (const resolvedPath of possiblePaths) {
                        if (!processedFiles.has(resolvedPath.toLowerCase()) && fs.existsSync(resolvedPath)) {
                            if (fs.lstatSync(resolvedPath).isDirectory()) continue;

                            Logger.log(`Found external submodel: ${path.basename(resolvedPath)}`);
                            const subParser = new LDrawParser();
                            const newModels = await subParser.parseModels(resolvedPath);
                            processedFiles.add(resolvedPath.toLowerCase());

                            for (const [name, data] of newModels) {
                                const key = normalizeKey(name);
                                if (newModels.size === 1) {
                                    const partIdKey = normalizeKey(part.partId);
                                    if (!models.has(partIdKey)) {
                                        models.set(partIdKey, data);
                                        modelQueue.push(data);
                                    }
                                    if (!models.has(key)) {
                                        models.set(key, data);
                                    }
                                } else {
                                    if (!models.has(key)) {
                                        models.set(key, data);
                                        modelQueue.push(data);
                                    }
                                }
                            }
                            break;
                        }
                    }
                }
            }
        }
    }

    // Parse Colors & Generate MTL
    let colorPath = argv.ldConfig ? path.resolve(argv.ldConfig) : path.join(mainLibraryPath, 'LDConfig.ldr');

    let colorMap = new Map<number, any>();

    if (fs.existsSync(colorPath)) {
        Logger.log(`Parsing colors from ${colorPath}...`);
        const parsedColors = await LDConfigParser.parse(colorPath);
        colorMap = parsedColors;
        await fs.writeJson(path.join(outputDir, 'data', 'colors.json'), Array.from(parsedColors.entries()), { spaces: 2 });

        // Generate colors.mtl
        Logger.log("Generating colors.mtl...");
        const mtlLines: string[] = [];
        for (const [code, color] of parsedColors) {
            mtlLines.push(`newmtl code_${code}`);
            let hex = color.hex;

            if (hex && hex.startsWith('#')) {
                const r_srgb = parseInt(hex.substring(1, 3), 16) / 255.0;
                const g_srgb = parseInt(hex.substring(3, 5), 16) / 255.0;
                const b_srgb = parseInt(hex.substring(5, 7), 16) / 255.0;

                // Convert sRGB to Linear RGB
                const toLinear = (c: number) => {
                    return (c <= 0.04045) ? c / 12.92 : Math.pow((c + 0.055) / 1.055, 2.4);
                };

                const r = toLinear(r_srgb);
                const g = toLinear(g_srgb);
                const b = toLinear(b_srgb);

                mtlLines.push(`Kd ${r.toFixed(4)} ${g.toFixed(4)} ${b.toFixed(4)}`);
            } else {
                mtlLines.push(`Kd 0.5 0.5 0.5`); // Fallback
            }
            mtlLines.push(`d 1.0`);
            mtlLines.push(``);
        }
        await fs.writeFile(path.join(outputDir, 'models', 'colors.mtl'), mtlLines.join('\n'));
        await fs.writeFile(path.join(outputDir, 'temp', 'colors.mtl'), mtlLines.join('\n'));

    } else {
        Logger.log("LDConfig.ldr not found!");
    }

    const compiledLibraryPath = libraryPaths.join(';');
    // Initialize Services
    let renderer: LDViewService | BlenderService;

    if (argv.blender) {
        // Check if blender exists
        if (!fs.existsSync(argv.blender)) {
            Logger.error(`Blender executable not found at: ${argv.blender}`);
            process.exit(1);
        }
        renderer = new BlenderService(argv.blender);
        Logger.log("Using Blender Renderer");
    } else {
        const ldview = new LDViewService(ldviewPath, compiledLibraryPath);
        renderer = ldview;
        Logger.log("Using LDView Renderer");
    }

    // Use objExporter
    const objExporter = new ObjExporter(parser, libraryPaths);

    // Register models with normalized keys
    for (const [name, data] of models) {
        objExporter.registerModel(name, data);
        objExporter.registerModel(normalizeKey(name), data);
    }

    const processedParts = new Set<string>();
    const processedSubmodels = new Set<string>();

    const allModels = Array.from(models.entries());
    const stepsDataList: any[] = [];

    // Helper to calculate Blender Camera from Lat/Long
    // Camera Transform Helper (Matches LDrawCamera.cs)
    // --- Matrix Math Helper Classes ---
    class Matrix3 {
        // Column-major or Row-major? 
        // User's python: Rx = Matrix.Rotation(...)
        // Let's implement row-major for simplicity or match MathUtils.
        // Storing as array of 9: [m00, m01, m02, m10, m11, m12, m20, m21, m22]
        elements: number[];

        constructor(elements?: number[]) {
            this.elements = elements || [1, 0, 0, 0, 1, 0, 0, 0, 1];
        }

        static identity(): Matrix3 {
            return new Matrix3();
        }

        static rotationX(degrees: number): Matrix3 {
            const rad = degrees * Math.PI / 180;
            const c = Math.cos(rad);
            const s = Math.sin(rad);
            // [ 1  0  0 ]
            // [ 0  c -s ]
            // [ 0  s  c ]
            return new Matrix3([1, 0, 0, 0, c, -s, 0, s, c]);
        }

        static rotationY(degrees: number): Matrix3 {
            const rad = degrees * Math.PI / 180;
            const c = Math.cos(rad);
            const s = Math.sin(rad);
            // [ c  0  s ]
            // [ 0  1  0 ]
            // [-s  0  c ]
            return new Matrix3([c, 0, s, 0, 1, 0, -s, 0, c]);
        }

        static rotationZ(degrees: number): Matrix3 {
            const rad = degrees * Math.PI / 180;
            const c = Math.cos(rad);
            const s = Math.sin(rad);
            // [ c -s  0 ]
            // [ s  c  0 ]
            // [ 0  0  1 ]
            return new Matrix3([c, -s, 0, s, c, 0, 0, 0, 1]);
        }

        // Matrix Multiplication (A * B)
        multiply(b: Matrix3): Matrix3 {
            const a = this.elements;
            const be = b.elements;
            const r = new Array(9);

            for (let i = 0; i < 3; i++) {
                for (let j = 0; j < 3; j++) {
                    let sum = 0;
                    for (let k = 0; k < 3; k++) {
                        sum += a[i * 3 + k] * be[k * 3 + j];
                    }
                    r[i * 3 + j] = sum;
                }
            }
            return new Matrix3(r);
        }

        // Transpose (equivalent to Invert for Rotation Matrices)
        transpose(): Matrix3 {
            const e = this.elements;
            return new Matrix3([
                e[0], e[3], e[6],
                e[1], e[4], e[7],
                e[2], e[5], e[8]
            ]);
        }

        // Apply to Vector3
        applyToVector(v: { x: number, y: number, z: number }): { x: number, y: number, z: number } {
            const e = this.elements;
            return {
                x: e[0] * v.x + e[1] * v.y + e[2] * v.z,
                y: e[3] * v.x + e[4] * v.y + e[5] * v.z,
                z: e[6] * v.x + e[7] * v.y + e[8] * v.z
            };
        }
    }

    // Camera Transform Helper - User Orbit Algorithm
    const calculateBlenderCamera = (rotX: number, rotY: number, rotZ: number = 0, dist: number = 550, center: { x: number, y: number, z: number } = { x: 0, y: 0, z: 0 }): CameraTransform => {

        Logger.log(`[Debug] Inputs: rotX=${rotX} rotY=${rotY} rotZ=${rotZ}`);

        // 1. Define Camera Start in Blender Space
        // Direct mapping: LDraw View usually starts looking along -Z or similar?
        // But user wants 1:1. Let's assume standard camera at negative Y looking at origin?
        // Or Negative Z? 
        // LDraw standard View: Looking from +Z towards -Z? Or -Z towards +Z?
        // Let's stick to the previous start location but without the axis swap logic later?
        // Previous logic: startLoc = { x: 0, y: -dist, z: 0 }; (Back on Y axis)

        // 1. Define Camera Start in Blender Space
        // User requested: Pos(0, 0, -dist) looking at +Z, Up is -Y
        const startLoc = { x: 0, y: 0, z: -dist };
        const startUp = { x: 0, y: -1, z: 0 };

        // 2. Define Rotations
        // Direct mapping as requested (Right Hand -> Right Hand 1:1)
        // Negate to fix "wrong side of rotation" due to Blender axis alignment
        const rx = -rotX;
        const ry = -rotY;
        const rz = -rotZ;

        // 3. Apply Rotations
        // Previous logic order: Y -> Z -> X
        // We will keep this order of application but use the correct matrices for the axes.
        const RotY = Matrix3.rotationY(ry);
        const RotZ = Matrix3.rotationZ(rz);
        const RotX = Matrix3.rotationX(rx);

        // Composite Rotation: Order X -> Y -> Z (Applied: Rx first, then Ry, then Rz)
        // Matrix Mult: Rz * Ry * Rx
        const R_total = RotZ.multiply(RotY).multiply(RotX);

        // 4. Apply Orbit to Camera Position and Up Vector
        const camLocOrbit = R_total.applyToVector(startLoc);
        const camUpOrbit = R_total.applyToVector(startUp);

        // 5. Handle Center Offset - Direct Mapping
        // User requested: Interact with object centered at (0,0,0)
        // So Ignore the passed 'center' and force orbit around (0,0,0)
        const centerBlender = {
            x: 0,
            y: 0,
            z: 0
        };

        // Final Camera Location = centerBlender + camLocOrbit
        const finalLoc = {
            x: centerBlender.x + camLocOrbit.x,
            y: centerBlender.y + camLocOrbit.y,
            z: centerBlender.z + camLocOrbit.z
        };

        Logger.log(`[Debug] StartLoc: ${JSON.stringify(startLoc)}`);
        Logger.log(`[Debug] StartUp: ${JSON.stringify(startUp)}`);
        Logger.log(`[Debug] CamUpOrbit: ${JSON.stringify(camUpOrbit)}`);
        Logger.log(`[Debug] CamLocOrbit: ${JSON.stringify(camLocOrbit)}`);

        return {
            loc: finalLoc,
            lookAt: centerBlender,
            up: camUpOrbit
        };
    };

    // --- PHASE 1: Write all temp files (Baking) ---
    Logger.log("--- Phase 1: Baking Submodels to Temp Files ---");
    for (const [modelName, modelData] of allModels) {
        const safeName = modelName.replace(/[\\/]/g, '_');
        const submodelLines: string[] = [];

        // Parts (Type 1)
        for (const step of modelData.steps) {
            for (const part of step.parts) {
                const m = part.rotation;
                const pos = part.position;

                const partKey = normalizeKey(part.partId);
                let targetLdrName = part.partId;

                // Helper to strip extension
                const stripExt = (s: string) => s.replace(/\.(ldr|mpd|dat)$/i, '');
                const keyNoExt = stripExt(partKey);

                let resolvedKey: string | null = null;
                if (models.has(partKey)) resolvedKey = partKey;
                else if (models.has(partKey + '.ldr')) resolvedKey = partKey + '.ldr';
                else if (models.has(partKey + '.mpd')) resolvedKey = partKey + '.mpd';
                else if (models.has(keyNoExt)) resolvedKey = keyNoExt;

                if (resolvedKey) {
                    const safePartName = resolvedKey.replace(/[\\/]/g, '_');
                    targetLdrName = `sub_${safePartName}.ldr`;
                }

                const rawX = pos.x;
                const rawY = pos.y;
                const rawZ = pos.z;

                const m00 = m.c0.x; const m01 = m.c1.x; const m02 = m.c2.x;
                const m10 = m.c0.y; const m11 = m.c1.y; const m12 = m.c2.y;
                const m20 = m.c0.z; const m21 = m.c1.z; const m22 = m.c2.z;

                const line = `1 ${part.color} ${rawX} ${rawY} ${rawZ} ${m00} ${m01} ${m02} ${m10} ${m11} ${m12} ${m20} ${m21} ${m22} ${targetLdrName}`;
                submodelLines.push(line);
            }
        }

        // Geometry (Type 3 & 4)
        // Pure 1:1 - Write raw values. Parser preserves order and sign.
        if (modelData.triangles) {
            for (const tri of modelData.triangles) {
                // Write: v1, v2, v3
                const l = `3 ${tri.color} ${tri.v1.x} ${tri.v1.y} ${tri.v1.z} ${tri.v2.x} ${tri.v2.y} ${tri.v2.z} ${tri.v3.x} ${tri.v3.y} ${tri.v3.z}`;
                submodelLines.push(l);
            }
        }
        if (modelData.quads) {
            for (const quad of modelData.quads) {
                // Write: v1, v2, v3, v4
                const l = `4 ${quad.color} ${quad.v1.x} ${quad.v1.y} ${quad.v1.z} ${quad.v2.x} ${quad.v2.y} ${quad.v2.z} ${quad.v3.x} ${quad.v3.y} ${quad.v3.z} ${quad.v4.x} ${quad.v4.y} ${quad.v4.z}`;
                submodelLines.push(l);
            }
        }

        const tempSubPath = path.join(outputDir, 'temp', `sub_${safeName}.ldr`);
        await fs.writeFile(tempSubPath, submodelLines.join('\n'));
    }

    // --- PHASE 2: Process & Export ---
    Logger.log("--- Phase 2: Exporting Assets ---");

    let p2Count = 0;
    const p2Total = allModels.length;
    let partsExported = 0; // Tracking detailed progress

    for (const [modelName, modelData] of allModels) {
        p2Count++;
        Logger.progress(p2Count, p2Total, "Exporting Assets");

        stepsDataList.push({ name: modelName, data: modelData });

        // Always export the model itself to OBJ (if it's not the main model, or even if it is)
        // This ensures every submodel gets an OBJ in 'models/' regardless of traversal order
        const safeName = modelName.replace(/[\\/]/g, '_');
        // valid submodel check: if it has steps or geometry
        if (modelName !== path.basename(inputPath) && (modelData.steps.length > 0 || modelData.triangles.length > 0 || modelData.quads.length > 0)) {
            const outModelPath = path.join(outputDir, 'models', `${safeName}.obj`);
            if (!fs.existsSync(outModelPath)) {
                await objExporter.exportModel(modelData, outModelPath);
            }
        }

        for (const step of modelData.steps) {
            for (const part of step.parts) {
                const partKey = normalizeKey(part.partId);

                // Smart Lookup for Submodels
                let isSubmodel = false;
                let submodelData: RuntimeModelData | undefined;
                let resolvedKey: string | null = null;

                // Helper to strip extension
                const stripExt = (s: string) => s.replace(/\.(ldr|mpd|dat)$/i, '');
                const keyNoExt = stripExt(partKey);

                if (models.has(partKey)) {
                    isSubmodel = true;
                    submodelData = models.get(partKey);
                    resolvedKey = partKey;
                } else if (models.has(partKey + '.ldr')) {
                    isSubmodel = true;
                    submodelData = models.get(partKey + '.ldr');
                    resolvedKey = partKey + '.ldr';
                } else if (models.has(partKey + '.mpd')) {
                    isSubmodel = true;
                    submodelData = models.get(partKey + '.mpd');
                    resolvedKey = partKey + '.mpd';
                } else if (models.has(keyNoExt)) {
                    // Check WITHOUT extension (e.g. ref "sub.ldr" -> model "sub")
                    isSubmodel = true;
                    submodelData = models.get(keyNoExt);
                    resolvedKey = keyNoExt;
                }

                // Persist isSubmodel flag to part object for JSON output
                (part as any).isSubmodel = isSubmodel;

                if (isSubmodel && submodelData) {
                    part.isSubmodel = true;

                    const safeName = resolvedKey!.replace(/[\\/]/g, '_');
                    const tempSubPath = path.join(outputDir, 'temp', `sub_${safeName}.ldr`);

                    // CRITICAL FIX: We MUST write the submodel LDR to disk so objExporter can find it!
                    if (!processedSubmodels.has(resolvedKey!)) {
                        processedSubmodels.add(resolvedKey!);

                        processedSubmodels.add(resolvedKey!);

                        // Generate OBJ for Blender
                        const outModelPath = path.join(outputDir, 'models', `${safeName}.obj`);

                        Logger.progress(p2Count, p2Total, "Exporting Assets", ` (Part: ${partsExported} - ${safeName})`); // Log BEFORE export

                        if (!fs.existsSync(outModelPath)) {
                            await objExporter.exportModel(submodelData, outModelPath);
                        }

                        const outImgName = `${safeName}.png`;
                        const outImgPath = path.join(outputDir, 'images', 'submodels', outImgName);
                        (part as any).imageName = `submodels/${outImgName}`;

                        Logger.log(`Generating submodel thumbnail: ${safeName}`);
                        try {
                            if (renderer instanceof BlenderService) {
                                await renderer.queueJob(outModelPath, outImgPath, argv.width, argv.height, calculateBlenderCamera(45, 30, 0), undefined, (argv['save-blend'] as boolean));
                                partsExported++;
                                // Logger.progress(p2Count, p2Total, "Exporting Assets", ` (Part: ${partsExported})`); // Moved up
                            } else {
                                // LDView
                                if (fs.existsSync(outImgPath)) await fs.unlink(outImgPath);
                                // Standard submodel view: 45, 30
                                await renderer.exportSnapshot(tempSubPath, outImgPath, argv.width, argv.height, undefined, { x: 45, y: 30, z: 0 });
                            }
                        } catch (e) {
                            Logger.error(`Failed to render submodel ${safeName}:`, e);
                        }
                    } else {
                        const outImgName = `${safeName}.png`;
                        (part as any).imageName = `submodels/${outImgName}`;
                    }
                } else {
                    let partFile = findPartFile(part.partId, libraryPaths);

                    // Fallback: Check if it's an internal MPD model (e.g. flexible part) logic missed
                    let isInternalMpd = false;
                    if (!partFile && models.has(part.partId.toLowerCase())) {
                        partFile = "INTERNAL_MPD_" + part.partId; // Dummy path to satisfy truthiness
                        isInternalMpd = true;
                    }

                    if (partFile) {
                        const isPrimitive = !isInternalMpd && (partFile.includes(path.sep + 'p' + path.sep) ||
                            partFile.includes(path.sep + 'parts' + path.sep + 's' + path.sep) ||
                            part.partId.startsWith('p/') ||
                            part.partId.startsWith('4-4'));

                        if (!isPrimitive) {
                            const safeId = part.partId.replace(/[\\/]/g, '_');
                            let outModelPath = path.join(outputDir, 'models', `${safeId}.obj`);

                            if (!processedParts.has(partKey)) {
                                processedParts.add(partKey);
                                outModelPath = path.join(outputDir, 'models', `${safeId}.obj`);
                                // We still export OBJ for 3D viewer, but not for image generation
                                if (!fs.existsSync(outModelPath)) {
                                    Logger.progress(p2Count, p2Total, "Exporting Assets", ` (Part: ${partsExported} - ${safeId})`); // Log BEFORE export
                                    await objExporter.exportPart(part.partId, outModelPath);
                                }
                            }

                            const outImgName = `${safeId}_${part.color}.png`;
                            const outImgPath = path.join(outputDir, 'images', 'parts', outImgName);
                            (part as any).imageName = `parts/${outImgName}`;

                            if (!fs.existsSync(outImgPath)) {
                                try {
                                    if (renderer instanceof BlenderService) {
                                        // Calculate RGB ... (omitted for brevity, keep existing logic)
                                        let colorArg = undefined;
                                        if (colorMap.has(part.color)) {
                                            const c = colorMap.get(part.color)!;
                                            if (c.hex && c.hex.startsWith('#')) {
                                                const r = parseInt(c.hex.substring(1, 3), 16) / 255.0;
                                                const g = parseInt(c.hex.substring(3, 5), 16) / 255.0;
                                                const b = parseInt(c.hex.substring(5, 7), 16) / 255.0;
                                                colorArg = { r, g, b };
                                            }
                                        }
                                        // Queue Job instead of direct render
                                        Logger.progress(p2Count, p2Total, "Exporting Assets", ` (Part: ${partsExported} - ${safeId})`); // Log BEFORE queue
                                        await renderer.queueJob(outModelPath, outImgPath, argv.width, argv.height, calculateBlenderCamera(45, 30, 0), colorArg, (argv['save-blend'] as boolean));
                                        partsExported++;
                                        // Logger.progress(p2Count, p2Total, "Exporting Assets", ` (Part: ${partsExported})`); // Moved up
                                    } else {
                                        // LDView
                                        const tempImgLdr = path.join(outputDir, 'temp', `img_${safeId}_${part.color}.ldr`);
                                        const ldrContent = `1 ${part.color} 0 0 0 1 0 0 0 1 0 0 0 1 ${part.partId}`;
                                        await fs.writeFile(tempImgLdr, ldrContent);
                                        await renderer.exportSnapshot(tempImgLdr, outImgPath, argv.width, argv.height, undefined, { x: 45, y: 30, z: 0 });
                                    }
                                } catch (e) {
                                    Logger.error(`Failed to render part ${safeId}:`, e);
                                }
                            }
                        }
                    } else {
                        if (!processedParts.has(partKey)) {
                            processedParts.add(partKey);
                            Logger.log(`Part file not found: ${part.partId}`);
                        }
                    }
                }
            }
        }
    }

    // --- PHASE 3: Steps Images ---
    Logger.log("--- Phase 3: Generating Step Images ---");

    // We process ALL models (main + submodels)

    // We process ALL models (main + submodels)

    let isFirstModel = true;
    let p3Count = 0;
    const p3Total = stepsDataList.length;

    for (const stepModel of stepsDataList) {
        p3Count++;
        const modelName = stepModel.name;
        const modelData = stepModel.data;

        Logger.log(`Processing Model ${p3Count}/${p3Total}: ${modelName} (${modelData.steps.length} steps)`);

        // Maintain persistent rotation
        const currentRotation = { x: 30, y: 45, z: 0 }; // Default LDraw View (Matches LDrawCamera.DefaultRotation)

        // Check if the FIRST step has an initial rotation command
        if (modelData.steps.length > 0 && modelData.steps[0].cameraParams) {
            currentRotation.x = modelData.steps[0].cameraParams.angles.x;
            currentRotation.y = modelData.steps[0].cameraParams.angles.y;
            currentRotation.z = modelData.steps[0].cameraParams.angles.z;
        }

        // Use all models that have steps
        if (modelData.steps.length === 0) continue;

        let subDir = "main";
        const inputBase = path.basename(inputPath);

        // Check if this model is the main input model OR if it matches input filename
        if (isFirstModel || normalizeKey(modelName) === normalizeKey(inputBase)) {
            subDir = "steps";
            isFirstModel = false;
        } else {
            subDir = "submodels/" + modelName.replace(/[\\/]/g, '_');
        }

        const stepImgDir = path.join(outputDir, 'images', subDir);
        await fs.ensureDir(stepImgDir);

        let stepCount = 0;
        const cumulativeLines: string[] = [];
        let minX = Infinity, minY = Infinity, minZ = Infinity;
        let maxX = -Infinity, maxY = -Infinity, maxZ = -Infinity;
        const stepMetadata: any = {};

        // Inject Raw Geometry (Triangles/Quads) from the base model
        // This is critical for models that are primarily geometry (primitives) and lack sub-parts
        if (modelData.triangles.length > 0 || modelData.quads.length > 0) {
            for (const tri of modelData.triangles) {
                cumulativeLines.push(`3 ${tri.color} ${tri.v1.x} ${tri.v1.y} ${tri.v1.z} ${tri.v2.x} ${tri.v2.y} ${tri.v2.z} ${tri.v3.x} ${tri.v3.y} ${tri.v3.z}`);
                // Update bounds for geometry
                [tri.v1, tri.v2, tri.v3].forEach(v => {
                    if (v.x < minX) minX = v.x; if (v.x > maxX) maxX = v.x;
                    if (v.y < minY) minY = v.y; if (v.y > maxY) maxY = v.y;
                    if (v.z < minZ) minZ = v.z; if (v.z > maxZ) maxZ = v.z;
                });
            }
            for (const quad of modelData.quads) {
                cumulativeLines.push(`4 ${quad.color} ${quad.v1.x} ${quad.v1.y} ${quad.v1.z} ${quad.v2.x} ${quad.v2.y} ${quad.v2.z} ${quad.v3.x} ${quad.v3.y} ${quad.v3.z} ${quad.v4.x} ${quad.v4.y} ${quad.v4.z}`);
                // Update bounds for geometry
                [quad.v1, quad.v2, quad.v3, quad.v4].forEach(v => {
                    if (v.x < minX) minX = v.x; if (v.x > maxX) maxX = v.x;
                    if (v.y < minY) minY = v.y; if (v.y > maxY) maxY = v.y;
                    if (v.z < minZ) minZ = v.z; if (v.z > maxZ) maxZ = v.z;
                });
            }
        }

        let currentStep = 0;
        const totalSteps = modelData.steps.length;

        // Pre-calculate Global Bounds for stable incremental centering
        let globalMinX = Infinity, globalMinY = Infinity, globalMinZ = Infinity;
        let globalMaxX = -Infinity, globalMaxY = -Infinity, globalMaxZ = -Infinity;

        // Accumulated Bounds for Step Metadata (Incremental)
        let currentBoundsMinX = Infinity, currentBoundsMinY = Infinity, currentBoundsMinZ = Infinity;
        let currentBoundsMaxX = -Infinity, currentBoundsMaxY = -Infinity, currentBoundsMaxZ = -Infinity;

        // Include Base Geometry
        if (modelData.triangles.length > 0 || modelData.quads.length > 0) {
            for (const tri of modelData.triangles) {
                [tri.v1, tri.v2, tri.v3].forEach(v => {
                    if (v.x < globalMinX) globalMinX = v.x; if (v.x > globalMaxX) globalMaxX = v.x;
                    if (v.y < globalMinY) globalMinY = v.y; if (v.y > globalMaxY) globalMaxY = v.y;
                    if (v.z < globalMinZ) globalMinZ = v.z; if (v.z > globalMaxZ) globalMaxZ = v.z;
                });
            }
            for (const quad of modelData.quads) {
                [quad.v1, quad.v2, quad.v3, quad.v4].forEach(v => {
                    if (v.x < globalMinX) globalMinX = v.x; if (v.x > globalMaxX) globalMaxX = v.x;
                    if (v.y < globalMinY) globalMinY = v.y; if (v.y > globalMaxY) globalMaxY = v.y;
                    if (v.z < globalMinZ) globalMinZ = v.z; if (v.z > globalMaxZ) globalMaxZ = v.z;
                });
            }
        }
        // Include All Steps
        for (const step of modelData.steps) {
            for (const part of step.parts) {
                const pos = part.position;
                if (pos.x < globalMinX) globalMinX = pos.x; if (pos.x > globalMaxX) globalMaxX = pos.x;
                if (pos.y < globalMinY) globalMinY = pos.y; if (pos.y > globalMaxY) globalMaxY = pos.y;
                if (pos.z < globalMinZ) globalMinZ = pos.z; if (pos.z > globalMaxZ) globalMaxZ = pos.z;
            }
        }

        const globalCenterX = (globalMinX === Infinity) ? 0 : (globalMinX + globalMaxX) / 2;
        const globalCenterY = (globalMinY === Infinity) ? 0 : (globalMinY + globalMaxY) / 2;
        const globalCenterZ = (globalMinZ === Infinity) ? 0 : (globalMinZ + globalMaxZ) / 2;

        let cumulativeVertexCount = 0;

        for (const step of modelData.steps) {
            currentStep++;
            Logger.progress(currentStep, totalSteps, `Rendering Steps`);
            stepCount++;

            // Camera Logic
            if (step.cameraParams) {
                currentRotation.x = step.cameraParams.angles.x;
                // Swap Y and Z for ROTSTEP inputs to align with User Expectation (Z=Vertical Orbit)
                // while keeping calculateBlenderCamera compatible with Standard View (Y=Vertical Orbit/Yaw)
                currentRotation.y = step.cameraParams.angles.y;
                currentRotation.z = step.cameraParams.angles.z;
            }

            for (const part of step.parts) {
                const m = part.rotation;
                const pos = part.position;

                // Update Bounds
                if (pos.x < minX) minX = pos.x;
                if (pos.x > maxX) maxX = pos.x;
                if (pos.y < minY) minY = pos.y;
                if (pos.y > maxY) maxY = pos.y;
                if (pos.z < minZ) minZ = pos.z;
                if (pos.z > maxZ) maxZ = pos.z;

                // Logic to reference submodels correctly in the temp LDR
                const partKey = normalizeKey(part.partId);
                let targetLdrName = part.partId;

                let resolvedKey: string | null = null;
                if (models.has(partKey)) resolvedKey = partKey;
                else if (models.has(partKey + '.ldr')) resolvedKey = partKey + '.ldr';
                else if (models.has(partKey + '.mpd')) resolvedKey = partKey + '.mpd';

                if (resolvedKey) {
                    const safePartName = resolvedKey.replace(/[\\/]/g, '_');
                    targetLdrName = `sub_${safePartName}.ldr`;
                }

                // Pure 1:1 Identity Mapping (No Transforms)
                // LDraw (x,y,z) is written directly to temporary file as (x,y,z)
                // Blender Import Settings handle the coordinate system interpretation (Forward=-Y, Up=Z)

                const m00 = m.c0.x; const m01 = m.c1.x; const m02 = m.c2.x;
                const m10 = m.c0.y; const m11 = m.c1.y; const m12 = m.c2.y;
                const m20 = m.c0.z; const m21 = m.c1.z; const m22 = m.c2.z;

                const line = `1 ${part.color} ${pos.x} ${pos.y} ${pos.z} ${m00} ${m01} ${m02} ${m10} ${m11} ${m12} ${m20} ${m21} ${m22} ${targetLdrName}`;
                cumulativeLines.push(line);
            }

            const stepName = `step_${stepCount}`;
            const stepNameSafe = `${modelName.replace(/[\\/]/g, '_')}_${stepName}`;
            const stepLdrPath = path.join(outputDir, 'temp', `${stepNameSafe}.ldr`);
            await fs.writeFile(stepLdrPath, cumulativeLines.join('\n'));

            const stepImgPath = path.join(stepImgDir, `${stepName}.png`);
            if (fs.existsSync(stepImgPath)) await fs.unlink(stepImgPath);

            if (renderer instanceof BlenderService) {
                // Must convert to OBJ first for Blender
                const stepObjPath = path.join(outputDir, 'temp', `${stepNameSafe}.obj`);
                const previousObjPath = path.join(outputDir, 'temp', `${modelName.replace(/[\\/]/g, '_')}_step_${stepCount - 1}.obj`);

                const deltaModelData: RuntimeModelData = {
                    modelName: `${modelName}_step_${stepCount}_delta`,
                    steps: [{
                        parts: step.parts, // Only parts for THIS step
                        cameraParams: null
                    }],
                    // Base geometry only goes into step 1
                    triangles: (stepCount === 1) ? modelData.triangles : [],
                    quads: (stepCount === 1) ? modelData.quads : [],
                    dependencies: new Set()
                };

                let deltaObjPath = path.join(outputDir, 'temp', `delta_${stepNameSafe}.obj`);

                // Create FRESH ObjExporter per step to ensure clean state and prevent submodel transformation leakage
                const stepExporter = new ObjExporter(parser, libraryPaths);
                // Re-register known models to the fresh exporter
                for (const [name, data] of models) {
                    stepExporter.registerModel(name, data);
                    stepExporter.registerModel(normalizeKey(name), data);
                }

                // 1. Export Deltas Standalone (for Highlighting)
                // Start Vertex 0 because it's a fresh file for Blender
                await stepExporter.exportModel(
                    deltaModelData,
                    deltaObjPath,
                    { x: globalCenterX, y: globalCenterY, z: globalCenterZ },
                    false, // No append
                    0
                );

                // 2. Export/Append to Accumulation Chain (for next steps)
                let appendMode = false;
                if (stepCount > 1) {
                    if (fs.existsSync(previousObjPath)) {
                        await fs.copy(previousObjPath, stepObjPath);
                        appendMode = true;
                    }
                }

                // Centering Geometry: Pass GLOBAL center as offset
                Logger.progress(currentStep, totalSteps, `Rendering Steps`, " (Generating Geometry Delta...)");
                try {
                    const exportResult = await stepExporter.exportModel(
                        deltaModelData,
                        stepObjPath,
                        { x: globalCenterX, y: globalCenterY, z: globalCenterZ },
                        appendMode,
                        cumulativeVertexCount
                    );
                    cumulativeVertexCount += exportResult.count;

                    // Update Accumulated Bounds with THIS step's geometry
                    // Note: The bounds returned by exportModel are already centered (OBJ coordinates)
                    // We want to track the total OBJ bounds.

                    // Handle first step initialization if bounds are pristine
                    if (currentBoundsMinX === Infinity && exportResult.bounds.min.x !== Infinity) {
                        currentBoundsMinX = exportResult.bounds.min.x;
                        currentBoundsMaxX = exportResult.bounds.max.x;
                        currentBoundsMinY = exportResult.bounds.min.y;
                        currentBoundsMaxY = exportResult.bounds.max.y;
                        currentBoundsMinZ = exportResult.bounds.min.z;
                        currentBoundsMaxZ = exportResult.bounds.max.z;
                    } else if (exportResult.bounds.min.x !== Infinity) {
                        if (exportResult.bounds.min.x < currentBoundsMinX) currentBoundsMinX = exportResult.bounds.min.x;
                        if (exportResult.bounds.max.x > currentBoundsMaxX) currentBoundsMaxX = exportResult.bounds.max.x;
                        if (exportResult.bounds.min.y < currentBoundsMinY) currentBoundsMinY = exportResult.bounds.min.y;
                        if (exportResult.bounds.max.y > currentBoundsMaxY) currentBoundsMaxY = exportResult.bounds.max.y;
                        if (exportResult.bounds.min.z < currentBoundsMinZ) currentBoundsMinZ = exportResult.bounds.min.z;
                        if (exportResult.bounds.max.z > currentBoundsMaxZ) currentBoundsMaxZ = exportResult.bounds.max.z;
                    }

                    // Determine bounds for camera (using Global Bounds allows static camera)
                    const sizeX = globalMaxX - globalMinX;
                    const sizeY = globalMaxY - globalMinY;
                    const sizeZ = globalMaxZ - globalMinZ;

                    // Blender Size Mapping: X->X, Y->Z, Z->Y
                    const blenderSizeX = sizeX;
                    const blenderSizeY = sizeZ; // LDraw Z -> Blender Y
                    const blenderSizeZ = sizeY; // LDraw Y -> Blender Z

                    Logger.log(`[Debug] Dimensions (LDraw): W(X)=${sizeX.toFixed(2)}, H(Y)=${sizeY.toFixed(2)}, D(Z)=${sizeZ.toFixed(2)}`);
                    Logger.log(`[Debug] Dimensions (Blender): W(X)=${blenderSizeX.toFixed(2)}, D(Y)=${blenderSizeY.toFixed(2)}, H(Z)=${blenderSizeZ.toFixed(2)}`);

                    // Use Accumulated Bounds for this Step Metadata
                    const bMinX = currentBoundsMinX;
                    const bMaxX = currentBoundsMaxX;
                    const bMinY = currentBoundsMinY;
                    const bMaxY = currentBoundsMaxY;
                    const bMinZ = currentBoundsMinZ;
                    const bMaxZ = currentBoundsMaxZ;

                    const bSizeX = bMaxX - bMinX;
                    const bSizeY = bMaxY - bMinY;
                    const bSizeZ = bMaxZ - bMinZ;

                    Logger.log(`[Debug] OBJ Geometry Bounds (Blender Space):`);
                    Logger.log(`[Debug] X: [${bMinX.toFixed(2)}, ${bMaxX.toFixed(2)}] Size: ${bSizeX.toFixed(2)}`);
                    Logger.log(`[Debug] Y: [${bMinY.toFixed(2)}, ${bMaxY.toFixed(2)}] Size: ${bSizeY.toFixed(2)}`);
                    Logger.log(`[Debug] Z: [${bMinZ.toFixed(2)}, ${bMaxZ.toFixed(2)}] Size: ${bSizeZ.toFixed(2)}`);

                    // Store metadata for App usage (using ACTUAL geometry bounds)
                    // bMinX is the OBJ coordinate (centered). Add centerX to get World coordinate.
                    (step as any).center = { x: globalCenterX, y: globalCenterY, z: globalCenterZ };
                    (step as any).bounds = {
                        min: { x: bMinX + globalCenterX, y: bMinY + globalCenterY, z: bMinZ + globalCenterZ },
                        max: { x: bMaxX + globalCenterX, y: bMaxY + globalCenterY, z: bMaxZ + globalCenterZ }
                    };

                    // We only support Blender for step images currently (due to advanced camera logic)
                    if (renderer instanceof BlenderService) {
                        // Input = Previous Accumulation (exists?) -> Ghosted
                        // Delta = Current Step (deltaObjPath) -> Opaque
                        const renderInput = fs.existsSync(previousObjPath) ? previousObjPath : ""; // Empty if first step

                        await renderer.queueJob(renderInput, stepImgPath, argv.width, argv.height, calculateBlenderCamera(
                            currentRotation.x,
                            currentRotation.y,
                            currentRotation.z,
                            argv.height // zoom
                        ), undefined, (argv['save-blend'] as boolean), deltaObjPath); // Pass delta!

                        Logger.progress(currentStep, totalSteps, `Rendering Steps`, " (Queued)");
                    }
                } catch (e) {
                    Logger.error(`Failed to render step ${stepNameSafe}:`, e);
                }
            } else {
                console.log(`Generating step image: ${modelName} / ${stepName}`);
                try {
                    await renderer.exportSnapshot(stepLdrPath, stepImgPath, 1024, 768, undefined, { ...currentRotation });
                } catch (e) {
                    console.error(`Failed to render step ${stepNameSafe}:`, e);
                }
            }

            // Update JSON
            (step as any).imageName = `${subDir}/${stepName}.png`;
        }
    }

    console.log(`Final Array Length: ${stepsDataList.length}`);
    await fs.writeJson(path.join(outputDir, 'data', 'steps.json'), stepsDataList, { spaces: 2 });

    // Create simplified metadata for easy app consumption
    const simpleMetadata: any = {};
    for (const model of stepsDataList) {
        simpleMetadata[model.name] = {
            steps: model.data.steps.map((s: any) => ({
                stepNumber: model.data.steps.indexOf(s) + 1,
                center: s.center || { x: 0, y: 0, z: 0 },
                bounds: s.bounds || {}
            }))
        };
    }
    await fs.writeJson(path.join(outputDir, 'data', 'steps_metadata.json'), simpleMetadata, { spaces: 2 });

    // Execute Batch Render if applicable
    if (renderer instanceof BlenderService) {
        Logger.log("--- Flushing remaining render jobs ---");
        await renderer.executeQueue();
    }

    const endTime = Date.now();
    const duration = (endTime - startTime) / 1000;
    Logger.finishProgress();
    Logger.log(`Done. Total execution time: ${duration.toFixed(2)} seconds.`);
}

main().catch(err => {
    Logger.error(err);
    process.exit(1);
});
