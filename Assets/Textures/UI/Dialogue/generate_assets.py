import numpy as np
from PIL import Image, ImageDraw, ImageFilter
import colorsys

# 1. Load reference image
ref_path = '/Users/slaygen/.gemini/antigravity-ide/brain/fee18d74-8c7d-41c9-90a9-cccaca78d8e3/.user_uploaded/media_1792398770116.jpg'
ref_img = Image.open(ref_path).convert('RGBA')
arr = np.array(ref_img, dtype=np.float32)
h, w, _ = arr.shape

print(f"Reference size: {w}x{h}")

# Extract Alita's portrait (unmasked)
# The portrait is located in the left hexagon region
p_crop = ref_img.crop((34, 4, 204, 146))
p_crop.save('Assets/Textures/UI/Dialogue/T_Alita_Dialogue_Portrait.png')
print("Saved T_Alita_Dialogue_Portrait.png")

# 2. Inpaint / clear the static text from the frame
# The body text 'We move now. The city won’t wait.' is roughly X in [230, 750], Y in [70, 115]
# Let's inspect the background around the text
bg_color = np.array([4.0, 9.0, 24.0, 255.0]) # dark glass navy

# Mask for body text: bright pixels in the text region
cleaned = arr.copy()
for y in range(65, 120):
    for x in range(230, 750):
        # text pixels are brighter than background
        lum = arr[y, x, 0] * 0.299 + arr[y, x, 1] * 0.587 + arr[y, x, 2] * 0.114
        if lum > 45.0:
            cleaned[y, x, :3] = bg_color[:3]

# Also clear the static speaker name 'ALITA' so TMP can render any name dynamically
for y in range(30, 56):
    for x in range(225, 310):
        lum = arr[y, x, 0] * 0.299 + arr[y, x, 1] * 0.587 + arr[y, x, 2] * 0.114
        if lum > 35.0:
            cleaned[y, x, :3] = np.array([3.0, 6.0, 18.0])

# 3. Alpha transparency for the frame
# Outside of the dialogue box is pure black (lum < 15 and not in the box)
# Let's create an alpha mask
alpha = np.ones((h, w), dtype=np.float32) * 255.0

# Outside corners:
# Top-left (outside hex):
for y in range(h):
    for x in range(w):
        # Outer background is very dark black [1, 9, 19]
        r, g, b = cleaned[y, x, :3]
        lum = r * 0.299 + g * 0.587 + b * 0.114
        
        # Check if outside dialogue box:
        # Before X=34 or after rightmost tip (X > 995)
        if x < 25 or x > 998:
            alpha[y, x] = 0
        elif y < 3 or y > 146:
            alpha[y, x] = 0
        elif (x < 48 and y < 35) or (x < 48 and y > 115):
            # top-left and bottom-left chamfer outside
            alpha[y, x] = 0
        elif x > 960 and (y < 45 or y > 135):
            # top-right and bottom-right chamfer outside
            if y < 45 and (x - 960) > (y - 5) * 1.5:
                alpha[y, x] = 0
            elif y > 135 and (x - 960) > (145 - y) * 1.5:
                alpha[y, x] = 0
        elif y < 28 and x > 185 and x < 215:
            # gap above left shoulder
            alpha[y, x] = 0
        elif y < 40 and x > 425:
            # above the top border line
            alpha[y, x] = 0

cleaned[:, :, 3] = alpha

# Save purple frame
purple_frame = Image.fromarray(np.clip(cleaned, 0, 255).astype(np.uint8))
purple_frame.save('Assets/Textures/UI/Dialogue/UI_Dialogue_Frame_Purple.png')
print("Saved UI_Dialogue_Frame_Purple.png")

# 4. Generate Cyan Frame by shifting purple hues to electric cyan
cyan_arr = cleaned.copy()
for y in range(h):
    for x in range(w):
        if cyan_arr[y, x, 3] > 0:
            r, g, b = cyan_arr[y, x, :3] / 255.0
            # convert to hsv
            h_val, s_val, v_val = colorsys.rgb_to_hsv(r, g, b)
            # purple hue is around 0.70 to 0.85 (250 to 306 deg)
            if 0.65 <= h_val <= 0.90:
                # shift to cyan: hue ~ 0.50 to 0.53 (180 to 195 deg)
                new_h = 0.51 + (h_val - 0.75) * 0.3
                new_r, new_g, new_b = colorsys.hsv_to_rgb(new_h, min(1.0, s_val * 1.15), v_val)
                cyan_arr[y, x, 0] = new_r * 255.0
                cyan_arr[y, x, 1] = new_g * 255.0
                cyan_arr[y, x, 2] = new_b * 255.0

