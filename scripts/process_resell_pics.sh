#!/usr/bin/env bash
#
# process_resell_pics.sh — Batch-process resale product photos with Qwen Code CLI
#
# Takes an input directory of product photos and an output directory.
# Originals are never modified. Each image is copied to the output directory,
# then a headless qwen-cli agent (Qwen3.8-27B-NVFP4 by default) analyzes the
# photo and applies resale-oriented edits via the image-edit-mcp tools:
#   - Auto-orient (rotate/flip if needed)
#   - Crop unnecessary background/whitespace around the product
#   - Clean up plain backgrounds (whiten borders)
#   - Brightness / contrast / saturation boost
#   - Save result to the output directory
#
# Usage:
#   ./process_resell_pics.sh <input-dir> <output-dir> [options]
#
# Options:
#   --model NAME        qwen model id (default: vllm/RadixArk/Qwen3.8-27B-NVFP4)
#   --dry-run           List files that would be processed, exit
#   --limit N           Process only the first N images
#   --timeout SECS      Per-image timeout in seconds (default: 300)
#   -h, --help          Show help

set -euo pipefail

MODEL="vllm/Qwen/Qwen3.6-35B-A3B-FP8"
TIMEOUT=300
LIMIT=0
INPUT_DIR=""
OUTPUT_DIR=""

usage() {
  sed -n '2,/^$/p' "$0" | sed 's/^# \{0,1\}//'
  exit 0
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --model)   MODEL="$2"; shift 2 ;;
    --limit)   LIMIT="$2"; shift 2 ;;
    --timeout) TIMEOUT="$2"; shift 2 ;;
    --dry-run) DRY_RUN=true; shift ;;
    -h|--help) usage ;;
    -*) echo "unknown option: $1" >&2; exit 2 ;;
    *)
      if   [[ -z "$INPUT_DIR"  ]]; then INPUT_DIR="$1"
      elif [[ -z "$OUTPUT_DIR" ]]; then OUTPUT_DIR="$1"
      else echo "unexpected argument: $1" >&2; exit 2
      fi
      shift ;;
  esac
done

[[ -n "$INPUT_DIR"  && -n "$OUTPUT_DIR" ]] || { usage; }
[[ -d "$INPUT_DIR"  ]] || { echo "input dir not found: $INPUT_DIR" >&2; exit 1; }

mkdir -p "$OUTPUT_DIR"
OUTPUT_DIR="$(cd "$OUTPUT_DIR" && pwd)"

mapfile -t IMAGES < <(find "$INPUT_DIR" -maxdepth 1 -type f \
  \( -iname '*.jpg' -o -iname '*.jpeg' -o -iname '*.png' -o -iname '*.webp' -o -iname '*.bmp' -o -iname '*.tiff' -o -iname '*.heic' \) | sort)

if [[ ${#IMAGES[@]} -eq 0 ]]; then
  echo "No supported images found in $INPUT_DIR" >&2
  exit 1
fi

if [[ $LIMIT -gt 0 ]]; then
  IMAGES=("${IMAGES[@]:0:$LIMIT}")
fi

echo "Processing ${#IMAGES[@]} image(s) with model: $MODEL"
echo "Input:  $INPUT_DIR"
echo "Output: $OUTPUT_DIR"
echo

if [[ "${DRY_RUN:-false}" == "true" ]]; then
  printf '%s\n' "${IMAGES[@]}"
  exit 0
fi

export QWEN_CODE_SUPPRESS_YOLO_WARNING=1

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

PROMPT_FILE="$WORK/prompt.txt"
cat > "$PROMPT_FILE" <<PROMPT
You are a product-photo retoucher preparing photos for a resale listing.
The image-edit MCP server is connected. Work on ONE image only.

Target file: __TARGET__

Steps:
1. Call load_image on the target file path.
2. Apply these resale-oriented edits using image-edit MCP tools:
   a. Crop the image to remove excess empty/white background and focus on the
      product. Use crop_image with a generous inset (about 10-15% from each edge)
      to remove whitespace while keeping the product visible.
   b. Apply adjust_brightness (+0.1) and adjust_contrast (+0.15) to make the
      product look crisp and appealing.
   c. If the image looks slightly desaturated, apply adjust_saturation (+0.1).
   d. Apply apply_sharpen to make product details look crisp.
3. Call save_image to save the result to the SAME target file path (overwrite).

Rules:
- Only modify the target file. Do NOT create, delete, or modify any other file.
- Do NOT run any shell commands.
- Do NOT call read_image — it returns an enormous base64 blob and wastes your context.
- Keep edits subtle and professional — do not over-edit.
- When done, reply with a one-line summary of the edits you applied.
PROMPT

OK=0
FAIL=0
SKIP=0
IDX=0

for src in "${IMAGES[@]}"; do
  IDX=$((IDX + 1))
  base="$(basename "$src")"
  dest="$OUTPUT_DIR/$base"
  echo "[$IDX/${#IMAGES[@]}] $base"

  if [[ -f "$dest" ]]; then
    echo "    SKIP (already exists)"
    SKIP=$((SKIP + 1))
    continue
  fi

  cp -p "$src" "$dest"

  PROMPT="$(sed "s|__TARGET__|$dest|g" "$PROMPT_FILE")"

  if timeout "$TIMEOUT" qwen --yolo --model "$MODEL" -p "$PROMPT" > "$WORK/qwen_$IDX.log" 2>&1; then
    if [[ -f "$dest" && $(stat -c%s "$dest") -gt 0 ]]; then
      echo "    OK  -> $dest"
      OK=$((OK + 1))
    else
      echo "    FAIL (output missing or empty)  log: $WORK/qwen_$IDX.log"
      tail -5 "$WORK/qwen_$IDX.log" | sed 's/^/    | /'
      FAIL=$((FAIL + 1))
    fi
  else
    echo "    FAILED (log: $WORK/qwen_$IDX.log)"
    tail -5 "$WORK/qwen_$IDX.log" | sed 's/^/    | /'
    FAIL=$((FAIL + 1))
  fi
done

echo
echo "=== Summary: $OK ok, $FAIL failed, $SKIP skipped (out of ${#IMAGES[@]}) ==="
[[ $FAIL -eq 0 ]]
