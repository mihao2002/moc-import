import bpy
import os
import sys

# Hardcoded path for simplicity
img_path = "c:\\clones\\moc-import\\tools\\ldraw-converter\\manual_lineart_test.png"

print(f"Verifying Line Art Image: {img_path}")

if not os.path.exists(img_path):
    print("Image file not found!")
    sys.exit(1)

try:
    img = bpy.data.images.load(img_path)
    # Resize to 256x256 for better line detection
    img.scale(256, 256)
    resolution = 256 * 256
    pixels = img.pixels[:] # Copy to list once
    
    print(f"Image Resized to: 256x256")
    
    black_pixel_count = 0
    white_pixel_count = 0
    opaque_pixel_count = 0
    
    stride = 4 # 1 pixel = 4 floats (RGBA)
    count = len(pixels)
    samples_printed = 0
    
    print(f"Sampling pixels (stride={stride})...")
    
    for i in range(0, count, stride): 
        r = pixels[i]
        g = pixels[i+1]
        b = pixels[i+2]
        a = pixels[i+3]
        
        if a > 0.1:
            opaque_pixel_count += 1
            
            # Check for Black Line (Tolerance < 0.1)
            # Freestyle is anti-aliased, so < 0.1 is safe
            if r < 0.1 and g < 0.1 and b < 0.1:
                black_pixel_count += 1
            
            # Check for White Line (Tolerance > 0.9)
            if r > 0.9 and g > 0.9 and b > 0.9:
                white_pixel_count += 1

            if samples_printed < 15:
                # print(f"Sample: R={r:.2f} G={g:.2f} B={b:.2f} A={a:.2f}")
                samples_printed += 1

    print(f"Non-Transparent Pixels: {opaque_pixel_count}")
    print(f"Black Pixels (Outlines): {black_pixel_count}")
    print(f"White Pixels (Highlights/Outlines): {white_pixel_count}")
    
    if black_pixel_count > 50:
        print("VERIFICATION RESULT: SUCCESS - Black Outlines Detected!")
    elif white_pixel_count > 50:
         print("VERIFICATION RESULT: SUCCESS - White Outlines Detected!")
    else:
        print("VERIFICATION RESULT: FAILED - No Outline Colors found.")

except Exception as e:
    print(f"Error analyzing image: {e}")
    sys.exit(1)
