
import * as fs from 'fs-extra';
import * as path from 'path';
import { LDrawParser, RuntimeModelData, Vector3, Matrix4x4 } from './parser';
import { Logger } from './logger';

// Helper interface for Face Data with Color
interface FaceData {
    v1: number;
    v2: number;
    v3: number;
    color: number;
}

interface ObjectGroupData {
    vertices: Vector3[];
    faces: FaceData[];
}

export class ObjExporter {
    private parser: LDrawParser;
    private internalParser = new LDrawParser(); // Separate parser for loading parts to avoid clearing the main parser's map
    private partCache: Map<string, RuntimeModelData> = new Map();
    private libraryPaths: string[] = [];

    // Storage for Object Groups
    private objectGroups = new Map<string, ObjectGroupData>();

    constructor(parser: LDrawParser, libraryPaths: string[]) {
        this.parser = parser;
        this.libraryPaths = libraryPaths;
    }

    // Helper to multiply matrix by vector
    private multiplyMatrixVector(m: Matrix4x4, v: Vector3): Vector3 {
        return {
            x: m.c0.x * v.x + m.c1.x * v.y + m.c2.x * v.z + m.c3.x,
            y: m.c0.y * v.x + m.c1.y * v.y + m.c2.y * v.z + m.c3.y,
            z: m.c0.z * v.x + m.c1.z * v.y + m.c2.z * v.z + m.c3.z
        };
    }

    // Helper to multiply two matrices
    private multiplyMatrices(a: Matrix4x4, b: Matrix4x4): Matrix4x4 {
        // C = A * B
        // Columns of B are transformed by A
        // This is a standard 4x4 mul, simplified for our struct
        // We only care about 3x4 affine really, but w=1 implies strict math

        // Helper to get col as vector
        const getCol = (m: Matrix4x4, idx: number) => {
            if (idx === 0) return m.c0;
            if (idx === 1) return m.c1;
            if (idx === 2) return m.c2;
            return m.c3;
        }

        const resCol = (c: { x: number, y: number, z: number, w: number }) => {
            return {
                x: a.c0.x * c.x + a.c1.x * c.y + a.c2.x * c.z + a.c3.x * c.w,
                y: a.c0.y * c.x + a.c1.y * c.y + a.c2.y * c.z + a.c3.y * c.w,
                z: a.c0.z * c.x + a.c1.z * c.y + a.c2.z * c.z + a.c3.z * c.w,
                w: c.w // Assuming rigid body mostly
            };
        };

        return {
            c0: resCol(b.c0),
            c1: resCol(b.c1),
            c2: resCol(b.c2),
            c3: resCol(b.c3)
        };
    }

