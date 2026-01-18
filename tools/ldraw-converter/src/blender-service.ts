import { spawn } from 'child_process';
import * as path from 'path';
import * as fs from 'fs-extra';

export interface CameraTransform {
    loc: { x: number, y: number, z: number };
    lookAt: { x: number, y: number, z: number };
    up: { x: number, y: number, z: number };
}

export class BlenderService {
    private blenderPath: string;
    private scriptPath: string;

    constructor(blenderPath: string) {
        this.blenderPath = blenderPath;
        this.scriptPath = path.resolve(__dirname, 'blender/render_script.py');
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
            console.log(`[Blender] Rendering ${path.basename(objPath)}...`);
            console.log(`[Blender] Args: ${args.join(' ')}`);
            const process = spawn(this.blenderPath, args);

            let stderr = '';
            let stdout = '';

            process.stdout.on('data', (data) => {
                const str = data.toString();
                stdout += str;
                // Stream debug lines directly to console
                if (str.includes('[Blender Debug]')) {
                    console.log(str.trim());
                }
            });
            process.stderr.on('data', (data) => {
                const str = data.toString();
                stderr += str;
                console.log(`[Blender Stderr] ${str.trim()}`);
            });

            process.on('close', (code) => {
                if (code === 0) {
                    resolve();
                } else {
                    console.error(`[Blender] Error Output:\n${stdout}\n${stderr}`);
                    reject(new Error(`Blender failed with code ${code}`));
                }
            });

            process.on('error', (err) => {
                reject(err);
            });
        });
    }
}
