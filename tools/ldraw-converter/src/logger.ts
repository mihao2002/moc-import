export class Logger {
    private static isQuiet = false;

    static init(quiet: boolean) {
        this.isQuiet = quiet;
    }

    static log(message: string, ...args: any[]) {
        if (!this.isQuiet) {
            console.log(message, ...args);
        }
    }

    static error(message: string | Error, ...args: any[]) {
        console.error(message, ...args);
    }

    static progress(current: number, total: string | number, prefix: string = "Progress", suffix: string = "") {
        if (process.stdout.isTTY) {
            process.stdout.clearLine(0);
            process.stdout.cursorTo(0);
            process.stdout.write(`${prefix}: ${current} / ${total}${suffix}`);
        } else if (!this.isQuiet) {
            // Non-TTY (e.g. piped), log periodically
            if (current % 100 === 0) {
                console.log(`${prefix}: ${current} / ${total}${suffix}`);
            }
        }
    }

    static finishProgress() {
        if (process.stdout.isTTY) {
            process.stdout.write('\n');
        }
    }
}