    private findPartFile(partId: string): string | null {
        for (const libraryPath of this.libraryPaths) {
            let p = path.join(libraryPath, 'parts', partId);
            if (fs.existsSync(p)) return p;
            p = path.join(libraryPath, 'parts', partId + ".dat");
            if (fs.existsSync(p)) return p;
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

    private async loadPart(partId: string): Promise<RuntimeModelData | null> {
        if (this.partCache.has(partId)) return this.partCache.get(partId)!;

        // Try resolving via Parser (MPD support)
        let mpdModel = this.parser.getModel(partId);

        // Robust Lookup Logic: Handle Extension and Slash mismatches
        if (!mpdModel) {
            // 1. Try stripping extension (e.g. 'sub.ldr' -> 'sub')
            const noExt = partId.replace(/\.(ldr|dat|mpd)$/i, '');
            if (noExt !== partId) {
                mpdModel = this.parser.getModel(noExt);
            }

            // 2. Try normalizing slashes (e.g. 'sub/part' <-> 'sub\part')
            if (!mpdModel) {
                const withForward = partId.replace(/\\/g, '/');
                if (withForward !== partId) mpdModel = this.parser.getModel(withForward);

                if (!mpdModel) {
                    const withBack = partId.replace(/\//g, '\\');
                    if (withBack !== partId) mpdModel = this.parser.getModel(withBack);
                }
            }

            // 3. Try both (No Ext + Normalized Slashes)
            if (!mpdModel && noExt !== partId) {
                const withForward = noExt.replace(/\\/g, '/');
                if (withForward !== noExt) mpdModel = this.parser.getModel(withForward);

                if (!mpdModel) {
                    const withBack = noExt.replace(/\//g, '\\');
                    if (withBack !== noExt) mpdModel = this.parser.getModel(withBack);
                }
            }
        }

        if (mpdModel) {
            // Cache using the REQUESTED partId to speed up next lookup
            console.log(`[ObjExporter] Resolved MPD: ${partId} -> ${mpdModel.modelName}`);
            this.partCache.set(partId, mpdModel);
            return mpdModel;
        }

        const file = this.findPartFile(partId);
        if (!file) {
            console.warn(`[ObjExporter] Part definition not found: ${partId}`);
            return null;
        }
        console.log(`[ObjExporter] Resolved Disk: ${partId} -> ${file}`);

        const content = await fs.readFile(file, 'utf-8');
        const map = this.internalParser.parseString(content);
        // parser returns a map, usually with one entry named after the file or "main.ldr"
        // We take the first one
        const data = map.values().next().value;
        if (data) {
            // Force model name to match partID to avoid ambiguity
            data.modelName = partId;
            this.partCache.set(partId, data);
            return data;
        }
        return null;
    }

    private async processModelData(
        data: RuntimeModelData,
        currentMatrix: Matrix4x4,
        currentColor: number,
        parentObjectID: string | null, // If null, we generate new IDs for parts. If set, we merge into it.
        visited: Set<string>
    ) {
        // Process Triangles
        for (const tri of data.triangles) {
            const targetID = parentObjectID || "Main_Mesh";

            let finalColor = tri.color;
            if (finalColor === 16) finalColor = currentColor;

            const v1 = this.multiplyMatrixVector(currentMatrix, tri.v1);
            const v2 = this.multiplyMatrixVector(currentMatrix, tri.v2);
            const v3 = this.multiplyMatrixVector(currentMatrix, tri.v3);

            let group = this.objectGroups.get(targetID);
            if (!group) {
                group = { vertices: [], faces: [] };
                this.objectGroups.set(targetID, group);
            }

            const baseIdx = group.vertices.length + 1; // 1-based local index (will offset later)
            group.vertices.push(v1, v2, v3);
            group.faces.push({ v1: baseIdx, v2: baseIdx + 1, v3: baseIdx + 2, color: finalColor });
        }

        // Process Quads
        for (const quad of data.quads) {
            const targetID = parentObjectID || "Main_Mesh";

            let finalColor = quad.color;
            if (finalColor === 16) finalColor = currentColor;

            const v1 = this.multiplyMatrixVector(currentMatrix, quad.v1);
            const v2 = this.multiplyMatrixVector(currentMatrix, quad.v2);
            const v3 = this.multiplyMatrixVector(currentMatrix, quad.v3);
            const v4 = this.multiplyMatrixVector(currentMatrix, quad.v4);

            let group = this.objectGroups.get(targetID);
            if (!group) {
                group = { vertices: [], faces: [] };
                this.objectGroups.set(targetID, group);
            }

            const baseIdx = group.vertices.length + 1;
            group.vertices.push(v1, v2, v3, v4);
            // Quad split to 2 triangles
            group.faces.push({ v1: baseIdx, v2: baseIdx + 1, v3: baseIdx + 2, color: finalColor });
            group.faces.push({ v1: baseIdx, v2: baseIdx + 2, v3: baseIdx + 3, color: finalColor });
        }

        // Process Types 1 (Subparts/Steps)
        // Use for loop with index to generate unique IDs
        let pIndex = 0;
        for (const step of data.steps) {
            for (const subPart of step.parts) {
                pIndex++;

                // Determine Object ID
                let subTargetID = parentObjectID;
                if (!subTargetID) {
                    // Top Level: Generate Unique ID
                    // Clean Part Name
                    const safeName = subPart.partId.replace(/[\.\\/]/g, '_');
                    subTargetID = `${safeName}_${pIndex}`;
                }

                // Debug Log Translation
                Logger.log(`[ObjExporter Debug] Part: ${subPart.partId} | ID: ${subTargetID} | Pos: ${subPart.position.x},${subPart.position.y},${subPart.position.z}`);

                const subMatrix = this.multiplyMatrices(currentMatrix, subPart.rotation);

                let nextColor = subPart.color;
                if (nextColor === 16) nextColor = currentColor;

                // Recursively accumulate geometry
                await this.accumulateGeometry(subPart.partId, subMatrix, nextColor, subTargetID, visited);
            }
        }
    }

    // Recursive function to accumulate geometry
    private async accumulateGeometry(
        partId: string,
        currentMatrix: Matrix4x4,
        currentColor: number,
        targetObjectID: string, // ID of the object to merge into
        visited: Set<string>
    ) {
        // Prevent infinite recursion (though standard LDraw shouldn't have cycles)
        // The partCache prevents re-parsing, but a visited set on partId could prevent
        // infinite recursion if a part definition directly or indirectly references itself.
        // For now, relying on LDraw's typical acyclic nature for part definitions.

        const data = await this.loadPart(partId);
        if (!data) return;

        await this.processModelData(data, currentMatrix, currentColor, targetObjectID, visited);
    }

    public async exportPart(partId: string, outputPath: string) {
        this.objectGroups.clear();
        const identity: Matrix4x4 = {
            c0: { x: 1, y: 0, z: 0, w: 0 },
            c1: { x: 0, y: 1, z: 0, w: 0 },
            c2: { x: 0, y: 0, z: 1, w: 0 },
            c3: { x: 0, y: 0, z: 0, w: 1 }
        };

        // For single part export, use PartID as object ID
        await this.accumulateGeometry(partId, identity, 16, partId, new Set());
        await this.writeObjResponse(outputPath);
    }

    public async exportModel(modelData: RuntimeModelData, outputPath: string, offset: { x: number, y: number, z: number } = { x: 0, y: 0, z: 0 }, append: boolean = false, vertexOffset: number = 0): Promise<{ count: number, bounds: { min: Vector3, max: Vector3 } }> {
        this.objectGroups.clear();
        const identity: Matrix4x4 = {
            c0: { x: 1, y: 0, z: 0, w: 0 },
            c1: { x: 0, y: 1, z: 0, w: 0 },
            c2: { x: 0, y: 0, z: 1, w: 0 },
            c3: { x: 0, y: 0, z: 0, w: 1 }
        };

        // Top Level Call: Pass null parentID to trigger separate object generation
        await this.processModelData(modelData, identity, 16, null, new Set());
        return await this.writeObjResponse(outputPath, offset, append, vertexOffset);
    }

    // Allow injecting existing models (for MPD submodels)
    public registerModel(name: string, data: RuntimeModelData) {
        this.partCache.set(name, data);
    }

    private async writeObjResponse(outputPath: string, offset: { x: number, y: number, z: number } = { x: 0, y: 0, z: 0 }, append: boolean = false, baseVertexOffset: number = 0): Promise<{ count: number, bounds: { min: Vector3, max: Vector3 } }> {
        const stream = fs.createWriteStream(outputPath, { flags: append ? 'a' : 'w' });

        // Helper to write string to stream
        const writeLine = (str: string) => {
            return new Promise<void>((resolve, reject) => {
                if (!stream.write(str + '\n')) {
                    stream.once('drain', resolve);
                } else {
                    resolve();
                }
            });
        };

        try {
            if (!append) {
                await writeLine("# Generated by LDraw-Converter Custom Exporter (Split Objects)");
                await writeLine("mtllib colors.mtl");
            }

            let globalVertexOffset = baseVertexOffset;
            let writtenVertexCount = 0;

            // Iterate Object Groups
            // Sort for deterministic order
            const sortedIDs = Array.from(this.objectGroups.keys()).sort();

            let minX = Infinity, minY = Infinity, minZ = Infinity;
            let maxX = -Infinity, maxY = -Infinity, maxZ = -Infinity;

            for (const objectID of sortedIDs) {
                const group = this.objectGroups.get(objectID)!;
                await writeLine(`o ${objectID}`);

                // Write Vertices
                for (const v of group.vertices) {
                    const x = v.x - offset.x; // These are the coordinates written to file
                    const y = v.y - offset.y;
                    const z = v.z - offset.z;

                    if (x < minX) minX = x; if (x > maxX) maxX = x;
                    if (y < minY) minY = y; if (y > maxY) maxY = y;
                    if (z < minZ) minZ = z; if (z > maxZ) maxZ = z;

                    await writeLine(`v ${x} ${y} ${z}`);
                }
                writtenVertexCount += group.vertices.length;

                // Group Faces by Color
                // Map<Color, Faces[]>
                const facesByColor = new Map<number, string[]>();

                for (const face of group.faces) {
                    if (!facesByColor.has(face.color)) facesByColor.set(face.color, []);
                    const i1 = face.v1 + globalVertexOffset;
                    const i2 = face.v2 + globalVertexOffset;
                    const i3 = face.v3 + globalVertexOffset;
                    facesByColor.get(face.color)!.push(`f ${i1} ${i2} ${i3}`);
                }

                const sortedColors = Array.from(facesByColor.keys()).sort((a, b) => a - b);
                for (const color of sortedColors) {
                    await writeLine(`usemtl code_${color}`);
                    const faces = facesByColor.get(color)!;
                    for (const faceLine of faces) {
                        await writeLine(faceLine);
                    }
                }

                globalVertexOffset += group.vertices.length;
            }
            return {
                count: writtenVertexCount,
                bounds: {
                    min: { x: minX, y: minY, z: minZ },
                    max: { x: maxX, y: maxY, z: maxZ }
                }
            };

        } finally {
            await new Promise<void>((resolve, reject) => {
                stream.end(() => {
                    resolve();
                });
            });
        }
    }
}
