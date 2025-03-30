import os
import cv2
import numpy as np
from skimage.metrics import structural_similarity as ssim
from skimage.metrics import peak_signal_noise_ratio as psnr

def calculate_metrics(predict_dir, reference_dir):
    # 存储每对图片的指标
    ssim_scores = []
    psnr_scores = []
    
    # 获取预测文件夹中的所有图片
    predict_files = sorted([f for f in os.listdir(predict_dir) if f.endswith('.png')])
    
    # 计数器
    processed_pairs = 0
    
    for predict_file in predict_files:
        # 构建对应的reference文件名
        reference_file = predict_file
        predict_path = os.path.join(predict_dir, predict_file)
        reference_path = os.path.join(reference_dir, reference_file)
        
        # 检查reference文件是否存在
        if not os.path.exists(reference_path):
            print(f"Warning: No matching reference file found for {predict_file}")
            continue
        
        # 读取图片
        predict_img = cv2.imread(predict_path)
        reference_img = cv2.imread(reference_path)
        
        if predict_img is None or reference_img is None:
            print(f"Error reading images: {predict_file}")
            continue
            
        # 确保图片尺寸相同
        if predict_img.shape != reference_img.shape:
            # 如果尺寸不同，将reference图像调整到与predict图像相同的尺寸
            print("Error size!")
            continue
        
        # 转换为灰度图（SSIM需要）
        predict_gray = cv2.cvtColor(predict_img, cv2.COLOR_BGR2GRAY)
        reference_gray = cv2.cvtColor(reference_img, cv2.COLOR_BGR2GRAY)
        
        # 计算SSIM
        ssim_score = ssim(predict_gray, reference_gray)
        # 计算PSNR
        psnr_score = psnr(reference_img, predict_img)
        
        ssim_scores.append(ssim_score)
        psnr_scores.append(psnr_score)
        
        processed_pairs += 1
        print(f"Processed {predict_file}: SSIM = {ssim_score:.4f}, PSNR = {psnr_score:.4f}")
    
    # 计算平均值
    if processed_pairs > 0:
        avg_ssim = np.mean(ssim_scores)
        avg_psnr = np.mean(psnr_scores)
        print(f"\nProcessed {processed_pairs} image pairs")
        print(f"Average SSIM: {avg_ssim:.4f}")
        print(f"Average PSNR: {avg_psnr:.4f}")
    else:
        print("No image pairs were processed successfully")
    
    return ssim_scores, psnr_scores

def main():
    # 获取当前脚本所在目录（Scripts文件夹）
    script_dir = os.path.dirname(os.path.abspath(__file__))
    # 构建Predict和Reference文件夹的路径
    predict_dir = os.path.join(script_dir, '..', 'Predict/Screen')
    reference_dir = os.path.join(script_dir, '..', 'Reference/Screen')
    
    # 检查文件夹是否存在
    if not os.path.exists(predict_dir) or not os.path.exists(reference_dir):
        print("Error: One or both directories do not exist")
        return
    
    print("Starting SSIM and PSNR calculation...")
    ssim_scores, psnr_scores = calculate_metrics(predict_dir, reference_dir)

if __name__ == "__main__":
    main()