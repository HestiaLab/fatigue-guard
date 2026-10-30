namespace FatigueGuard.Core.Detection;

public static class EyeLandmarks
{
    // 6 点顺序：p1 外眼角、p2/p3 上睑、p4 内眼角、p5/p6 下睑。
    public static readonly int[] RightEye = { 33, 160, 158, 133, 153, 144 };

    public static readonly int[] LeftEye = { 362, 385, 387, 263, 373, 380 };
}
