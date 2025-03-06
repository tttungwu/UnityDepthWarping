## 算法流程
- 将上一帧的DepthBuffer逐像素p逆变换回世界空间，再利用当前帧的变换矩阵投影到屏幕空间中得到像素位置q，利用q - p得到位置差作为二维向量MotionVector
- 根据Motion Vector构建min-max mipmap：第0层mipmap的(i, j)像素存储的是(MotionVector[i][j], MotionVector[i][j])，之后的每一层存储的是 (min x, min y, max x, max y)
- 利用mipmap进行不动点迭代得到预测的DepthBuffer
- 对DepthBuffer构建yMaps（所谓yMaps也就是前x层是mipmap，后面的层都是NBuffer）
- 对通过视锥体剔除的物体，首先将他们的包围盒投影到屏幕空间得到一个区域，利用yMaps查询这个区域的预测最大深度值，将这个值物体包围盒在这个区域的最小深度值比较，如果最大值比最小值小，则剔除

## 我的实现
- 上一帧的DepthBuffer通过在渲染管线中增加Feature，把上一帧的DepthBuffer一次以便下一帧使用
- 接下来的流程基本都用compute shader的不同Kernel实现：
  - Kernel 0：GetMotionVector
    - 输入：Texture2D<float> PrevDepthTexture和投影矩阵
    - 输出：RWTexture2D<float4> ForwardWarpingDepthTexture;
      RWTexture2D<float4> MotionVector;
  - Kernel 1： GenerateMipmap（循环调用以计算每一层的信息）
    - 输入：Texture2D<float4> MotionTextureSrc;
    - 输出：RWTexture2D<float4> MotionTextureDst;
  - Kernel 2：BackwardSearch
    - 输入：Texture2D<float4> MotionVectorAndPredictedDepthTexture;
      Texture2D<float4> MipmapMotionVectorsTexture;
    - 输出：RWTexture2D<float> BackwardWarpingDepthTexture;
  - Kernel 3: GenerateNBuffer（类似GenerateMipmap）
  - Kernel 4：ComputeVisibility（目前正在做，由于上一步得到的内容是