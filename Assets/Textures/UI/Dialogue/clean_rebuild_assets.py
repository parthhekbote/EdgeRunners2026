import numpy as np
from PIL import Image, ImageDraw, ImageFilter
import colorsys

# 1. Load reference image
ref_path = '/Users/slaygen/.gemini/antigravity-ide/brain/fee18d74-8c7d-41c9-90a9-cccaca78d8e3/.user_uploaded/media_1792398770116.jpg'
ref_img = Image.open(ref_path).convert('RGBA')
arr = np.array(ref_img, dtype=np.float32)
h, w, _ = arr.shape

print(f"Loaded reference: {w}x{h}")

# Define inner hexagon polygon in 1024x157 coordinates
# Center is (118, 75)
center = np.array([118.0, 75.0])
base_pts = [
    (100, 14), (136, 14),
    (184, 55), (184, 95),
    (136, 136), (100, 136),
    (52, 95), (52, 55)
]

hex_mask_img = Image.new('L', (w, h), 0)
hex_draw = ImageDraw.Draw(hex_mask_img)
hex_draw.polygon(base_pts, fill=255)
hex_mask = np.array(hex_mask_img) > 128

# 2. Extract Alita's Portrait (Masked inside the hexagon)
# Cut Alita's face and background out of the reference
alita_portrait_arr = np.zeros((h, w, 4), dtype=np.float32)
alita_portrait_arr[:, :, :3] = arr[:, :, :3]
# Smooth anti-aliased alpha mask for portrait
hex_mask_smooth = np.array(hex_mask_img.filter(ImageFilter.GaussianBlur(radius=0.75))) / 255.0
alita_portrait_arr[:, :, 3] = hex_mask_smooth * 255.0

# Crop tight to the hexagon bounds [X: 45..192, Y: 8..142]
alita_crop = Image.fromarray(np.clip(alita_portrait_arr, 0, 255).astype(np.uint8)).crop((45, 8, 192, 142))
alita_crop.save('Assets/Textures/UI/Dialogue/T_Alita_Dialogue_Portrait.png')
print(f"Saved T_Alita_Dialogue_Portrait.png (size={alita_crop.size})")

# 3. Create Doctor Ido's Portrait (Masked inside the exact same hexagon)
ido_portrait_arr = np.zeros((h, w, 4), dtype=np.float32)
# Create the dramatic crimson red backdrop with diagonal neon light beam
for y in range(h):
    ratio = y / float(h)
    # Crimson red gradient
    red = 230 * (1.0 - ratio * 0.45)
    green = 20 * (1.0 - ratio * 0.5)
    blue = 28 * (1.0 - ratio * 0.5)
    ido_portrait_arr[y, :, 0] = red
    ido_portrait_arr[y, :, 1] = green
    ido_portrait_arr[y, :, 2] = blue

# Diagonal bright red/white neon beam
for x in range(w):
    # Line from (145, 0) to (190, 157)
    target_x = 145 + (x - 145) # check distance to diagonal
    pass

for y in range(h):
    beam_x = 140 + int(y * 0.32)
    for dx in range(-8, 9):
        x = beam_x + dx
        if 0 <= x < w:
            intensity = 1.0 - abs(dx) / 8.0
            ido_portrait_arr[y, x, 0] = min(255, ido_portrait_arr[y, x, 0] + 120 * intensity)
            ido_portrait_arr[y, x, 1] = min(255, ido_portrait_arr[y, x, 1] + 180 * intensity)
            ido_portrait_arr[y, x, 2] = min(255, ido_portrait_arr[y, x, 2] + 200 * intensity)

# Paste Doctor Ido head in center
try:
    ido_base = Image.open('Assets/Characters/Doctor/Textures/T_Doctor_Ido_HoloPortrait.png').convert('RGBA')
    # Doctor head crop
    head_crop = ido_base.crop((60, 40, 450, 470)).resize((115, 125), Image.Resampling.LANCZOS)
    head_arr = np.array(head_crop, dtype=np.float32)
    # Mask out dark background
    lum = head_arr[:, :, 0] * 0.299 + head_arr[:, :, 1] * 0.587 + head_arr[:, :, 2] * 0.114
    head_alpha = np.clip((lum - 15) / 25.0, 0.0, 1.0)
    
    # Place head at (60, 15) in 1024x157
    hx, hy = 60, 15
    for y in range(125):
        for x in range(115):
            if hy + y < h and hx + x < w:
                a = head_alpha[y, x]
                if a > 0:
                    ido_portrait_arr[hy + y, hx + x, :3] = (
                        head_arr[y, x, :3] * a + ido_portrait_arr[hy + y, hx + x, :3] * (1.0 - a)
                    )
except Exception as ex:
    print(f"Notice during doctor head compositing: {ex}")

# Draw Doctor Ido cyber visor: dark horizontal visor with glowing cyan light bar
# Visor at center of face: around X in [92, 142], Y in [60, 72]
ido_portrait_arr[62:72, 94:142, :3] = [10.0, 14.0, 18.0]
# Glowing cyan horizontal visor light line
ido_portrait_arr[66:68, 97:139, :3] = [0.0, 245.0, 255.0]
# Cyan triangle on collar: Y in [112, 124], X in [110, 126]
for dy in range(12):
    half_w = int((12 - dy) * 0.7)
    ido_portrait_arr[112 + dy, 118 - half_w : 118 + half_w + 1, :3] = [0.0, 240.0, 255.0]