# In Doctor Ido's reference, there is a dotted telemetry line near the advance arrow:
# Add cyan dots: X from 860 to 930 at Y=112
for x_dot in range(860, 930, 8):
    for dy in range(2):
        for dx in range(2):
            cyan_arr[111 + dy, x_dot + dx, :3] = [0.0, 240.0, 255.0]

cyan_frame = Image.fromarray(np.clip(cyan_arr, 0, 255).astype(np.uint8))
cyan_frame.save('Assets/Textures/UI/Dialogue/UI_Dialogue_Frame_Cyan.png')
print("Saved UI_Dialogue_Frame_Cyan.png")

# 5. Doctor Ido Portrait:
# Doctor Ido with the iconic cyber visor and vibrant red neon backlight with diagonal white/red light tube
# Create 512x512 portrait
ido_img = Image.new('RGBA', (512, 512), (0, 0, 0, 255))
ido_draw = ImageDraw.Draw(ido_img)

# Crimson red cyber backdrop with diagonal light bar
# Gradient from #FF1E27 down to #600508
for y in range(512):
    ratio = y / 512.0
    red = int(240 * (1.0 - ratio * 0.65))
    green = int(25 * (1.0 - ratio))
    blue = int(35 * (1.0 - ratio))
    ido_draw.line([(0, y), (511, y)], fill=(red, green, blue, 255))

# Diagonal bright red/white light beam running from top-center to right-center
for offset in range(-25, 26):
    alpha_beam = int(255 * (1.0 - abs(offset) / 25.0))
    ido_draw.line([(310 + offset, 0), (460 + offset, 512)], fill=(255, 200, 210, alpha_beam), width=2)

# Load existing Doctor Ido texture to composite head
ido_base_path = 'Assets/Characters/Doctor/Textures/T_Doctor_Ido_HoloPortrait.png'
try:
    ido_base = Image.open(ido_base_path).convert('RGBA')
    # Doctor Ido head is in center
    ido_head_crop = ido_base.crop((60, 40, 450, 470)).resize((440, 470), Image.Resampling.LANCZOS)
    
    # Mask out dark background from doctor head
    head_arr = np.array(ido_head_crop, dtype=np.float32)
    # Background is dark cyan/navy
    bg_dist = np.sqrt(head_arr[:, :, 0]**2 + head_arr[:, :, 1]**2 + head_arr[:, :, 2]**2)
    head_mask = np.clip((bg_dist - 35) / 40.0, 0.0, 1.0)
    head_arr[:, :, 3] = head_mask * 255.0
    
    ido_head_clean = Image.fromarray(np.clip(head_arr, 0, 255).astype(np.uint8))
    ido_img.paste(ido_head_clean, (25, 30), ido_head_clean)
except Exception as ex:
    print(f"Doctor head compositing notice: {ex}")

# Draw Doctor Ido cyber visor: dark horizontal visor with glowing cyan light bar
# Visor across eyes
ido_draw.rectangle([130, 195, 335, 230], fill=(12, 16, 22, 255))
# Glowing cyan horizontal visor light line
ido_draw.line([(140, 212), (325, 212)], fill=(0, 245, 255, 255), width=4)
# Glowing cyan inverted triangle on chest collar
ido_draw.polygon([(230, 420), (270, 420), (250, 455)], fill=(0, 240, 255, 255))

ido_img.save('Assets/Textures/UI/Dialogue/T_DoctorIdo_Dialogue_Portrait.png')
print("Saved T_DoctorIdo_Dialogue_Portrait.png")

# 6. Glowing Downward Triangles for Next Indicator
for theme, color, path in [
    ('purple', (210, 155, 255), 'Assets/Textures/UI/Dialogue/UI_Dialogue_Next_Purple.png'),
    ('cyan', (0, 240, 255), 'Assets/Textures/UI/Dialogue/UI_Dialogue_Next_Cyan.png')
]:
    tri_img = Image.new('RGBA', (64, 64), (0, 0, 0, 0))
    t_draw = ImageDraw.Draw(tri_img)
    # Downward pointing equilateral triangle: top=(12, 18), (52, 18), bottom=(32, 48)
    t_draw.polygon([(12, 18), (52, 18), (32, 48)], fill=(color[0], color[1], color[2], 255))
    tri_img.save(path)
    print(f"Saved {path}")

print("All dialogue textures generated successfully!")
