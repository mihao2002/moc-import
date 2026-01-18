import * as fs from 'fs-extra';
import * as path from 'path';

export interface Vector3 {
    x: number;
    y: number;
    z: number;
}

export interface Matrix4x4 {
    c0: { x: number, y: number, z: number, w: number };
    c1: { x: number, y: number, z: number, w: number };
    c2: { x: number, y: number, z: number, w: number };
    c3: { x: number, y: number, z: number, w: number };
}

export interface LDrawPart {
    partId: string;
    description?: string;
    position: Vector3;
    rotation: Matrix4x4; // We will store full matrix for now
    color: number;
    // Helper to identify if it's a submodel
    isSubmodel: boolean;
}

export interface LDrawStep {
    parts: LDrawPart[];
    rotationRef?: number; // Index of step that defines rotation
    cameraParams?: any; // For image generation
}


export interface LDrawTriangle {
    color: number;
    v1: Vector3;
    v2: Vector3;
    v3: Vector3;
}

export interface LDrawQuad {
    color: number;
    v1: Vector3;
    v2: Vector3;
    v3: Vector3;
    v4: Vector3;
}

export interface RuntimeModelData {
    modelName: string;
    steps: LDrawStep[]; // For MPD/LDR steps
    // Raw geometry (for Parts .dat)
    triangles: LDrawTriangle[];
    quads: LDrawQuad[];
    // Subparts/Primitives referenced in this file (Type 1 lines that are NOT steps, but part composition)
    // Actually, Type 1 lines are parsed as "parts" in steps. 
    // For a .dat file, it usually has 0 steps (or 1 implicit step).
    dependencies: Set<string>;
}

export class LDrawParser {
    private models: Map<string, RuntimeModelData> = new Map();
    private fileSections: { name: string, lines: string[] }[] = [];

    constructor() { }

    public async parseModels(filePath: string): Promise<Map<string, RuntimeModelData>> {
        const content = await fs.readFile(filePath, 'utf-8');
        return this.parseString(content, path.basename(filePath).toLowerCase());
    }

    public getModel(modelName: string): RuntimeModelData | undefined {
        return this.models.get(modelName.toLowerCase());
    }

    public parseString(content: string, defaultName: string = "main.ldr"): Map<string, RuntimeModelData> {
        this.models.clear();
        this.fileSections = [];
        const lines = content.split(/\r?\n/);

        this.splitIntoSections(lines, defaultName);

        for (const section of this.fileSections) {
            this.models.set(section.name, this.parseSection(section.name, section.lines));
        }

        return this.models;
    }

    private splitIntoSections(lines: string[], defaultName: string) {
        let currentModelName = defaultName;
        let currentLines: string[] = [];

        for (let i = 0; i < lines.length; i++) {
            const line = lines[i].trim();
            if (line.startsWith("0 FILE ")) {
                // End previous section
                if (currentLines.length > 0) {
                    this.fileSections.push({ name: currentModelName, lines: currentLines });
                }
                currentModelName = line.substring(7).trim().toLowerCase();
                currentLines = [];
            }
            currentLines.push(line);
        }
        // Push last section
        if (currentLines.length > 0) {
            this.fileSections.push({ name: currentModelName, lines: currentLines });
        }
    }

