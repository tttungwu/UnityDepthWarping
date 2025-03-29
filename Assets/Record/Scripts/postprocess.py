from PIL import Image, ImageDraw

def draw_rectangle(image_path, bottom_left, top_right, output_path):
    img = Image.open(image_path)
    
    width, height = img.size

    x1, y1 = bottom_left
    x2, y2 = top_right

    y1 = height - y1
    y2 = height - y2

    draw = ImageDraw.Draw(img)

    draw.rectangle([x1, y2, x2, y1], outline="red", width=5)

    img.save(output_path)
    print(f"矩形已绘制并保存为 {output_path}")

# 示例使用
image_path = "./Assets/Record/Predict/depthData1.png" 
bottom_left = (462, 589)
top_right = (696, 767)
output_path = "./Assets/Record/Predict/depthData1(1).png"

draw_rectangle(image_path, bottom_left, top_right, output_path)
