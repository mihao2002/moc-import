# Manual Generator

A tool to generate high-quality PDF building instructions from LDraw models (`.ldr`, `.mpd`), featuring professional Line Art rendering and responsive layouts.

## Features

*   **Line Art Rendering**: Produces "LEGO Instruction" style visuals with black/white outlines and flat shading using Blender Freestyle.
*   **Submodel Support**: Automatically recurses through complex MPD files, generating instructions for sub-assemblies before the main model.
*   **Breadcrumb Navigation**: Visual hierarchy at the top of each page showing the current submodule context.
*   **Responsive Layout**:
    *   **A2 Landscape**: optimized for high-resolution readability.
    *   **Vertical Parts List**: Auto-wrapping columns to prevent truncation of large part lists.
    *   **Dynamic Scaling**: Images resize to fit available space without cutoff.
*   **Step Highlighting**: Previous parts are rendered translucently (ghosted) to emphasize new additions.

## Prerequisites

1.  **Node.js**: v18+ recommended.
2.  **Blender**: Version 4.0 or higher (5.0 supported).
3.  **LDraw Library**: The official LDraw parts library must be installed.
    *   Default path assumed: `C:\Users\mihao\LDraw` (Verify this in `src/index.ts` or via CLI args).

## Installation

```bash
cd tools/manual-generator
npm install
```

## Usage

Run the tool using `ts-node`:

```bash
npx ts-node src/index.ts --input <path_to_model> --output <output_pdf>
```

### Arguments

*   `--input`, `-i`: Path to the input LDraw file (Required).
*   `--output`, `-o`: Path to the output PDF file (Default: `instructions.pdf`).
*   `--library`: Path to the LDraw parts library (Default: `C:\Users\mihao\LDraw`).
*   `--blender`: Path to the Blender executable (Default: `C:\Program Files\Blender Foundation\Blender 5.0\blender.exe`).
*   `--width`: Output image width (Default: 2048).
*   `--height`: Output image height (Default: 1536).
*   `--quiet`: Suppress verbose Blender logs.
*   `--save-blend`: Save intermediate `.blend` files for debugging.

### Example

Generate instructions for a complex MPD model:

```bash
npx ts-node src/index.ts --input "C:\Users\mihao\OneDrive\Documents\test3.mpd" --output manual_v2.pdf --quiet
```