    private parseSection(modelName: string, lines: string[]): RuntimeModelData {
        const steps: LDrawStep[] = [];
        let currentStep: LDrawStep = { parts: [] };
        const dependencies = new Set<string>();
        const triangles: LDrawTriangle[] = [];
        const quads: LDrawQuad[] = [];
        let currentRotation: { x: number, y: number, z: number, type: string } | undefined;

        for (const line of lines) {
            const trimmed = line.trim();
            if (!trimmed) continue;

            const tokens = trimmed.split(/\s+/);
            const type = tokens[0];

            if (type === "0") {
                const directive = tokens[1] ? tokens[1].toUpperCase() : "";

                if (directive === "STEP") {
                    // Apply inherited rotation to the step being finished if not set
                    if (currentRotation) {
                        if (!currentStep.cameraParams) currentStep.cameraParams = {};
                        if (!currentStep.cameraParams.angles) currentStep.cameraParams.angles = { ...currentRotation };
                    }

                    if (currentStep.parts.length > 0) {
                        steps.push(currentStep);
                        currentStep = { parts: [] };
                    }
                } else if (directive === "ROTSTEP") {
                    // Format: 0 ROTSTEP <x> <y> <z> ABS/REL
                    if (tokens.length >= 5) {
                        const x = parseFloat(tokens[2]);
                        const y = parseFloat(tokens[3]);
                        const z = parseFloat(tokens[4]);
                        const rotType = tokens[5] || "ABS";

                        // If the current step already has a specific camera assigned (and hasn't been pushed yet),
                        // and we encounter ANOTHER ROTSTEP, we should treat the previous one as a complete "view step".
                        // This allows for sequences of view changes (animations) without adding parts.
                        if (currentStep.cameraParams && currentStep.parts.length === 0) {
                            steps.push(currentStep);
                            currentStep = { parts: [] };
                        }

                        // Update persistent state
                        currentRotation = { x, y, z, type: rotType };

                        // Apply to current step - it terminates this step with this view
                        if (!currentStep.cameraParams) currentStep.cameraParams = {};
                        currentStep.cameraParams.angles = { ...currentRotation };
                    }

                    // ROTSTEP acts as a step delimiter if we have parts
                    if (currentStep.parts.length > 0) {
                        steps.push(currentStep);
                        currentStep = { parts: [] };
                    }
                }
            } else if (type === "1") {
                // Part/Submodel reference
                if (tokens.length >= 15) {
                    const color = parseInt(tokens[1]);
                    const x = parseFloat(tokens[2]);
                    const y = parseFloat(tokens[3]);
                    const z = parseFloat(tokens[4]);
                    const m00 = parseFloat(tokens[5]); const m01 = parseFloat(tokens[6]); const m02 = parseFloat(tokens[7]);
                    const m10 = parseFloat(tokens[8]); const m11 = parseFloat(tokens[9]); const m12 = parseFloat(tokens[10]);
                    const m20 = parseFloat(tokens[11]); const m21 = parseFloat(tokens[12]); const m22 = parseFloat(tokens[13]);
                    const partFile = tokens.slice(14).join(" ").toLowerCase();

                    // Pure 1:1 Mapping (No Transforms)
                    const part: LDrawPart = {
                        partId: partFile,
                        color: color,
                        position: { x, y, z: z },
                        rotation: {
                            c0: { x: m00, y: m10, z: m20, w: 0 },
                            c1: { x: m01, y: m11, z: m21, w: 0 },
                            c2: { x: m02, y: m12, z: m22, w: 0 },
                            c3: { x, y, z: z, w: 1 }
                        },
                        isSubmodel: false
                    };

                    currentStep.parts.push(part);
                    dependencies.add(partFile);
                }
            } else if (type === "3") {
                // Triangle
                // 3 <colour> x1 y1 z1 x2 y2 z2 x3 y3 z3
                if (tokens.length >= 11) {
                    triangles.push({
                        color: parseInt(tokens[1]),
                        // Raw Coordinates (No Z-flip, No Winding Change)
                        v1: { x: parseFloat(tokens[2]), y: parseFloat(tokens[3]), z: parseFloat(tokens[4]) },
                        v2: { x: parseFloat(tokens[5]), y: parseFloat(tokens[6]), z: parseFloat(tokens[7]) },
                        v3: { x: parseFloat(tokens[8]), y: parseFloat(tokens[9]), z: parseFloat(tokens[10]) }
                    });
                }
            } else if (type === "4") {
                // Quad
                // 4 <colour> x1 y1 z1 x2 y2 z2 x3 y3 z3 x4 y4 z4
                if (tokens.length >= 14) {
                    quads.push({
                        color: parseInt(tokens[1]),
                        // Raw Coordinates (No Z-flip, No Winding Change)
                        v1: { x: parseFloat(tokens[2]), y: parseFloat(tokens[3]), z: parseFloat(tokens[4]) },
                        v2: { x: parseFloat(tokens[5]), y: parseFloat(tokens[6]), z: parseFloat(tokens[7]) },
                        v3: { x: parseFloat(tokens[8]), y: parseFloat(tokens[9]), z: parseFloat(tokens[10]) },
                        v4: { x: parseFloat(tokens[11]), y: parseFloat(tokens[12]), z: parseFloat(tokens[13]) }
                    });
                }
            }
        }

        if (currentStep.parts.length > 0 || currentStep.cameraParams) {
            // Inherit rotation for final step
            if (currentRotation) {
                if (!currentStep.cameraParams) currentStep.cameraParams = {};
                // Only if not already set (which it won't be if it's just parts)
                if (!currentStep.cameraParams.angles) currentStep.cameraParams.angles = { ...currentRotation };
            }
            steps.push(currentStep);
        }

        // Fix for Geometry-Only Models (e.g. flexible parts defined in MPD without explicit steps)
        if (steps.length === 0 && (triangles.length > 0 || quads.length > 0)) {
            steps.push({ parts: [] });
        }

        return {
            modelName,
            steps,
            triangles,
            quads,
            dependencies
        };
    }
}