# Apply hexagonal mask
ido_portrait_arr[:, :, 3] = hex_mask_smooth * 255.0

# Crop to same bounds [X: 45..192, Y: 8..142]
ido_crop = Image.fromarray(np.clip(ido_portrait_arr, 0, 255).astype(np.uint8)).crop((45, 8, 192, 142))
ido_crop.save('Assets/Textures/UI/Dialogue/T_DoctorIdo_Dialogue_Portrait.png')
print(f"Saved T_DoctorIdo_Dialogue_Portrait.png (size={ido_crop.size})")

# 4. Clean Rebuild of the Dialogue Box Frames (Purple and Cyan)
# Start from reference array
cleaned_frame = arr.copy()

# Sample the clean dark glass background from the right side of the box (X: 750..920, Y: 65..120)
clean_patch = arr[65:120, 750:920, :3]
# Compute smooth vertical gradient for glass
vert_grad = clean_patch.mean(axis=1) # shape (55, 3)

# A. Completely CLEAR the text area (X: 215..940, Y: 58..128)
# Replace with smooth dark cyber glass
for y in range(58, 128):
    gy = min(54, max(0, y - 65))
    base_color = vert_grad[gy]
    for x in range(215, 940):
        # subtle glass shading
        cleaned_frame[y, x, :3] = base_color

# B. Completely CLEAR the speaker name tab (X: 220..365, Y: 28..56)
for y in range(28, 56):
    for x in range(220, 365):
        # Keep the top border line at Y <= 30
        if y > 30 and y < 55 and x > 223 and x < 362:
            cleaned_frame[y, x, :3] = [4.0, 8.0, 26.0]

# C. Completely CLEAR the advance arrow area (X: 944..970, Y: 100..125)
for y in range(100, 125):
    for x in range(944, 970):
        cleaned_frame[y, x, :3] = vert_grad[min(54, y - 65)]

# D. Inside the hex portrait window: SET ALPHA = 0!
# That way, the portrait placed behind the frame shows through cleanly,
# and the glowing neon frame and corner brackets sit ON TOP of it!
alpha_channel = np.ones((h, w), dtype=np.float32) * 255.0

# Outside background is pure black -> transparent
for y in range(h):
    for x in range(w):
        if x < 26 or x > 996:
            alpha_channel[y, x] = 0.0
        elif y < 3 or y > 146:
            alpha_channel[y, x] = 0.0
        elif (x < 46 and y < 34) or (x < 46 and y > 116):
            alpha_channel[y, x] = 0.0
        elif y < 28 and 185 < x < 215:
            alpha_channel[y, x] = 0.0
        elif y < 38 and x > 430:
            alpha_channel[y, x] = 0.0
        elif x > 960 and (y < 46 or y > 134):
            # 45-degree chamfers on right edge
            if y < 46 and (x - 960) > (y - 5) * 1.5:
                alpha_channel[y, x] = 0.0
            elif y > 134 and (x - 960) > (145 - y) * 1.5:
                alpha_channel[y, x] = 0.0

# Now, punch out the interior of the hex portrait window so it is 100% transparent!
for y in range(h):
    for x in range(w):
        if hex_mask[y, x]:
            alpha_channel[y, x] = 0.0

cleaned_frame[:, :, 3] = alpha_channel

# Save Clean Purple Frame
purple_img = Image.fromarray(np.clip(cleaned_frame, 0, 255).astype(np.uint8))
purple_img.save('Assets/Textures/UI/Dialogue/UI_Dialogue_Frame_Purple.png')
print("Saved UI_Dialogue_Frame_Purple.png (100% clean, no text, transparent hex window)")

# 5. Clean Cyan Frame
# Convert glowing purple borders and chevrons to electric cyan (#00F0FF)
cyan_frame = cleaned_frame.copy()
for y in range(h):
    for x in range(w):
        if cyan_frame[y, x, 3] > 0:
            r, g, b = cyan_frame[y, x, :3] / 255.0
            h_val, s_val, v_val = colorsys.rgb_to_hsv(r, g, b)
            # purple hue is around 0.65 to 0.90
            if 0.65 <= h_val <= 0.92:
                new_h = 0.51 + (h_val - 0.75) * 0.25 # Electric cyan
                new_r, new_g, new_b = colorsys.hsv_to_rgb(new_h, min(1.0, s_val * 1.15), v_val)
                cyan_frame[y, x, 0] = new_r * 255.0
                cyan_frame[y, x, 1] = new_g * 255.0
                cyan_frame[y, x, 2] = new_b * 255.0

# Add cyan telemetry dots near the right edge: X in [860, 930], Y=112
for x_dot in range(860, 930, 8):
    for dy in range(2):
        for dx in range(2):
            cyan_frame[111 + dy, x_dot + dx, :3] = [0.0, 240.0, 255.0]

cyan_img = Image.fromarray(np.clip(cyan_frame, 0, 255).astype(np.uint8))
cyan_img.save('Assets/Textures/UI/Dialogue/UI_Dialogue_Frame_Cyan.png')
print("Saved UI_Dialogue_Frame_Cyan.png (100% clean, no text, transparent hex window)")

print("Clean asset generation complete!")
