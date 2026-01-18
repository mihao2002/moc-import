# LDraw to Asset Converter

This tool converts LDraw (`.ldr`, `.mpd`) models into 3D assets (OBJ) and rendered images (PNG) for use in other applications. It generates:
-   **OBJ Files**: Geometry for the main model and all distinct parts/submodels.
-   **Part Images**: Thumbnails for every unique part.
-   **Step Images**: Step-by-step building instructions (if steps are present).

## Features

-   **Pipeline Rendering**: Processes rendering jobs in the background (batches of 50) while parsing continues, significantly improving performance for large models.
-   **Memory Optimization**: Uses streamed writing for OBJ files to handle complex models without running out of memory.
-   **Progress Feedback**: Shows real-time progress for asset export and image rendering steps.
-   **Headless Rendering**: Uses Blender in background mode for high-quality, scriptable rendering.

## Prerequisites

1.  **Node.js**: Installed and available in PATH.
2.  **Blender**: Version 3.0 or later (Tested with 5.0).
3.  **LDraw Part Library**: The official LDraw parts library (e.g., `C:\Users\Public\Documents\LDraw`).

## Installation

Run the following command in this directory to install dependencies:

```bash
npm install
```

## Usage

Run the converter using `ts-node` (via `npx`):

```bash
npx ts-node src/main.ts --input <INPUT_LDR> --output <OUTPUT_DIR> --library <LDRAW_LIB_PATH> --blender <BLENDER_EXE_PATH>
```

### Arguments

| Argument | Description | Required | Example |
| :--- | :--- | :--- | :--- |
| `--input` | Path to the source LDraw file (`.ldr` or `.mpd`) | Yes | `"C:\Models\car.ldr"` |
| `--output` | Directory where assets will be generated | Yes | `"./output_car"` |
| `--library` | Path to the root of the LDraw Parts Library | Yes | `"C:\LDraw"` |
| `--blender` | Path to the Blender executable | Yes | `"C:\Program Files\Blender Foundation\Blender 4.0\blender.exe"` |
| `--width` | Output image width (default: 1024) | No | `512` |
| `--height` | Output image height (default: 1024) | No | `512` |
| `--save-blend` | Save .blend files for debugging | No | (ignored) |
| `--quiet` | Suppress verbose output (logs only progress) | No | (flag) |

### Example Command

```bash
npx ts-node src/main.ts \
  --input "C:\Users\mihao\Documents\test.ldr" \
  --output "output_test" \
  --library "C:\Users\Public\Documents\LDraw" \
  --blender "C:\Program Files\Blender Foundation\Blender 5.0\blender.exe"
```

## Progress Output

The tool provides real-time progress feedback in the console. Here is how to interpret the output:

### 1. Exporting Assets
*Format:* `Exporting Assets: [nCurrent] / [nTotal] (Part: [pTotal] - [CurrentItemName])`

-   **[nCurrent]**: The index of the *Model* currently being processed.
-   **[nTotal]**: The total number of unique Models (Main Model + Submodels) found in the input file.
-   **[pTotal]**: The cumulative count of individual *Parts* (bricks) and *Submodels* processed so far across all models.
-   **[CurrentItemName]**: The name of the specific part file or submodel currently being exported.

> **Note**: It is normal for the process to hang on a specific item (e.g., `Part: 0 - -chassis.ldr`) if that item is very large or complex. The tool is likely calculating geometry, not frozen.

### 2. Rendering Steps
*Format:* `Rendering Steps: [sCurrent] / [sTotal]`

-   **[sCurrent]**: The index of the building step currently being rendered.
-   **[sTotal]**: The total number of steps in the current model.

## Output Structure

The output directory will contain:
-   `images/`
    -   `parts/`: Thumbnails of individual parts.
    -   `steps/`: Step-by-step instruction images.
    -   `submodels/`: Thumbnails of internal submodels.
-   `models/`: OBJ files for the model and its components.
-   `parts_map.json`: A mapping of part IDs to their generated assets.
