import * as fs from 'fs-extra';

export interface LDrawColor {
    code: number;
    name: string;
    hex: string;
    alpha: number;
    material: string; // CHROME, RUBBER, etc.
}

export class LDConfigParser {
    public static async parse(filePath: string): Promise<Map<number, LDrawColor>> {
        const colors = new Map<number, LDrawColor>();
        const content = await fs.readFile(filePath, 'utf-8');
        const lines = content.split(/\r?\n/);

        for (const line of lines) {
            const trimmed = line.trim();
            if (trimmed.startsWith("0 !COLOUR")) {
                try {
                    // Example: 0 !COLOUR Black CODE 0 VALUE #000000 EDGE #595959
                    // Regex is simpler here
                    const nameMatch = trimmed.match(/!COLOUR\s+(\S+)/);
                    const codeMatch = trimmed.match(/CODE\s+(\d+)/);
                    const valueMatch = trimmed.match(/VALUE\s+(#[\w\d]+)/);
                    const alphaMatch = trimmed.match(/ALPHA\s+(\d+)/);
                    const materialMatch = trimmed.match(/MATERIAL\s+(\w+)/);

                    if (nameMatch && codeMatch && valueMatch) {
                        const code = parseInt(codeMatch[1]);
                        const color: LDrawColor = {
                            code: code,
                            name: nameMatch[1],
                            hex: valueMatch[1],
                            alpha: alphaMatch ? parseInt(alphaMatch[1]) : 255,
                            material: materialMatch ? materialMatch[1] : 'PLASTIC'
                        };
                        colors.set(code, color);
                    }
                } catch (e) {
                    console.warn(`Failed to parse color line: ${trimmed}`, e);
                }
            }
        }
        return colors;
    }
}
