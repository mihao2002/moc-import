import { spawn } from 'child_process';
import * as fs from 'fs-extra';
import * as path from 'path';

export class LDViewService {
    private ldviewPath: string;
    private libraryPath: string;

    constructor(ldviewPath: string = "LDView", libraryPath: string) {
        this.ldviewPath = ldviewPath;
        this.libraryPath = libraryPath;
    }

    public async exportGeometry(inputPath: string, outputPath: string): Promise<void> {
        // LDView.exe input.dat -ExportFile=output.obj -ExportFmt=OBJ -TexCoords=1 -Normals=1
        const args = [
            inputPath,
            `-ExportFile=${outputPath}`,
            '-ExportFmt=OBJ',
            '-TexCoords=1',
            '-Normals=1',
            '-Submodels=0', // Flatten submodels? No, we want to export JUST this part/submodel
            '-Scope=0',
            `-LibraryPath=${this.libraryPath}`
        ];

        await this.runLDView(args);
    }

    public async exportSnapshot(inputPath: string, outputPath: string, width: number = 512, height: number = 512, color?: number, camera?: { x: number, y: number, z: number, type?: string }): Promise<void> {
        // LDView.exe input.dat -SaveSnapShot=output.png -DefaultLatLong=45,30 -SaveWidth=512 -SaveHeight=512
        const args = [
            inputPath,
            `-SaveSnapShot=${outputPath}`,
            `-SaveWidth=${width}`,
            `-SaveHeight=${height}`,
            `-LibraryPath=${this.libraryPath}`,
            '-ProcessLDConfig=1'
        ];

        if (color !== undefined) {
            args.push(`-DefaultColor=${color}`);
        }

        if (camera) {
            args.push(`-Latitude=${camera.x}`);
            args.push(`-Longitude=${-camera.y}`);
            if (camera.z !== 0) {
                args.push(`-Rotation=${-camera.z}`);
            }
        } else {
            args.push('-DefaultLatLong=45,30');
            args.push('-Ortho=1');
        }

        await this.runLDView(args);
    }

    // For baking submodels, we might want to recursively load them. 
    // Actually, if we pass a submodel file that contains references, LDView handles it.
    // If we want to bake it flattened, we use -Submodels=1 (default is usually 1, but we might check).

    private runLDView(args: string[]): Promise<void> {
        return new Promise((resolve, reject) => {
            console.log(`Running: ${this.ldviewPath} ${args.join(' ')}`);
            const process = spawn(this.ldviewPath, args);

            let stderr = '';

            process.stderr.on('data', (data) => {
                stderr += data.toString();
            });

            process.on('close', (code) => {
                if (code === 0) {
                    resolve();
                } else {
                    reject(new Error(`LDView failed with code ${code}: ${stderr}`));
                }
            });

            process.on('error', (err) => {
                reject(err);
            });
        });
    }
}
