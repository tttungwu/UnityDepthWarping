import os
import subprocess
import numpy as np
from PIL import Image
from skimage.metrics import structural_similarity as ssim
from skimage.metrics import peak_signal_noise_ratio as psnr
import cv2

# 获取当前脚本所在目录（Scripts文件夹）
script_dir = os.path.dirname(os.path.abspath(__file__))

# 构建Predict和Reference文件夹的路径
predict_dir = os.path.join(script_dir, '..', 'Predict')
reference_dir = os.path.join(script_dir, '..', 'Reference')

def read_depth_file(file_path):
    """读取单个bin文件，返回1920x1080的float数组"""
    expected_size = 1920 * 1080 * 4
    try:
        with open(file_path, 'rb') as f:
            data = f.read()
            if len(data) != expected_size:
                print(f"警告: 文件 {file_path} 大小不符合预期")
            depth_array = np.frombuffer(data, dtype=np.float32).reshape(1080, 1920)
            return depth_array
    except Exception as e:
        print(f"读取文件 {file_path} 时出错: {str(e)}")
        return None

def depth_to_grayscale(depth_array):
    """将深度数据转换为0-255的灰度图"""
    depth_min = depth_array.min()
    depth_max = depth_array.max()
    if depth_max == depth_min:
        normalized = np.zeros_like(depth_array, dtype=np.uint8)
    else:
        normalized = ((depth_array - depth_min) / (depth_max - depth_min) * 255).astype(np.uint8)
    normalized = (depth_array * 255).astype(np.uint8)
    return normalized

def process_folder(folder_path, folder_name):
    """处理指定文件夹中的所有depthDatax.bin文件并保存为灰度图"""
    depth_data_dict = {}
    bin_files = [f for f in os.listdir(folder_path) if f.startswith('depthData') and f.endswith('.bin')]
    bin_files.sort()
    
    for bin_file in bin_files:
        file_path = os.path.join(folder_path, bin_file)
        depth_array = read_depth_file(file_path)
        if depth_array is not None:
            depth_data_dict[bin_file] = depth_array
            gray_image = depth_to_grayscale(depth_array)
            output_filename = bin_file.replace('.bin', '.png')
            output_path = os.path.join(folder_path, output_filename)
            img = Image.fromarray(gray_image)
            img.save(output_path)
            print(f"成功处理 {folder_name}/{bin_file} -> {output_filename}")
    
    return depth_data_dict

def calculate_metrics(predict_data, reference_data):
    """计算每组Predict/depthDatai.png和Reference/depthData(i+1).png的SSIM和PSNR，并返回平均值"""
    metrics = []
    ssim_sum = 0
    psnr_sum = 0
    
    for i in range(len(predict_data)):
        predict_key = f'depthData{i}.bin'
        reference_key = f'depthData{i+1}.bin'
        
        if predict_key in predict_data and reference_key in reference_data:
            pred_array = predict_data[predict_key]
            ref_array = reference_data[reference_key]
            pred_gray = depth_to_grayscale(pred_array)
            ref_gray = depth_to_grayscale(ref_array)
            ssim_value = ssim(pred_gray, ref_gray, data_range=255)
            psnr_value = psnr(pred_gray, ref_gray, data_range=255)
            
            metrics.append({
                'predict': f'depthData{i}.png',
                'reference': f'depthData{i+1}.png',
                'ssim': ssim_value,
                'psnr': psnr_value
            })
            
            ssim_sum += ssim_value
            psnr_sum += psnr_value
            
            print(f"比较 Predict/depthData{i}.png 和 Reference/depthData{i+1}.png:")
            print(f"  SSIM: {ssim_value:.4f}")
            print(f"  PSNR: {psnr_value:.2f} dB")
    
    # 计算平均值
    if metrics:
        ssim_avg = ssim_sum / len(metrics)
        psnr_avg = psnr_sum / len(metrics)
        print(f"\n平均值:")
        print(f"  平均 SSIM: {ssim_avg:.4f}")
        print(f"  平均 PSNR: {psnr_avg:.2f} dB")
    else:
        ssim_avg = None
        psnr_avg = None
        print("\n没有可用的比较数据，无法计算平均值")
    
    return metrics, ssim_avg, psnr_avg

def create_video(folder_path, output_filename, fps=30):
    """使用 FFmpeg 将文件夹中的PNG图片合成为视频"""
    try:
        # 获取PNG文件列表并排序
        img_files = [f for f in os.listdir(folder_path) if f.startswith('depthData') and f.endswith('.png')]
        img_files.sort()
        
        if not img_files:
            print(f"警告: {folder_path} 中没有找到PNG文件")
            return False
        
        # 检查 FFmpeg 是否可用
        try:
            subprocess.run(['ffmpeg', '-version'], stdout=subprocess.PIPE, stderr=subprocess.PIPE, check=True)
        except (subprocess.CalledProcessError, FileNotFoundError):
            print("错误: FFmpeg 未安装或未找到，请确保 FFmpeg 已安装并添加到系统路径")
            return False
        
        # 构建 FFmpeg 命令
        video_path = os.path.join(folder_path, output_filename)
        input_pattern = os.path.join(folder_path, "depthData%d.png")
        
        # FFmpeg 命令：
        # -r: 设置帧率
        # -i: 输入文件模式，使用 %d 通配符匹配 depthData0.png, depthData1.png 等
        # -c:v libx264: 使用 H.264 编码
        # -pix_fmt yuv420p: 设置像素格式以确保广泛兼容性
        # -y: 强制覆盖输出文件
        ffmpeg_cmd = [
            'ffmpeg',
            '-r', str(fps),              # 帧率
            '-i', input_pattern,         # 输入文件模式
            '-c:v', 'libx264',           # 视频编码器
            '-pix_fmt', 'yuv420p',       # 像素格式
            '-y',                        # 覆盖输出文件
            video_path                   # 输出文件路径
        ]
        
        # 执行 FFmpeg 命令
        result = subprocess.run(ffmpeg_cmd, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
        
        if result.returncode != 0:
            print(f"错误: FFmpeg 执行失败")
            print(f"FFmpeg 输出: {result.stderr}")
            return False
        
        print(f"视频已保存至: {video_path}")
        return True
    
    except Exception as e:
        print(f"创建视频时发生错误: {str(e)}")
        return False

def main():
    if not os.path.exists(predict_dir):
        print(f"错误: Predict文件夹不存在 - {predict_dir}")
        return
    if not os.path.exists(reference_dir):
        print(f"错误: Reference文件夹不存在 - {reference_dir}")
        return
    
    print("开始处理Predict文件夹...")
    predict_data = process_folder(predict_dir, "Predict")
    
    print("\n开始处理Reference文件夹...")
    reference_data = process_folder(reference_dir, "Reference")
    
    print("\n开始计算SSIM和PSNR...")
    metrics, ssim_avg, psnr_avg = calculate_metrics(predict_data, reference_data)
    
    print("\n创建Predict视频...")
    create_video(predict_dir, "predict_video.mp4")
    
    print("\n创建Reference视频...")
    create_video(reference_dir, "reference_video.mp4")
    
    print(f"\n处理完成!")
    print(f"Predict文件夹共处理 {len(predict_data)} 个文件")
    print(f"Reference文件夹共处理 {len(reference_data)} 个文件")
    print(f"共计算 {len(metrics)} 组指标")

if __name__ == "__main__":
    main()