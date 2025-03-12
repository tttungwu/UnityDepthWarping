import os
import re
import numpy as np
from scipy.ndimage import uniform_filter

# Function to calculate Mean Squared Error (MSE) between two images
def calculate_mse(image1, image2):
    if image1.shape != image2.shape:
        raise ValueError("Images must have the same dimensions")
    return np.mean((image1 - image2) ** 2)

# Function to calculate PSNR between two images
def calculate_psnr(image1, image2, data_range=1.0):
    mse = calculate_mse(image1, image2)
    if mse == 0:
        return float('inf')  # Return infinity if images are identical
    return 10 * np.log10((data_range ** 2) / mse)

# Function to calculate SSIM between two images
def calculate_ssim(image1, image2, data_range=1.0, win_size=11):
    if image1.shape != image2.shape:
        raise ValueError("Images must have the same dimensions")
    
    # Compute local means using a uniform filter
    mu1 = uniform_filter(image1, size=win_size)
    mu2 = uniform_filter(image2, size=win_size)
    
    # Compute squares of means and their product
    mu1_sq = mu1 ** 2
    mu2_sq = mu2 ** 2
    mu1_mu2 = mu1 * mu2
    
    # Compute local variances and covariance
    sigma1_sq = uniform_filter(image1 ** 2, size=win_size) - mu1_sq
    sigma2_sq = uniform_filter(image2 ** 2, size=win_size) - mu2_sq
    sigma12 = uniform_filter(image1 * image2, size=win_size) - mu1_mu2
    
    # Clip variances to prevent negative values due to floating-point errors
    sigma1_sq = np.maximum(sigma1_sq, 0)
    sigma2_sq = np.maximum(sigma2_sq, 0)
    
    # Compute standard deviations
    sigma1 = np.sqrt(sigma1_sq)
    sigma2 = np.sqrt(sigma2_sq)
    
    # Define constants to stabilize the SSIM calculation
    K1 = 0.01
    K2 = 0.03
    C1 = (K1 * data_range) ** 2
    C2 = (K2 * data_range) ** 2
    C3 = C2 / 2
    
    # Compute luminance, contrast, and structure components
    l = (2 * mu1_mu2 + C1) / (mu1_sq + mu2_sq + C1)
    c = (2 * sigma1 * sigma2 + C2) / (sigma1_sq + sigma2_sq + C2)
    s = (sigma12 + C3) / (sigma1 * sigma2 + C3)
    
    # Compute SSIM map and return its mean
    ssim_map = l * c * s
    return np.mean(ssim_map)

# Get the directory where this script is located
script_dir = os.path.dirname(os.path.abspath(__file__))

# Define paths to Predict and Reference directories
predict_dir = os.path.join(script_dir, '..', 'Predict')
reference_dir = os.path.join(script_dir, '..', 'Reference')

# Function to extract the number 'i' from a filename
def extract_number(filename):
    match = re.search(r'depthData(\d+)\.bin', filename)
    if match:
        return int(match.group(1))
    return -1  # Return -1 if the filename doesn't match the pattern

# Get a sorted list of files in Predict directory
predict_files = sorted(
    [f for f in os.listdir(predict_dir) if re.match(r'depthData\d+\.bin', f)],
    key=extract_number
)

# Initialize lists to store PSNR and SSIM values
psnr_values = []
ssim_values = []

# Process each pair of files
for predict_file in predict_files:
    i = extract_number(predict_file)
    if i == -1:
        print(f"Invalid filename: {predict_file}. Skipping.")
        continue
    
    # Construct the corresponding reference filename
    reference_file = f'depthData{i+1}.bin'
    reference_path = os.path.join(reference_dir, reference_file)
    predict_path = os.path.join(predict_dir, predict_file)
    
    # Check if the reference file exists
    if not os.path.exists(reference_path):
        print(f"Reference file {reference_file} does not exist. Skipping this pair.")
        continue
    
    # Read the binary files as 1080x1920 float32 arrays
    with open(predict_path, 'rb') as f:
        img_predict = np.fromfile(f, dtype=np.float32).reshape(1080, 1920)
    with open(reference_path, 'rb') as f:
        img_reference = np.fromfile(f, dtype=np.float32).reshape(1080, 1920)
    
    # Check if image values are within the expected range [0,1]
    if np.min(img_predict) < 0 or np.max(img_predict) > 1:
        print(f"Warning: Predict image {predict_file} has values outside [0,1]: {np.min(img_predict)}, {np.max(img_predict)}")
    if np.min(img_reference) < 0 or np.max(img_reference) > 1:
        print(f"Warning: Reference image {reference_file} has values outside [0,1]: {np.min(img_reference)}, {np.max(img_reference)}")
    
    # Calculate PSNR and SSIM
    psnr = calculate_psnr(img_reference, img_predict, data_range=1.0)
    ssim = calculate_ssim(img_reference, img_predict, data_range=1.0)
    
    # Print results for the current pair
    print(f"File pair: {predict_file} and {reference_file}")
    print(f"PSNR: {psnr:.4f}")
    print(f"SSIM: {ssim:.4f}")
    print()
    
    # Store the values
    psnr_values.append(psnr)
    ssim_values.append(ssim)

# Calculate and print average PSNR and SSIM if any pairs were processed
if psnr_values:
    average_psnr = np.mean(psnr_values)
    average_ssim = np.mean(ssim_values)
    print("Average PSNR:", f"{average_psnr:.4f}")
    print("Average SSIM:", f"{average_ssim:.4f}")
else:
    print("No file pairs were processed.")