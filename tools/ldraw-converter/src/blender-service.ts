import { spawn } from 'child_process';
import * as path from 'path';
import * as fs from 'fs-extra';
import { Logger } from './logger';

export interface CameraTransform {
    loc: { x: number, y: number, z: number };
    lookAt: { x: number, y: number, z: number };
    up: { x: number, y: number, z: number };
}

export interface RenderJob {
    input: string;
    output: string;
    width: number;
    height: number;
    locX: number;
    locY: number;
    locZ: number;
    lookAtX: number;
    lookAtY: number;
    lookAtZ: number;
    upX: number;
    upY: number;
    upZ: number;
    color?: { r: number, g: number, b: number };
    save_blend?: boolean;
    center?: { x: number, y: number, z: number };
    bounds?: { min: { x: number, y: number, z: number }, max: { x: number, y: number, z: number } };
}

export class BlenderService {
    private readonly BATCH_SIZE = 50; // Render every 50 images
    private blenderPath: string;
    private scriptPath: string;
    private jobQueue: RenderJob[] = [];

    constructor(blenderPath: string) {
        this.blenderPath = blenderPath;
        this.scriptPath = path.resolve(__dirname, 'blender/render_script.py');
    }

    public async queueJob(objPath: string, outputPath: string, width: number, height: number, camera: CameraTransform, color?: { r: number, g: number, b: number }, saveBlend: boolean = false) {
        this.jobQueue.push({
            input: objPath,
            output: outputPath,
            width,
            height,
            locX: camera.loc.x,
            locY: camera.loc.y,
            locZ: camera.loc.z,
            lookAtX: camera.lookAt.x,
            lookAtY: camera.lookAt.y,
            lookAtZ: camera.lookAt.z,
            upX: camera.up.x,
            upY: camera.up.y,
            upZ: camera.up.z,
            color,
            save_blend: saveBlend
        });

        // Auto-flush queue if batch size reached
        if (this.jobQueue.length >= this.BATCH_SIZE) {
            await this.executeQueue();
        }
    }

    public async executeQueue(): Promise<void> {
        if (this.jobQueue.length === 0) return;

        Logger.log(`[BlenderService] Executing batch of ${this.jobQueue.length} jobs...`);
        // Use unique filename to avoid collisions if called roughly in parallel (though we await it)
        const batchFile = path.resolve(path.dirname(this.scriptPath), `batch_jobs_${Date.now()}.json`);

        // Take current batch and clear queue immediately
        const currentBatch = [...this.jobQueue];
        this.jobQueue = [];

        await fs.writeJson(batchFile, currentBatch);

        const args = [
            '--background',
            '--python', this.scriptPath,
            '--',
            '--batch', batchFile
        ];

        return new Promise((resolve, reject) => {
            const process = spawn(this.blenderPath, args);
            let stderr = '';
            let stdout = '';

            process.stdout.on('data', (data) => {
                const str = data.toString();
                stdout += str;
                if (str.includes('[Blender Debug]')) {
                    Logger.log(str.trim());
                }
            });
            process.stderr.on('data', (data) => {
                const str = data.toString();
                stderr += str;
                // console.log(`[Blender Stderr] ${str.trim()}`); // Optional logging
            });

            process.on('close', async (code) => {
                // Clean up batch file
                if (fs.existsSync(batchFile)) await fs.unlink(batchFile);

                if (code === 0) {
                    resolve();
                } else {
                    Logger.error(`[Blender] Error Output:\n${stdout}\n${stderr}`);
                    reject(new Error(`Blender Batch failed with code ${code}`));
                }
            });
            process.on('error', (err) => reject(err));
        });
    }

    public async render(objPath: string, outputPath: string, width: number, height: number, camera: CameraTransform, color?: { r: number, g: number, b: number }, center?: { x: number, y: number, z: number }, bounds?: { min: { x: number, y: number, z: number }, max: { x: number, y: number, z: number } }, saveBlend: boolean = false): Promise<void> {
        // Ensure output dir exists
        await fs.ensureDir(path.dirname(outputPath));

        const args = [
            '--background',
            '--python', this.scriptPath,
            '--',
            '--input', objPath,
            '--output', outputPath,
            `--width=${width}`,
            `--height=${height}`,
            `--locX=${camera.loc.x.toFixed(4)}`,
            `--locY=${camera.loc.y.toFixed(4)}`,
            `--locZ=${camera.loc.z.toFixed(4)}`,
            `--lookAtX=${camera.lookAt.x.toFixed(4)}`,
            `--lookAtY=${camera.lookAt.y.toFixed(4)}`,
            `--lookAtZ=${camera.lookAt.z.toFixed(4)}`,
            `--upX=${camera.up.x.toFixed(4)}`,
            `--upY=${camera.up.y.toFixed(4)}`,
            `--upZ=${camera.up.z.toFixed(4)}`
        ];

        if (color) {
            args.push(`--r=${color.r.toFixed(4)}`);
            args.push(`--g=${color.g.toFixed(4)}`);
            args.push(`--b=${color.b.toFixed(4)}`);
        }

        if (saveBlend) {
            args.push('--save_blend');
        }

        return new Promise((resolve, reject) => {
            Logger.log(`[Blender] Rendering ${path.basename(objPath)}...`);
            Logger.log(`[Blender] Args: ${args.join(' ')}`);
            const process = spawn(this.blenderPath, args);

            let stderr = '';
            let stdout = '';

            process.stdout.on('data', (data) => {
                const str = data.toString();
                stdout += str;
                // Stream debug lines directly to console
                if (str.includes('[Blender Debug]')) {
                    Logger.log(str.trim());
                }
            });
            process.stderr.on('data', (data) => {
                const str = data.toString();
                stderr += str;
                Logger.log(`[Blender Stderr] ${str.trim()}`);
            });

            process.on('close', (code) => {
                if (code === 0) {
                    resolve();
                } else {
                    Logger.error(`[Blender] Error Output:\n${stdout}\n${stderr}`);
                    reject(new Error(`Blender failed with code ${code}`));
                }
            });

            process.on('error', (err) => {
                reject(err);
            });
        });
    }
}
