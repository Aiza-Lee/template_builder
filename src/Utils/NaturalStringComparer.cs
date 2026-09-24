namespace Utils {
    /// <summary>
    /// 自然字母数字排序比较器（Natural / Alphanumeric Sort）。
    /// <para>
    /// 遵循人类直觉，将文件名中的数字段作为数值而非纯字符比较，
    /// 确保 "2.py" 排在 "10.py" 之前，消除标准字典序中 "10" 排在 "2" 前面的缺陷。
    /// 同时支持任意大整数（无溢出风险）与大小写不敏感匹配。
    /// </para>
    /// </summary>
    public sealed class NaturalStringComparer : IComparer<string?> {
        public static readonly NaturalStringComparer OrdinalIgnoreCase = new();

        public int Compare(string? x, string? y) {
            if (ReferenceEquals(x, y)) return 0;
            if (x == null) return -1;
            if (y == null) return 1;

            int ix = 0;
            int iy = 0;

            while (ix < x.Length && iy < y.Length) {
                char cx = x[ix];
                char cy = y[iy];

                if (char.IsDigit(cx) && char.IsDigit(cy)) {
                    // 读取两边连续的数字段
                    int startX = ix;
                    while (ix < x.Length && char.IsDigit(x[ix])) ix++;
                    var numSpanX = x.AsSpan(startX, ix - startX);

                    int startY = iy;
                    while (iy < y.Length && char.IsDigit(y[iy])) iy++;
                    var numSpanY = y.AsSpan(startY, iy - startY);

                    // 剥离前导 0 进行数值比较
                    var trimmedX = numSpanX.TrimStart('0');
                    var trimmedY = numSpanY.TrimStart('0');

                    // 长度不同：长度更长的数值绝对更大
                    if (trimmedX.Length != trimmedY.Length) {
                        return trimmedX.Length.CompareTo(trimmedY.Length);
                    }

                    // 长度相同：逐位对比数值
                    int numCmp = trimmedX.SequenceCompareTo(trimmedY);
                    if (numCmp != 0) {
                        return numCmp;
                    }

                    // 数值相等（如 "01" vs "1"）：前导零较少或总长度不同时作为 tie-breaker 维持稳定排序
                    if (numSpanX.Length != numSpanY.Length) {
                        return numSpanX.Length.CompareTo(numSpanY.Length);
                    }
                } else if (char.IsDigit(cx)) {
                    // 数字在 ASCII/Unicode 中排在字母前
                    return -1;
                } else if (char.IsDigit(cy)) {
                    return 1;
                } else {
                    // 非数字字符按 OrdinalIgnoreCase 比较
                    int cmp = char.ToUpperInvariant(cx).CompareTo(char.ToUpperInvariant(cy));
                    if (cmp != 0) {
                        return cmp;
                    }
                    ix++;
                    iy++;
                }
            }

            return x.Length.CompareTo(y.Length);
        }
    }
}
